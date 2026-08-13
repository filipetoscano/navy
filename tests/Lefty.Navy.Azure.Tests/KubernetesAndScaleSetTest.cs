using Lefty.Navy.Azure;
using Lefty.Navy.Model;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace Lefty.Navy.Tests;

/// <summary />
public class KubernetesAndScaleSetTest
{
    private const string ClusterId = "/subscriptions/s/resourceGroups/rg/providers/Microsoft.ContainerService/managedClusters/aks-one";
    private const string EncryptionSetId = "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Compute/diskEncryptionSets/des-one";
    private const string GroupId = "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/networkSecurityGroups/nsg-one";
    private const string IdentityId = "/subscriptions/s/resourceGroups/rg-aks-nodes/providers/Microsoft.ManagedIdentity/userAssignedIdentities/aks-one-agentpool";
    private const string SubnetId = "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/virtualNetworks/vnet-one/subnets/snet-nodes";

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
    public void KubernetesService_IsFullyMapped()
    {
        var cluster = Map<AzKubernetesService>( ClusterJson );

        Assert.Equal( "Base", cluster.Sku );
        Assert.Equal( "Premium", cluster.SkuTier );
        Assert.Equal( "Succeeded", cluster.ProvisioningState );
        Assert.Equal( "Running", cluster.PowerState );

        Assert.Equal( "1.35.3", cluster.KubernetesVersion );
        Assert.Equal( "1.35.3", cluster.CurrentKubernetesVersion );
        Assert.Equal( "none", cluster.UpgradeChannel );
        Assert.Equal( "NodeImage", cluster.NodeOSUpgradeChannel );
        Assert.Equal( "KubernetesOfficial", cluster.SupportPlan );

        Assert.Equal( "rg-aks-nodes", cluster.NodeResourceGroup );
        Assert.True( cluster.EnableRbac );
        Assert.True( cluster.DisableLocalAccounts );
        Assert.True( cluster.EnableAzureRbac );
        Assert.True( cluster.WorkloadIdentityEnabled );
        Assert.True( cluster.OidcIssuerEnabled );
        Assert.True( cluster.DefenderEnabled );

        Assert.Equal( "Istio", cluster.ServiceMeshMode );
    }


    /// <summary />
    /// <remarks>
    /// Whether the API server is reachable from outside the network, and how
    /// the nodes reach the internet, are the two things worth knowing about a
    /// cluster from an inventory.
    /// </remarks>
    [Fact]
    public void KubernetesService_NetworkingIsMapped()
    {
        var cluster = Map<AzKubernetesService>( ClusterJson );

        Assert.True( cluster.EnablePrivateCluster );
        Assert.Null( cluster.Fqdn );
        Assert.Equal( "aks-one-abcd1234.privatelink.westeurope.azmk8s.io", cluster.PrivateFqdn );
        Assert.Empty( cluster.AuthorizedIPRanges );

        Assert.Equal( "azure", cluster.NetworkPlugin );
        Assert.Equal( "overlay", cluster.NetworkPluginMode );
        Assert.Equal( "calico", cluster.NetworkPolicy );
        Assert.Equal( "userDefinedRouting", cluster.OutboundType );
        Assert.Equal( "standard", cluster.LoadBalancerSku );
        Assert.Equal( [ "10.0.0.0/16" ], cluster.PodCidrs );
        Assert.Equal( [ "192.168.0.0/16" ], cluster.ServiceCidrs );
        Assert.Equal( "192.168.0.10", cluster.DnsServiceIP );
    }


