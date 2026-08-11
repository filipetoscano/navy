using Lefty.Navy.Azure;
using Lefty.Navy.Model;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace Lefty.Navy.Tests;

/// <summary />
/// <remarks>
/// Rows are shaped exactly as Azure Resource Graph returns them from the
/// <c>resources</c> table, including its habit of omitting absent fields rather
/// than returning them as null.
/// </remarks>
public class ResourceMapperTest
{
    private static readonly ResourceMapper Mapper = new( NullLogger.Instance );


    /// <summary />
    private static JsonElement Row( string json )
    {
        return JsonDocument.Parse( json ).RootElement.Clone();
    }


    /// <summary />
    [Fact]
    public void KeyVault_IsFullyMapped()
    {
        var row = Row( """
            {
              "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.KeyVault/vaults/kv-one",
              "name": "kv-one",
              "type": "Microsoft.KeyVault/vaults",
              "location": "westeurope",
              "tags": { "env": "prod" },
              "properties": {
                "sku": { "family": "A", "name": "premium" },
                "enabledForDiskEncryption": true,
                "enableSoftDelete": true,
                "softDeleteRetentionInDays": 90,
                "enableRbacAuthorization": true,
                "enablePurgeProtection": true
              }
            }
            """ );

        var vault = Assert.IsType<AzKeyVault>( Mapper.Map( row ) );

        Assert.Equal( "kv-one", vault.Name );
        Assert.Equal( "westeurope", vault.Location );
        Assert.Equal( "prod", vault.Tags[ "env" ] );
        Assert.Equal( "premium", vault.Sku );
        Assert.True( vault.EnabledForDiskEncryption );
        Assert.True( vault.EnableSoftDelete );
        Assert.Equal( 90, vault.SoftDeleteRetentionInDays );
        Assert.True( vault.EnableRbacAuthorization );
        Assert.True( vault.EnablePurgeProtection );
    }


    /// <summary />
    [Fact]
    public void KeyVault_WithPrivateEndpoint_CapturesId()
    {
        var row = Row( """
            {
              "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.KeyVault/vaults/kv-two",
              "name": "kv-two",
              "type": "Microsoft.KeyVault/vaults",
              "location": "westeurope",
              "properties": {
                "sku": { "name": "standard" },
                "privateEndpointConnections": [
                  {
                    "id": "/subscriptions/s/.../connections/one",
                    "properties": {
                      "privateEndpoint": { "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/privateEndpoints/pe-one" }
                    }
                  }
                ]
              }
            }
            """ );

        var vault = Assert.IsType<AzKeyVault>( Mapper.Map( row ) );

        Assert.Equal( "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/privateEndpoints/pe-one", vault.PrivateEndpointId );
    }


    /// <summary />
    /// <remarks>
    /// The overwhelmingly common case: no private endpoint, and no tags. Both
    /// keys are absent from the row rather than null.
    /// </remarks>
    [Fact]
    public void KeyVault_WithoutPrivateEndpoint_LeavesIdNull()
    {
        var row = Row( """
            {
              "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.KeyVault/vaults/kv-three",
              "name": "kv-three",
              "type": "Microsoft.KeyVault/vaults",
              "location": "westeurope",
              "properties": { "sku": { "name": "standard" } }
            }
            """ );

        var vault = Assert.IsType<AzKeyVault>( Mapper.Map( row ) );

        Assert.Null( vault.PrivateEndpointId );
        Assert.Empty( vault.Tags );
        Assert.False( vault.EnablePurgeProtection );
    }


    /// <summary />
    [Fact]
    public void VirtualNetwork_IsFullyMapped()
    {
        var network = Assert.IsType<AzVirtualNetwork>( Mapper.Map( Row( VirtualNetworkJson ) ) );

        Assert.Equal( [ "10.0.0.0/16" ], network.AddressPrefixes );
        Assert.Equal( [ "10.0.0.4" ], network.DnsServers );
        Assert.Equal( 2, network.Subnets.Count );

        var guarded = network.Subnets[ 0 ];

        Assert.Equal( "snet-guarded", guarded.Name );
        Assert.Equal( "10.0.1.0/24", guarded.AddressPrefix );
        Assert.Equal( "Microsoft.Network/virtualNetworks/subnets", guarded.Type );
        Assert.Equal( "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/networkSecurityGroups/nsg-one", guarded.NetworkSecurityGroupId );
        Assert.Equal( "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/routeTables/rt-one", guarded.RouteTableId );
    }


    /// <summary />
    /// <remarks>
    /// A subnet with neither a security group nor a route table is ordinary,
    /// which is why those identifiers are nullable on the model.
    /// </remarks>
    [Fact]
    public void VirtualNetwork_BareSubnet_HasNoReferences()
    {
        var network = Assert.IsType<AzVirtualNetwork>( Mapper.Map( Row( VirtualNetworkJson ) ) );

        var bare = network.Subnets[ 1 ];

        Assert.Equal( "snet-bare", bare.Name );
        Assert.Null( bare.NetworkSecurityGroupId );
        Assert.Null( bare.RouteTableId );
    }


