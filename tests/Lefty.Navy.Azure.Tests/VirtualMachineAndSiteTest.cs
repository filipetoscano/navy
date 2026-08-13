using Lefty.Navy.Azure;
using Lefty.Navy.Model;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace Lefty.Navy.Tests;

/// <summary />
public class VirtualMachineAndSiteTest
{
    private const string EncryptionSetId = "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Compute/diskEncryptionSets/des-one";
    private const string MachineId = "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Compute/virtualMachines/vm-one";
    private const string NicId = "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/networkInterfaces/nic-one";
    private const string SubnetId = "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/virtualNetworks/vnet-one/subnets/snet-app";

    private static readonly ResourceMapper Mapper = new( NullLogger<ResourceMapper>.Instance );
    private static readonly ResourceLinker Linker = new( NullLogger<ResourceLinker>.Instance );


    /// <summary />
    private static T Map<T>( string json )
        where T : AzResource
    {
        return Assert.IsType<T>( Mapper.Map( JsonDocument.Parse( json ).RootElement.Clone() ) );
    }


    /// <summary />
    [Fact]
    public void VirtualMachine_IsFullyMapped()
    {
        var machine = Map<AzVirtualMachine>( MachineJson );

        Assert.Equal( "fe024a1b-9f4e-4ed9-937c-2dab8b8ec3e5", machine.VmId );
        Assert.Equal( "Standard_D2s_v5", machine.VmSize );
        Assert.Equal( "Succeeded", machine.ProvisioningState );
        Assert.Equal( 2025, machine.TimeCreated!.Value.Year );
        Assert.Equal( "Regular", machine.Priority );
        Assert.Equal( "Windows_Server", machine.LicenseType );

        Assert.Equal( "vmone", machine.ComputerName );
        Assert.Equal( "vmadmin", machine.AdminUsername );
        Assert.Equal( "Windows", machine.OsType );
        Assert.Equal( "AutomaticByPlatform", machine.PatchMode );

        Assert.Equal( "TrustedLaunch", machine.SecurityType );
        Assert.True( machine.EncryptionAtHost );
        Assert.True( machine.SecureBootEnabled );
        Assert.True( machine.VTpmEnabled );
        Assert.True( machine.BootDiagnosticsEnabled );
    }


    /// <summary />
    /// <remarks>
    /// Resource Graph folds the instance view into the properties of a virtual
    /// machine, which is where what it is actually running comes from, as
    /// against what it was asked to run.
    /// </remarks>
    [Fact]
    public void VirtualMachine_InstanceViewIsMapped()
    {
        var machine = Map<AzVirtualMachine>( MachineJson );

        Assert.Equal( "PowerState/running", machine.PowerState );
        Assert.Equal( "Windows Server 2022 Datacenter Azure Edition", machine.OsName );
        Assert.Equal( "10.0.20348.4297", machine.OsVersion );
        Assert.Equal( "V2", machine.HyperVGeneration );
    }


    /// <summary />
    /// <remarks>
    /// A deallocated machine is not charged for compute but still pays for its
    /// disks, which is the difference worth reading out of an inventory.
    /// </remarks>
    [Fact]
    public void VirtualMachine_DeallocatedPowerStateIsKept()
    {
        var machine = Map<AzVirtualMachine>( MachineJson.Replace( "PowerState/running", "PowerState/deallocated" ) );

        Assert.Equal( "PowerState/deallocated", machine.PowerState );
    }


    /// <summary />
    [Fact]
    public void VirtualMachine_DisksAreMapped()
    {
        var machine = Map<AzVirtualMachine>( MachineJson );

        var os = machine.OsDisk!;

        Assert.Equal( "md_vm-one_osdisk", os.Name );
        Assert.Equal( 128, os.DiskSizeGB );
        Assert.Equal( "Standard_LRS", os.StorageAccountType );
        Assert.Equal( "ReadWrite", os.Caching );
        Assert.Equal( "Detach", os.DeleteOption );
        Assert.Equal( EncryptionSetId, os.DiskEncryptionSetId );

        var data = Assert.Single( machine.DataDisks );

        Assert.Equal( "md_vm-one_data_1", data.Name );
        Assert.Equal( 1, data.Lun );
        Assert.Equal( 512, data.DiskSizeGB );
        Assert.Equal( "Premium_LRS", data.StorageAccountType );
        Assert.EndsWith( "/disks/md_vm-one_data_1", data.ManagedDiskId );
    }