    /// <summary />
    /// <remarks>
    /// Agent pools have no identifier of their own, so one is made from the
    /// cluster and the pool name.
    /// </remarks>
    [Fact]
    public void KubernetesService_NodePoolsAreMapped()
    {
        var cluster = Map<AzKubernetesService>( ClusterJson );

        Assert.Equal( 2, cluster.NodePools.Count );

        var system = cluster.NodePools[ 0 ];

        Assert.Equal( ClusterId + "/agentPools/systemnp", system.Id );
        Assert.Equal( "Microsoft.ContainerService/managedClusters/agentPools", system.Type );
        Assert.Equal( "System", system.Mode );
        Assert.Equal( 2, system.Count );
        Assert.Equal( "Standard_D2s_v4", system.VmSize );
        Assert.Equal( 250, system.MaxPods );
        Assert.False( system.EnableAutoScaling );
        Assert.True( system.EnableEncryptionAtHost );
        Assert.Equal( "AzureLinux", system.OsSku );
        Assert.Equal( 64, system.OsDiskSizeGB );
        Assert.Equal( "system", system.NodeLabels[ "workload" ] );
        Assert.Equal( [ "CriticalAddonsOnly=true:NoSchedule" ], system.NodeTaints );
        Assert.Equal( SubnetId, system.SubnetId );

        var user = cluster.NodePools[ 1 ];

        Assert.Equal( "User", user.Mode );
        Assert.True( user.EnableAutoScaling );
        Assert.Equal( 2, user.MinCount );
        Assert.Equal( 10, user.MaxCount );
        Assert.Equal( [ "1", "2", "3" ], user.AvailabilityZones );
    }


    /// <summary />
    /// <remarks>
    /// Azure reports every add-on it knows about, most of them turned off.
    /// </remarks>
    [Fact]
    public void KubernetesService_DisabledAddonsAreSkipped()
    {
        var cluster = Map<AzKubernetesService>( ClusterJson );

        var addon = Assert.Single( cluster.Addons );

        Assert.Equal( "azureKeyvaultSecretsProvider", addon.Name );
        Assert.EndsWith( "/userAssignedIdentities/azurekeyvaultsecretsprovider-aks-one", addon.IdentityResourceId );
    }


    /// <summary />
    [Fact]
    public void KubernetesService_ReferencesAreResolved()
    {
        var cluster = Map<AzKubernetesService>( ClusterJson );
        var encryptionSet = Map<AzDiskEncryptionSet>( EncryptionSetJson );
        var identity = Map<AzManagedIdentity>( IdentityJson );
        var network = Network();

        Linker.Link( [ cluster, encryptionSet, identity, network ] );

        Assert.Same( encryptionSet, cluster.DiskEncryptionSet );
        Assert.Same( identity, cluster.KubeletIdentity );
        Assert.Same( network.Subnets[ 0 ], cluster.NodePools[ 0 ].Subnet );

        var json = JsonSerializer.Serialize<List<AzResource>>( [ cluster, encryptionSet, identity, network ] );

        Assert.Contains( "aks-one", json );
    }


    /// <summary />
    /// <remarks>
    /// The kubelet identity is created by Azure in the node resource group,
    /// which is often in another subscription than the one being read.
    /// </remarks>
    [Fact]
    public void KubernetesService_WithoutItsIdentity_IsLeftNull()
    {
        var cluster = Map<AzKubernetesService>( ClusterJson );

        Linker.Link( [ cluster ] );

        Assert.Equal( IdentityId, cluster.KubeletIdentityId );
        Assert.Null( cluster.KubeletIdentity );
        Assert.Null( cluster.DiskEncryptionSet );
    }


    /// <summary />
    [Fact]
    public void ScaleSet_IsFullyMapped()
    {
        var set = Map<AzVirtualMachineScaleSet>( ScaleSetJson );

        Assert.Equal( "Standard_D8s_v4", set.Sku );
        Assert.Equal( "Standard", set.SkuTier );
        Assert.Equal( 2, set.SkuCapacity );

        Assert.Equal( "Uniform", set.OrchestrationMode );
        Assert.Equal( "Manual", set.UpgradeMode );
        Assert.Equal( "Succeeded", set.ProvisioningState );
        Assert.Equal( 2026, set.TimeCreated!.Value.Year );
        Assert.False( set.Overprovision );
        Assert.False( set.SinglePlacementGroup );
        Assert.Equal( 1, set.PlatformFaultDomainCount );

        Assert.Equal( "aks-stateful-26237244-vmss", set.ComputerNamePrefix );
        Assert.Equal( "azureuser", set.AdminUsername );
        Assert.True( set.DisablePasswordAuthentication );

        Assert.Equal( "Standard", set.SecurityType );
        Assert.True( set.EncryptionAtHost );

        Assert.Equal( "Linux", set.OsType );
        Assert.Equal( 128, set.OsDiskSizeGB );
        Assert.Equal( "ReadOnly", set.OsDiskCaching );
        Assert.Equal( "Premium_LRS", set.OsDiskStorageAccountType );
        Assert.Equal( EncryptionSetId, set.DiskEncryptionSetId );
    }


