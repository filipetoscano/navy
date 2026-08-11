using Lefty.Navy.Azure;
using Lefty.Navy.Model;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace Lefty.Navy.Tests;

/// <summary />
/// <remarks>
/// The rows below reproduce what Resource Graph returns for an API Management
/// service, including that sku is a column of its own rather than a member of
/// properties, and that it drops the null members of sku entirely.
/// </remarks>
public class ApiManagementTest
{
    private const string ServiceId = "/subscriptions/s/resourceGroups/rg/providers/Microsoft.ApiManagement/service/apim-one";
    private const string SubnetId = "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/virtualNetworks/vnet-one/subnets/snet-apim";

    private static readonly ResourceMapper Mapper = new( NullLogger.Instance );
    private static readonly ResourceLinker Linker = new( NullLogger.Instance );


    /// <summary />
    private static AzApiManagement Map( string json )
    {
        return Assert.IsType<AzApiManagement>( Mapper.Map( JsonDocument.Parse( json ).RootElement.Clone() ) );
    }


    /// <summary />
    [Fact]
    public void Service_IsFullyMapped()
    {
        var service = Map( ServiceJson );

        Assert.Equal( "Developer", service.Sku );
        Assert.Equal( 1, service.SkuCapacity );
        Assert.Equal( "Publisher Name", service.PublisherName );
        Assert.Equal( "admin@example.com", service.PublisherEmail );
        Assert.Equal( "stv2.1", service.PlatformVersion );

        Assert.Equal( "https://apim-one.azure-api.net", service.GatewayUrl );
        Assert.Equal( "https://apim-one.developer.azure-api.net", service.DeveloperPortalUrl );
        Assert.Equal( "https://apim-one.management.azure-api.net", service.ManagementApiUrl );
        Assert.Equal( "https://apim-one.scm.azure-api.net", service.ScmUrl );

        Assert.Equal( "Internal", service.VirtualNetworkType );
        Assert.Equal( "Enabled", service.PublicNetworkAccess );
        Assert.Equal( SubnetId, service.SubnetId );
        Assert.Equal( [ "20.21.114.41" ], service.PublicIPAddresses );
        Assert.Equal( [ "10.200.0.4" ], service.PrivateIPAddresses );
    }


    /// <summary />
    /// <remarks>
    /// The built-in hostname is always present; the custom domains are what the
    /// portal shows under that name.
    /// </remarks>
    [Fact]
    public void Service_HostnamesAreMapped()
    {
        var service = Map( ServiceJson );

        Assert.Equal( 3, service.HostnameConfigurations.Count );

        var builtin = service.HostnameConfigurations[ 0 ];

        Assert.Equal( "apim-one.azure-api.net", builtin.Name );
        Assert.Equal( "BuiltIn", builtin.CertificateSource );
        Assert.Equal( "Proxy", builtin.HostnameType );
        Assert.True( builtin.DefaultSslBinding );
        Assert.False( builtin.NegotiateClientCertificate );
        Assert.Null( builtin.KeyVaultId );

        Assert.Equal( ServiceId + "/hostnameConfigurations/apim-one.azure-api.net", builtin.Id );
        Assert.Equal( "Microsoft.ApiManagement/service/hostnameConfigurations", builtin.Type );

        Assert.Equal( [ "api.example.com", "eap.example.com" ],
            service.HostnameConfigurations.Where( x => x.CertificateSource != "BuiltIn" ).Select( x => x.Name ) );
    }


    /// <summary />
    [Fact]
    public void Service_KeyVaultBackedHostname_CapturesSecretIdentifier()
    {
        var service = Map( ServiceJson );

        var custom = service.HostnameConfigurations[ 2 ];

        Assert.Equal( "eap.example.com", custom.Name );
        Assert.Equal( "KeyVault", custom.CertificateSource );
        Assert.Equal( "Completed", custom.CertificateStatus );
        Assert.Equal( "https://kv-one.vault.azure.net/secrets/wildcard", custom.KeyVaultId );
        Assert.True( custom.NegotiateClientCertificate );
    }