    /// <summary />
    /// <remarks>
    /// A machine holds its interfaces, so the interface must not resolve back
    /// to the machine or the graph would close a loop.
    /// </remarks>
    [Fact]
    public void VirtualMachine_ReferencesAreResolved()
    {
        var machine = Map<AzVirtualMachine>( MachineJson );
        var nic = Map<AzNetworkInterface>( NicJson );
        var encryptionSet = Map<AzDiskEncryptionSet>( EncryptionSetJson );

        Linker.Link( [ machine, nic, encryptionSet ] );

        Assert.Same( nic, Assert.Single( machine.NetworkInterfaces ) );
        Assert.Same( encryptionSet, machine.DiskEncryptionSet );
        Assert.Equal( MachineId, nic.VirtualMachineId );

        var json = JsonSerializer.Serialize<List<AzResource>>( [ machine, nic, encryptionSet ] );

        Assert.Contains( "vm-one", json );
    }


    /// <summary />
    /// <remarks>
    /// The kind is the only thing telling a web app from a function app, since
    /// both are Microsoft.Web/sites.
    /// </remarks>
    [Theory]
    [InlineData( "app", typeof( AzAppService ) )]
    [InlineData( "app,linux", typeof( AzAppService ) )]
    [InlineData( "api", typeof( AzAppService ) )]
    [InlineData( "functionapp", typeof( AzFunctionApp ) )]
    [InlineData( "functionapp,linux", typeof( AzFunctionApp ) )]
    [InlineData( "functionapp,workflowapp", typeof( AzFunctionApp ) )]
    public void WebSite_KindDecidesTheClass( string kind, Type expected )
    {
        var resource = Mapper.Map( JsonDocument.Parse( SiteJson.Replace( "\"kind\": \"app,linux\"", $"\"kind\": \"{kind}\"" ) ).RootElement.Clone() );

        Assert.IsType( expected, resource );
    }


    /// <summary />
    /// <remarks>
    /// A Standard logic app is a function app as far as Azure is concerned, and
    /// says so only in its kind.
    /// </remarks>
    [Fact]
    public void FunctionApp_WorkflowAppIsRecognized()
    {
        var ordinary = Map<AzFunctionApp>( SiteJson.Replace( "\"kind\": \"app,linux\"", "\"kind\": \"functionapp,linux\"" ) );
        var workflow = Map<AzFunctionApp>( SiteJson.Replace( "\"kind\": \"app,linux\"", "\"kind\": \"functionapp,workflowapp\"" ) );

        Assert.False( ordinary.IsWorkflowApp );
        Assert.True( workflow.IsWorkflowApp );
    }


    /// <summary />
    [Fact]
    public void AppService_IsFullyMapped()
    {
        var site = Map<AzAppService>( SiteJson );

        Assert.Equal( "app,linux", site.Kind );
        Assert.Equal( "Running", site.State );
        Assert.True( site.Enabled );
        Assert.True( site.IsLinux );

        Assert.Equal( "app-one.azurewebsites.net", site.DefaultHostName );
        Assert.Equal( [ "app-one.azurewebsites.net", "www.example.org" ], site.HostNames );
        Assert.True( site.HttpsOnly );
        Assert.Equal( "1.2", site.MinTlsVersion );
        Assert.Equal( "Disabled", site.FtpsState );
        Assert.True( site.Http20Enabled );
        Assert.Equal( "Disabled", site.PublicNetworkAccess );

        Assert.EndsWith( "/serverfarms/plan-one", site.ServerFarmId );
        Assert.Equal( "DOTNETCORE|8.0", site.RuntimeStack );
        Assert.True( site.AlwaysOn );
        Assert.Equal( SubnetId, site.VirtualNetworkSubnetId );
        Assert.True( site.VnetRouteAllEnabled );
        Assert.Equal( "20.16.0.1,20.16.0.2", site.OutboundIpAddresses );

        Assert.False( site.ClientAffinityEnabled );
        Assert.Equal( 2, site.NumberOfWorkers );
        Assert.Equal( "None", site.RedundancyMode );
    }


    /// <summary />
    /// <remarks>
    /// A Windows site records its stack as a framework version rather than as
    /// the Linux-style image string, and only ever one of the two is set.
    /// </remarks>
    [Fact]
    public void AppService_WindowsRuntimeStackIsMapped()
    {
        var site = Map<AzAppService>( SiteJson
            .Replace( "\"linuxFxVersion\": \"DOTNETCORE|8.0\",", "\"netFrameworkVersion\": \"v8.0\"," )
            .Replace( "\"reserved\": true", "\"reserved\": false" ) );

        Assert.False( site.IsLinux );
        Assert.Equal( "v8.0", site.RuntimeStack );
    }