    /// <summary />
    /// <remarks>
    /// A scale set describes the interface each instance is given rather than
    /// interfaces which exist; the instance interfaces are not indexed.
    /// </remarks>
    [Fact]
    public void ScaleSet_NetworkProfileIsMapped()
    {
        var set = Map<AzVirtualMachineScaleSet>( ScaleSetJson );

        var nic = Assert.Single( set.NetworkInterfaces );

        Assert.Equal( "aks-stateful-26237244-vmss", nic.Name );
        Assert.True( nic.Primary );
        Assert.True( nic.EnableAcceleratedNetworking );
        Assert.True( nic.EnableIPForwarding );
        Assert.Equal( GroupId, nic.NetworkSecurityGroupId );

        var configuration = Assert.Single( nic.IPConfigurations );

        Assert.Equal( "ipconfig1", configuration.Name );
        Assert.True( configuration.Primary );
        Assert.Equal( SubnetId, configuration.SubnetId );
        Assert.Single( configuration.LoadBalancerBackendPoolIds );
    }


    /// <summary />
    [Fact]
    public void ScaleSet_ExtensionsAreMapped()
    {
        var set = Map<AzVirtualMachineScaleSet>( ScaleSetJson );

        var extension = Assert.Single( set.Extensions );

        Assert.Equal( "vmssCSE", extension.Name );
        Assert.Equal( "Microsoft.Azure.Extensions", extension.Publisher );
        Assert.Equal( "CustomScript", extension.ExtensionType );
        Assert.Equal( "2.0", extension.TypeHandlerVersion );
        Assert.True( extension.AutoUpgradeMinorVersion );
    }


    /// <summary />
    /// <remarks>
    /// An image from a gallery is named by identifier, one from the marketplace
    /// by four parts which mean nothing apart.
    /// </remarks>
    [Fact]
    public void ScaleSet_MarketplaceImageIsJoined()
    {
        var set = Map<AzVirtualMachineScaleSet>( ScaleSetJson.Replace(
            """{ "id": "/subscriptions/other/resourceGroups/AKS-Ubuntu/providers/Microsoft.Compute/galleries/AKSUbuntu/images/2204gen2/versions/1.0.0" }""",
            """{ "offer": "0001-com-ubuntu-server-jammy", "publisher": "Canonical", "sku": "22_04-lts-gen2", "version": "latest" }""" ) );

        Assert.Null( set.ImageReferenceId );
        Assert.Equal( "Canonical:0001-com-ubuntu-server-jammy:22_04-lts-gen2:latest", set.ImageReference );
    }


    /// <summary />
    [Fact]
    public void ScaleSet_ReferencesAreResolved()
    {
        var set = Map<AzVirtualMachineScaleSet>( ScaleSetJson );
        var encryptionSet = Map<AzDiskEncryptionSet>( EncryptionSetJson );
        var group = Map<AzNetworkSecurityGroup>( GroupJson );
        var network = Network();

        Linker.Link( [ set, encryptionSet, group, network ] );

        Assert.Same( encryptionSet, set.DiskEncryptionSet );
        Assert.Same( group, set.NetworkInterfaces[ 0 ].NetworkSecurityGroup );
        Assert.Same( network.Subnets[ 0 ], set.NetworkInterfaces[ 0 ].IPConfigurations[ 0 ].Subnet );

        var json = JsonSerializer.Serialize<List<AzResource>>( [ set, encryptionSet, group, network ] );

        Assert.Contains( "vmss-one", json );
    }