    /// <summary />
    /// <remarks>
    /// A service which is not injected into a virtual network has neither a
    /// subnet nor private addresses, and omits those keys altogether.
    /// </remarks>
    [Fact]
    public void Service_NotInjected_HasNoSubnetOrPrivateAddresses()
    {
        var service = Map( """
            {
              "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.ApiManagement/service/apim-two",
              "name": "apim-two",
              "type": "Microsoft.ApiManagement/service",
              "location": "westeurope",
              "sku": { "capacity": 1, "name": "Consumption" },
              "properties": { "virtualNetworkType": "None", "publisherName": "Contoso" }
            }
            """ );

        Assert.Equal( "Consumption", service.Sku );
        Assert.Null( service.SubnetId );
        Assert.Empty( service.PrivateIPAddresses );
        Assert.Empty( service.PublicIPAddresses );
        Assert.Empty( service.HostnameConfigurations );
    }


    /// <summary />
    [Fact]
    public void Service_SubnetIsResolved()
    {
        var service = Map( ServiceJson );

        var network = Basic<AzVirtualNetwork>( "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/virtualNetworks/vnet-one" );

        network.AddressPrefixes = [ "10.200.0.0/16" ];
        network.DnsServers = [];
        network.Subnets =
        [
            new AzSubnet
            {
                Id = SubnetId,
                Name = "snet-apim",
                Type = "Microsoft.Network/virtualNetworks/subnets",
                AddressPrefix = "10.200.0.0/24",
            },
        ];

        Linker.Link( [ service, network ] );

        Assert.NotNull( service.Subnet );
        Assert.Same( network.Subnets[ 0 ], service.Subnet );
        Assert.Equal( "snet-apim", service.Subnet.Name );
    }


    /// <summary />
    /// <remarks>
    /// The network the service is injected into commonly lives in another
    /// subscription, which was not read.
    /// </remarks>
    [Fact]
    public void Service_UnresolvableSubnet_IsLeftNull()
    {
        var service = Map( ServiceJson );

        Linker.Link( [ service ] );

        Assert.Equal( SubnetId, service.SubnetId );
        Assert.Null( service.Subnet );
    }


    /// <summary />
    private static T Basic<T>( string id )
        where T : AzResource
    {
        var resource = Activator.CreateInstance<T>();

        resource.Id = id;
        resource.Name = id.Split( '/' ).Last();
        resource.Type = "Microsoft.Network/virtualNetworks";
        resource.Location = "westeurope";

        return resource;
    }


    private const string ServiceJson = """
        {
          "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.ApiManagement/service/apim-one",
          "name": "apim-one",
          "type": "Microsoft.ApiManagement/service",
          "location": "westeurope",
          "tags": { "env": "nonprod" },
          "sku": { "capacity": 1, "name": "Developer" },
          "properties": {
            "publisherEmail": "admin@example.com",
            "publisherName": "Publisher Name",
            "platformVersion": "stv2.1",
            "gatewayUrl": "https://apim-one.azure-api.net",
            "developerPortalUrl": "https://apim-one.developer.azure-api.net",
            "managementApiUrl": "https://apim-one.management.azure-api.net",
            "scmUrl": "https://apim-one.scm.azure-api.net",
            "virtualNetworkType": "Internal",
            "publicNetworkAccess": "Enabled",
            "publicIPAddresses": [ "20.21.114.41" ],
            "privateIPAddresses": [ "10.200.0.4" ],
            "virtualNetworkConfiguration": {
              "subnetResourceId": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/virtualNetworks/vnet-one/subnets/snet-apim"
            },
            "hostnameConfigurations": [
              {
                "type": "Proxy",
                "hostName": "apim-one.azure-api.net",
                "certificateSource": "BuiltIn",
                "defaultSslBinding": true,
                "negotiateClientCertificate": false
              },
              {
                "type": "Proxy",
                "hostName": "api.example.com",
                "certificateSource": "Custom",
                "defaultSslBinding": false,
                "negotiateClientCertificate": false
              },
              {
                "type": "Proxy",
                "hostName": "eap.example.com",
                "certificateSource": "KeyVault",
                "certificateStatus": "Completed",
                "keyVaultId": "https://kv-one.vault.azure.net/secrets/wildcard",
                "defaultSslBinding": false,
                "negotiateClientCertificate": true
              }
            ]
          }
        }
        """;
}