    /// <summary />
    [Fact]
    public void FunctionApp_IsFullyMapped()
    {
        var app = Map<AzFunctionApp>( FunctionAppJson );

        Assert.Equal( "functionapp,linux", app.Kind );
        Assert.False( app.IsWorkflowApp );
        Assert.Equal( 1536, app.ContainerSize );
        Assert.Equal( 0L, app.DailyMemoryTimeQuota );
        Assert.Equal( 40, app.FunctionAppScaleLimit );
        Assert.Equal( 1, app.MinimumElasticInstanceCount );
        Assert.Equal( "Python|3.12", app.RuntimeStack );
    }


    /// <summary />
    [Fact]
    public void WebSite_ReferencesAreResolved()
    {
        var site = Map<AzAppService>( SiteJson );
        var app = Map<AzFunctionApp>( FunctionAppJson );
        var endpoint = Map<AzPrivateEndpoint>( EndpointJson );
        var network = Network();

        Linker.Link( [ site, app, endpoint, network ] );

        Assert.Same( network.Subnets[ 0 ], site.Subnet );
        Assert.Same( network.Subnets[ 0 ], app.Subnet );
        Assert.Same( endpoint, Assert.Single( site.PrivateEndpoints ) );

        var json = JsonSerializer.Serialize<List<AzResource>>( [ site, app, endpoint, network ] );

        Assert.Contains( "AzFunctionApp", json );
    }


    /// <summary />
    /// <remarks>
    /// Application settings hold a site's connection strings and keys. They are
    /// not part of the resource Resource Graph returns, and are not read.
    /// </remarks>
    [Fact]
    public void WebSite_ApplicationSettingsAreNotMapped()
    {
        var app = Map<AzFunctionApp>( FunctionAppJson );

        var json = JsonSerializer.Serialize<AzResource>( app );

        Assert.DoesNotContain( "appSettings", json );
        Assert.DoesNotContain( "AccountKey", json );
    }


    /// <summary />
    private static AzVirtualNetwork Network()
    {
        var network = Activator.CreateInstance<AzVirtualNetwork>();

        network.Id = "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/virtualNetworks/vnet-one";
        network.Name = "vnet-one";
        network.Type = "Microsoft.Network/virtualNetworks";
        network.Location = "westeurope";
        network.AddressPrefixes = [ "10.0.0.0/16" ];
        network.DnsServers = [];
        network.Subnets =
        [
            new AzSubnet
            {
                Id = SubnetId,
                Name = "snet-app",
                Type = "Microsoft.Network/virtualNetworks/subnets",
                AddressPrefix = "10.0.1.0/24",
            },
        ];

        return network;
    }


    private const string MachineJson = """
        {
          "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Compute/virtualMachines/vm-one",
          "name": "vm-one",
          "type": "microsoft.compute/virtualmachines",
          "location": "westeurope",
          "properties": {
            "diagnosticsProfile": { "bootDiagnostics": { "enabled": true, "storageUri": "https://stdiag.blob.core.windows.net/" } },
            "extended": {
              "instanceView": {
                "computerName": "vmone",
                "hyperVGeneration": "V2",
                "osName": "Windows Server 2022 Datacenter Azure Edition",
                "osVersion": "10.0.20348.4297",
                "powerState": { "code": "PowerState/running", "displayStatus": "VM running", "level": "Info" }
              }
            },
            "hardwareProfile": { "vmSize": "Standard_D2s_v5" },
            "licenseType": "Windows_Server",
            "networkProfile": {
              "networkInterfaces": [
                {
                  "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/networkInterfaces/nic-one",
                  "properties": { "primary": true }
                }
              ]
            },
            "osProfile": {
              "adminUsername": "vmadmin",
              "allowExtensionOperations": true,
              "computerName": "vmone",
              "secrets": [],
              "windowsConfiguration": {
                "enableAutomaticUpdates": true,
                "patchSettings": { "assessmentMode": "AutomaticByPlatform", "enableHotpatching": false, "patchMode": "AutomaticByPlatform" },
                "provisionVMAgent": true
              }
            },
            "priority": "Regular",
            "provisioningState": "Succeeded",
            "securityProfile": {
              "encryptionAtHost": true,
              "securityType": "TrustedLaunch",
              "uefiSettings": { "secureBootEnabled": true, "vTpmEnabled": true }
            },
            "storageProfile": {
              "dataDisks": [
                {
                  "caching": "None",
                  "createOption": "Attach",
                  "deleteOption": "Detach",
                  "diskSizeGB": 512,
                  "lun": 1,
                  "managedDisk": {
                    "diskEncryptionSet": { "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Compute/diskEncryptionSets/des-one" },
                    "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Compute/disks/md_vm-one_data_1",
                    "storageAccountType": "Premium_LRS"
                  },
                  "name": "md_vm-one_data_1",
                  "toBeDetached": false,
                  "writeAcceleratorEnabled": false
                }
              ],
              "diskControllerType": "SCSI",
              "imageReference": { "exactVersion": "1.0.0", "id": "/subscriptions/other/resourceGroups/rg-images/providers/Microsoft.Compute/galleries/corp/images/win2022/versions/1.0.0" },
              "osDisk": {
                "caching": "ReadWrite",
                "createOption": "FromImage",
                "deleteOption": "Detach",
                "diskSizeGB": 128,
                "managedDisk": {
                  "diskEncryptionSet": { "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Compute/diskEncryptionSets/des-one" },
                  "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Compute/disks/md_vm-one_osdisk",
                  "storageAccountType": "Standard_LRS"
                },
                "name": "md_vm-one_osdisk",
                "osType": "Windows",
                "writeAcceleratorEnabled": false
              }
            },
            "timeCreated": "2025-04-03T14:48:45.716Z",
            "vmId": "fe024a1b-9f4e-4ed9-937c-2dab8b8ec3e5"
          }
        }
        """;