    /// <summary />
    /// <remarks>
    /// A backend pool names a balancer which many scale sets sit behind, so it
    /// stays an identifier rather than pulling the balancer in under each one.
    /// </remarks>
    [Fact]
    public void ScaleSet_BackendPoolsAreNotResolved()
    {
        var set = Map<AzVirtualMachineScaleSet>( ScaleSetJson );

        Linker.Link( [ set ] );

        var configuration = set.NetworkInterfaces[ 0 ].IPConfigurations[ 0 ];

        Assert.EndsWith( "/loadBalancers/kubernetes-internal/backendAddressPools/kubernetes", configuration.LoadBalancerBackendPoolIds[ 0 ] );

        var json = JsonSerializer.Serialize<AzResource>( set );

        Assert.DoesNotContain( "AzLoadBalancer", json );
    }


    /// <summary />
    private static AzVirtualNetwork Network()
    {
        var network = Activator.CreateInstance<AzVirtualNetwork>();

        network.Id = "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/virtualNetworks/vnet-one";
        network.Name = "vnet-one";
        network.Type = "Microsoft.Network/virtualNetworks";
        network.Location = "westeurope";
        network.AddressPrefixes = [ "10.200.0.0/16" ];
        network.DnsServers = [];
        network.Subnets =
        [
            new AzSubnet
            {
                Id = SubnetId,
                Name = "snet-nodes",
                Type = "Microsoft.Network/virtualNetworks/subnets",
                AddressPrefix = "10.200.1.0/24",
            },
        ];

        return network;
    }