    /// <summary />
    /// <remarks>
    /// A virtual network with no DNS servers configured omits dhcpOptions
    /// entirely; the model requires a list, so it must come back empty.
    /// </remarks>
    [Fact]
    public void VirtualNetwork_WithoutDhcpOptions_HasEmptyDnsServers()
    {
        var row = Row( """
            {
              "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/virtualNetworks/vnet-bare",
              "name": "vnet-bare",
              "type": "Microsoft.Network/virtualNetworks",
              "location": "westeurope",
              "properties": { "addressSpace": { "addressPrefixes": [ "10.1.0.0/16" ] } }
            }
            """ );

        var network = Assert.IsType<AzVirtualNetwork>( Mapper.Map( row ) );

        Assert.Empty( network.DnsServers );
        Assert.Empty( network.Subnets );
    }


    /// <summary />
    /// <remarks>
    /// A declared type must be mapped onto its own class, which is what allows
    /// references to it to resolve, and must survive a row which reports no
    /// properties at all.
    /// </remarks>
    [Theory]
    [InlineData( "Microsoft.Network/networkSecurityGroups", typeof( AzNetworkSecurityGroup ) )]
    [InlineData( "Microsoft.Network/routeTables", typeof( AzRouteTable ) )]
    [InlineData( "Microsoft.Network/privateEndpoints", typeof( AzPrivateEndpoint ) )]
    [InlineData( "Microsoft.Storage/storageAccounts", typeof( AzStorageAccount ) )]
    [InlineData( "Microsoft.Sql/servers", typeof( AzSqlServer ) )]
    [InlineData( "microsoft.sql/servers/databases", typeof( AzSqlDatabase ) )]
    [InlineData( "MICROSOFT.INSIGHTS/COMPONENTS", typeof( AzApplicationInsights ) )]
    [InlineData( "Microsoft.Compute/diskEncryptionSets", typeof( AzDiskEncryptionSet ) )]
    [InlineData( "Microsoft.ManagedIdentity/userAssignedIdentities", typeof( AzManagedIdentity ) )]
    [InlineData( "Microsoft.Insights/actionGroups", typeof( AzActionGroup ) )]
    [InlineData( "Microsoft.Insights/activityLogAlerts", typeof( AzActivityLogAlertRule ) )]
    [InlineData( "Microsoft.Insights/metricAlerts", typeof( AzMetricAlertRule ) )]
    [InlineData( "Microsoft.AlertsManagement/smartDetectorAlertRules", typeof( AzSmartDetectorAlertRule ) )]
    [InlineData( "Microsoft.ContainerService/managedClusters", typeof( AzKubernetesService ) )]
    [InlineData( "Microsoft.Compute/virtualMachineScaleSets", typeof( AzVirtualMachineScaleSet ) )]
    [InlineData( "Microsoft.Network/loadBalancers", typeof( AzLoadBalancer ) )]
    [InlineData( "Microsoft.NetApp/netAppAccounts/capacityPools/volumes", typeof( AzVolume ) )]
    public void DeclaredType_MapsToItsOwnClass( string type, Type expected )
    {
        var row = Row( $$"""
            {
              "id": "/subscriptions/s/resourceGroups/rg/providers/{{type}}/thing",
              "name": "thing",
              "type": "{{type}}",
              "location": "westeurope"
            }
            """ );

        var resource = Mapper.Map( row );

        Assert.IsType( expected, resource );
        Assert.Equal( "thing", resource.Name );
    }


    /// <summary />
    [Fact]
    public void UnknownType_FallsBackToBase()
    {
        var row = Row( """
            {
              "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Web/sites/app-one",
              "name": "app-one",
              "type": "Microsoft.Web/sites",
              "location": "westeurope",
              "properties": { "state": "Running" }
            }
            """ );

        var resource = Mapper.Map( row );

        Assert.Equal( typeof( AzResource ), resource.GetType() );
        Assert.Equal( "app-one", resource.Name );
        Assert.Equal( "Microsoft.Web/sites", resource.Type );
    }


    private const string VirtualNetworkJson = """
        {
          "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/virtualNetworks/vnet-one",
          "name": "vnet-one",
          "type": "Microsoft.Network/virtualNetworks",
          "location": "westeurope",
          "properties": {
            "addressSpace": { "addressPrefixes": [ "10.0.0.0/16" ] },
            "dhcpOptions": { "dnsServers": [ "10.0.0.4" ] },
            "subnets": [
              {
                "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/virtualNetworks/vnet-one/subnets/snet-guarded",
                "name": "snet-guarded",
                "properties": {
                  "addressPrefix": "10.0.1.0/24",
                  "networkSecurityGroup": { "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/networkSecurityGroups/nsg-one" },
                  "routeTable": { "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/routeTables/rt-one" }
                }
              },
              {
                "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/virtualNetworks/vnet-one/subnets/snet-bare",
                "name": "snet-bare",
                "properties": { "addressPrefix": "10.0.2.0/24" }
              }
            ]
          }
        }
        """;
}