    private const string NicJson = """
        {
          "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/networkInterfaces/nic-one",
          "name": "nic-one",
          "type": "microsoft.network/networkinterfaces",
          "location": "westeurope",
          "properties": {
            "provisioningState": "Succeeded",
            "virtualMachine": { "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Compute/virtualMachines/vm-one" },
            "ipConfigurations": [
              {
                "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/networkInterfaces/nic-one/ipConfigurations/ipconfig1",
                "name": "ipconfig1",
                "properties": { "primary": true, "privateIPAddress": "10.0.1.4" }
              }
            ]
          }
        }
        """;

    private const string EncryptionSetJson = """
        {
          "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Compute/diskEncryptionSets/des-one",
          "name": "des-one",
          "type": "Microsoft.Compute/diskEncryptionSets",
          "location": "westeurope",
          "properties": { "encryptionType": "EncryptionAtRestWithCustomerKey", "provisioningState": "Succeeded" }
        }
        """;

    private const string SiteJson = """
        {
          "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Web/sites/app-one",
          "name": "app-one",
          "type": "microsoft.web/sites",
          "location": "westeurope",
          "kind": "app,linux",
          "properties": {
            "clientAffinityEnabled": false,
            "clientCertEnabled": false,
            "defaultHostName": "app-one.azurewebsites.net",
            "enabled": true,
            "hostNames": [ "app-one.azurewebsites.net", "www.example.org" ],
            "hostNamesDisabled": false,
            "httpsOnly": true,
            "outboundIpAddresses": "20.16.0.1,20.16.0.2",
            "privateEndpointConnections": [
              {
                "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Web/sites/app-one/privateEndpointConnections/one",
                "properties": { "privateEndpoint": { "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/privateEndpoints/pe-one" } }
              }
            ],
            "publicNetworkAccess": "Disabled",
            "redundancyMode": "None",
            "reserved": true,
            "serverFarmId": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Web/serverfarms/plan-one",
            "siteConfig": {
              "alwaysOn": true,
              "ftpsState": "Disabled",
              "http20Enabled": true,
              "linuxFxVersion": "DOTNETCORE|8.0",
              "minTlsVersion": "1.2",
              "numberOfWorkers": 2
            },
            "state": "Running",
            "virtualNetworkSubnetId": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/virtualNetworks/vnet-one/subnets/snet-app",
            "vnetRouteAllEnabled": true
          }
        }
        """;

    private const string FunctionAppJson = """
        {
          "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Web/sites/func-one",
          "name": "func-one",
          "type": "microsoft.web/sites",
          "location": "westeurope",
          "kind": "functionapp,linux",
          "properties": {
            "containerSize": 1536,
            "dailyMemoryTimeQuota": 0,
            "defaultHostName": "func-one.azurewebsites.net",
            "enabled": true,
            "hostNames": [ "func-one.azurewebsites.net" ],
            "httpsOnly": true,
            "reserved": true,
            "serverFarmId": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Web/serverfarms/plan-one",
            "siteConfig": {
              "alwaysOn": false,
              "ftpsState": "FtpsOnly",
              "functionAppScaleLimit": 40,
              "linuxFxVersion": "Python|3.12",
              "minTlsVersion": "1.2",
              "minimumElasticInstanceCount": 1
            },
            "state": "Running",
            "virtualNetworkSubnetId": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/virtualNetworks/vnet-one/subnets/snet-app",
            "vnetRouteAllEnabled": true
          }
        }
        """;

    private const string EndpointJson = """
        {
          "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/privateEndpoints/pe-one",
          "name": "pe-one",
          "type": "microsoft.network/privateendpoints",
          "location": "westeurope",
          "properties": { "provisioningState": "Succeeded" }
        }
        """;
}