    private const string ClusterJson = """
        {
          "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.ContainerService/managedClusters/aks-one",
          "name": "aks-one",
          "type": "microsoft.containerservice/managedclusters",
          "location": "westeurope",
          "kind": "Base",
          "sku": { "name": "Base", "tier": "Premium" },
          "properties": {
            "aadProfile": { "adminGroupObjectIDs": null, "enableAzureRBAC": true, "managed": true, "tenantID": "6c3cd73a-5fa3-4efb-bedc-f14f6a0ebf33" },
            "addonProfiles": {
              "azureKeyvaultSecretsProvider": {
                "config": { "enableSecretRotation": "true", "rotationPollInterval": "2m" },
                "enabled": true,
                "identity": {
                  "clientId": "fea0f4b4-e73d-4beb-8c9b-5b77b2e8169c",
                  "objectId": "b73a8bfb-734c-4769-8eea-4f1bd8335947",
                  "resourceId": "/subscriptions/s/resourcegroups/rg-aks-nodes/providers/Microsoft.ManagedIdentity/userAssignedIdentities/azurekeyvaultsecretsprovider-aks-one"
                }
              },
              "azurepolicy": { "config": null, "enabled": false },
              "openServiceMesh": { "config": null, "enabled": false }
            },
            "agentPoolProfiles": [
              {
                "count": 2,
                "currentOrchestratorVersion": "1.35.3",
                "enableAutoScaling": false,
                "enableEncryptionAtHost": true,
                "enableFIPS": false,
                "enableNodePublicIP": false,
                "maxPods": 250,
                "mode": "System",
                "name": "systemnp",
                "nodeImageVersion": "AKSAzureLinux-V3gen2-202606.08.1",
                "nodeLabels": { "workload": "system" },
                "nodeTaints": [ "CriticalAddonsOnly=true:NoSchedule" ],
                "orchestratorVersion": "1.35.3",
                "osDiskSizeGB": 64,
                "osDiskType": "Managed",
                "osSKU": "AzureLinux",
                "osType": "Linux",
                "powerState": { "code": "Running" },
                "provisioningState": "Succeeded",
                "type": "VirtualMachineScaleSets",
                "vmSize": "Standard_D2s_v4",
                "vnetSubnetID": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/virtualNetworks/vnet-one/subnets/snet-nodes"
              },
              {
                "availabilityZones": [ "1", "2", "3" ],
                "count": 4,
                "enableAutoScaling": true,
                "maxCount": 10,
                "maxPods": 250,
                "minCount": 2,
                "mode": "User",
                "name": "workload",
                "osSKU": "AzureLinux",
                "osType": "Linux",
                "powerState": { "code": "Running" },
                "provisioningState": "Succeeded",
                "type": "VirtualMachineScaleSets",
                "vmSize": "Standard_D8s_v4",
                "vnetSubnetID": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/virtualNetworks/vnet-one/subnets/snet-nodes"
              }
            ],
            "apiServerAccessProfile": { "enablePrivateCluster": true, "enablePrivateClusterPublicFQDN": false, "privateDNSZone": "system" },
            "autoScalerProfile": { "expander": "random", "scan-interval": "10s" },
            "autoUpgradeProfile": { "nodeOSUpgradeChannel": "NodeImage", "upgradeChannel": "none" },
            "currentKubernetesVersion": "1.35.3",
            "disableLocalAccounts": true,
            "diskEncryptionSetID": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Compute/diskEncryptionSets/des-one",
            "dnsPrefix": "aks-one",
            "enableRBAC": true,
            "identityProfile": {
              "kubeletidentity": {
                "clientId": "3a567295-2bde-43dd-b5c6-082db0b631f0",
                "objectId": "c0bde2db-d7a8-48cf-80e6-dbd353b532dc",
                "resourceId": "/subscriptions/s/resourceGroups/rg-aks-nodes/providers/Microsoft.ManagedIdentity/userAssignedIdentities/aks-one-agentpool"
              }
            },
            "kubernetesVersion": "1.35.3",
            "networkProfile": {
              "dnsServiceIP": "192.168.0.10",
              "loadBalancerSku": "standard",
              "networkDataplane": "azure",
              "networkPlugin": "azure",
              "networkPluginMode": "overlay",
              "networkPolicy": "calico",
              "outboundType": "userDefinedRouting",
              "podCidr": "10.0.0.0/16",
              "podCidrs": [ "10.0.0.0/16" ],
              "serviceCidr": "192.168.0.0/16",
              "serviceCidrs": [ "192.168.0.0/16" ]
            },
            "nodeResourceGroup": "rg-aks-nodes",
            "oidcIssuerProfile": { "enabled": true, "issuerURL": "https://westeurope.oic.prod-aks.azure.com/tenant/cluster/" },
            "powerState": { "code": "Running" },
            "privateFQDN": "aks-one-abcd1234.privatelink.westeurope.azmk8s.io",
            "provisioningState": "Succeeded",
            "securityProfile": {
              "defender": { "securityMonitoring": { "enabled": true } },
              "workloadIdentity": { "enabled": true }
            },
            "serviceMeshProfile": { "istio": { "revisions": [ "asm-1-29" ] }, "mode": "Istio" },
            "supportPlan": "KubernetesOfficial"
          }
        }
        """;

    private const string ScaleSetJson = """
        {
          "id": "/subscriptions/s/resourceGroups/rg-aks-nodes/providers/Microsoft.Compute/virtualMachineScaleSets/vmss-one",
          "name": "vmss-one",
          "type": "microsoft.compute/virtualmachinescalesets",
          "location": "westeurope",
          "sku": { "capacity": 2, "name": "Standard_D8s_v4", "tier": "Standard" },
          "properties": {
            "doNotRunExtensionsOnOverprovisionedVMs": false,
            "orchestrationMode": "Uniform",
            "overprovision": false,
            "platformFaultDomainCount": 1,
            "provisioningState": "Succeeded",
            "singlePlacementGroup": false,
            "timeCreated": "2026-04-12T11:48:18.4110386Z",
            "uniqueId": "4bc090cc-7d21-40bf-9691-b3236c28bc9b",
            "upgradePolicy": { "mode": "Manual" },
            "virtualMachineProfile": {
              "extensionProfile": {
                "extensions": [
                  {
                    "name": "vmssCSE",
                    "properties": {
                      "autoUpgradeMinorVersion": true,
                      "publisher": "Microsoft.Azure.Extensions",
                      "type": "CustomScript",
                      "typeHandlerVersion": "2.0"
                    }
                  }
                ],
                "extensionsTimeBudget": "PT16M"
              },
              "networkProfile": {
                "networkInterfaceConfigurations": [
                  {
                    "name": "aks-stateful-26237244-vmss",
                    "properties": {
                      "disableTcpStateTracking": false,
                      "dnsSettings": { "dnsServers": [] },
                      "enableAcceleratedNetworking": true,
                      "enableIPForwarding": true,
                      "ipConfigurations": [
                        {
                          "name": "ipconfig1",
                          "properties": {
                            "loadBalancerBackendAddressPools": [
                              { "id": "/subscriptions/s/resourceGroups/rg-aks-nodes/providers/Microsoft.Network/loadBalancers/kubernetes-internal/backendAddressPools/kubernetes" }
                            ],
                            "primary": true,
                            "privateIPAddressVersion": "IPv4",
                            "subnet": { "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/virtualNetworks/vnet-one/subnets/snet-nodes" }
                          }
                        }
                      ],
                      "networkSecurityGroup": { "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/networkSecurityGroups/nsg-one" },
                      "primary": true
                    }
                  }
                ]
              },
              "osProfile": {
                "adminUsername": "azureuser",
                "allowExtensionOperations": true,
                "computerNamePrefix": "aks-stateful-26237244-vmss",
                "linuxConfiguration": {
                  "disablePasswordAuthentication": true,
                  "provisionVMAgent": true,
                  "ssh": { "publicKeys": [ { "keyData": "ssh-rsa AAAAB3NzaC1yc2EAAAADAQABAAABgQ", "path": "/home/azureuser/.ssh/authorized_keys" } ] }
                },
                "secrets": []
              },
              "securityProfile": { "encryptionAtHost": true, "securityType": "Standard" },
              "storageProfile": {
                "diskControllerType": "SCSI",
                "imageReference": { "id": "/subscriptions/other/resourceGroups/AKS-Ubuntu/providers/Microsoft.Compute/galleries/AKSUbuntu/images/2204gen2/versions/1.0.0" },
                "osDisk": {
                  "caching": "ReadOnly",
                  "createOption": "FromImage",
                  "diskSizeGB": 128,
                  "managedDisk": {
                    "diskEncryptionSet": { "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Compute/diskEncryptionSets/des-one" },
                    "storageAccountType": "Premium_LRS"
                  },
                  "osType": "Linux"
                }
              }
            }
          }
        }
        """;

    private const string EncryptionSetJson = """
        {
          "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Compute/diskEncryptionSets/des-one",
          "name": "des-one",
          "type": "Microsoft.Compute/diskEncryptionSets",
          "location": "westeurope",
          "properties": {
            "activeKey": { "keyUrl": "https://kv-one.vault.azure.net/keys/cmk-disks/6f2c1b9a" },
            "encryptionType": "EncryptionAtRestWithCustomerKey",
            "provisioningState": "Succeeded"
          }
        }
        """;

    private const string IdentityJson = """
        {
          "id": "/subscriptions/s/resourceGroups/rg-aks-nodes/providers/Microsoft.ManagedIdentity/userAssignedIdentities/aks-one-agentpool",
          "name": "aks-one-agentpool",
          "type": "Microsoft.ManagedIdentity/userAssignedIdentities",
          "location": "westeurope",
          "properties": {
            "clientId": "3a567295-2bde-43dd-b5c6-082db0b631f0",
            "principalId": "c0bde2db-d7a8-48cf-80e6-dbd353b532dc",
            "tenantId": "6c3cd73a-5fa3-4efb-bedc-f14f6a0ebf33"
          }
        }
        """;

    private const string GroupJson = """
        {
          "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/networkSecurityGroups/nsg-one",
          "name": "nsg-one",
          "type": "Microsoft.Network/networkSecurityGroups",
          "location": "westeurope",
          "properties": { "provisioningState": "Succeeded" }
        }
        """;
}
