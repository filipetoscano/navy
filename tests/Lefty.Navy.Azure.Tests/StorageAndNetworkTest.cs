using Lefty.Navy.Azure;
using Lefty.Navy.Model;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace Lefty.Navy.Tests;

/// <summary />
/// <remarks>
/// The rows reproduce what Resource Graph returns, including that sku and kind
/// are columns of their own rather than members of properties.
/// </remarks>
public class StorageAndNetworkTest
{
    private const string AccountId = "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Storage/storageAccounts/stone";
    private const string EndpointId = "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/privateEndpoints/pe-stone";
    private const string NicId = "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/networkInterfaces/pe-stone.nic.abc";
    private const string SubnetId = "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/virtualNetworks/vnet-one/subnets/snet-pep";
    private const string NsgId = "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/networkSecurityGroups/nsg-one";

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
    public void StorageAccount_IsFullyMapped()
    {
        var account = Map<AzStorageAccount>( AccountJson );

        Assert.Equal( "StorageV2", account.Kind );
        Assert.Equal( "Standard_ZRS", account.Sku );
        Assert.Equal( "Standard", account.SkuTier );
        Assert.Equal( "Hot", account.AccessTier );

        Assert.True( account.SupportsHttpsTrafficOnly );
        Assert.Equal( "TLS1_2", account.MinimumTlsVersion );
        Assert.False( account.AllowBlobPublicAccess );
        Assert.False( account.AllowSharedKeyAccess );
        Assert.Equal( "Disabled", account.PublicNetworkAccess );
        Assert.True( account.IsHnsEnabled );

        Assert.Equal( "Microsoft.Keyvault", account.EncryptionKeySource );
        Assert.True( account.RequireInfrastructureEncryption );
        Assert.Equal( "https://kv-cmk.vault.azure.net", account.EncryptionKeyVaultUri );
        Assert.Equal( "storage-cmk", account.EncryptionKeyName );

        Assert.Equal( "Deny", account.NetworkAclsDefaultAction );
        Assert.Equal( "AzureServices", account.NetworkAclsBypass );
        Assert.Equal( [ "203.0.113.10" ], account.NetworkAclsIpRules );
        Assert.Equal( [ SubnetId ], account.NetworkAclsVirtualNetworkRules );

        Assert.Equal( "https://stone.blob.core.windows.net/", account.PrimaryEndpoints[ "blob" ] );
        Assert.Equal( [ EndpointId ], account.PrivateEndpointIds );
    }


    /// <summary />
    /// <remarks>
    /// Container contents come from the management plane, not Resource Graph,
    /// so a freshly mapped account has none.
    /// </remarks>
    [Fact]
    public void StorageAccount_ChildCollectionsStartEmpty()
    {
        var account = Map<AzStorageAccount>( AccountJson );

        Assert.Empty( account.BlobContainers );
        Assert.Empty( account.FileShares );
        Assert.Empty( account.Queues );
        Assert.Empty( account.Tables );
    }


    /// <summary />
    [Fact]
    public void NetworkInterface_IsFullyMapped()
    {
        var nic = Map<AzNetworkInterface>( NicJson );

        Assert.Equal( "00-11-22-33-44-55", nic.MacAddress );
        Assert.False( nic.EnableAcceleratedNetworking );
        Assert.False( nic.EnableIPForwarding );
        Assert.Equal( NsgId, nic.NetworkSecurityGroupId );
        Assert.Equal( EndpointId, nic.PrivateEndpointId );

        var configuration = Assert.Single( nic.IPConfigurations );

        Assert.Equal( "10.200.7.244", configuration.PrivateIPAddress );
        Assert.Equal( "Dynamic", configuration.PrivateIPAllocationMethod );
        Assert.True( configuration.Primary );
        Assert.Equal( SubnetId, configuration.SubnetId );
        Assert.Equal( "Microsoft.Network/networkInterfaces/ipConfigurations", configuration.Type );
    }


    /// <summary />
    [Fact]
    public void PrivateEndpoint_IsFullyMapped()
    {
        var endpoint = Map<AzPrivateEndpoint>( EndpointJson );

        Assert.Equal( SubnetId, endpoint.SubnetId );
        Assert.Equal( [ NicId ], endpoint.NetworkInterfaceIds );

        var connection = Assert.Single( endpoint.Connections );

        Assert.Equal( AccountId, connection.PrivateLinkServiceId );
        Assert.Equal( [ "blob" ], connection.GroupIds );
        Assert.Equal( "Approved", connection.Status );
        Assert.Equal( "Auto-Approved", connection.StatusDescription );
        Assert.False( connection.IsManual );

        var dns = Assert.Single( endpoint.CustomDnsConfigs );

        Assert.Equal( "stone.blob.core.windows.net", dns.Fqdn );
        Assert.Equal( [ "10.200.7.244" ], dns.IPAddresses );
    }


    /// <summary />
    /// <remarks>
    /// A connection awaiting approval by the owner of the target arrives in a
    /// separate collection, and is only distinguishable by which one it was in.
    /// </remarks>
    [Fact]
    public void PrivateEndpoint_ManualConnection_IsMarked()
    {
        var endpoint = Map<AzPrivateEndpoint>( """
            {
              "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/privateEndpoints/pe-two",
              "name": "pe-two",
              "type": "Microsoft.Network/privateEndpoints",
              "location": "westeurope",
              "properties": {
                "manualPrivateLinkServiceConnections": [
                  {
                    "name": "psc-manual",
                    "properties": {
                      "privateLinkServiceId": "/subscriptions/other/providers/Microsoft.Storage/storageAccounts/stfar",
                      "groupIds": [ "blob" ],
                      "privateLinkServiceConnectionState": { "status": "Pending", "description": "Awaiting approval" }
                    }
                  }
                ]
              }
            }
            """ );

        var connection = Assert.Single( endpoint.Connections );

        Assert.True( connection.IsManual );
        Assert.Equal( "Pending", connection.Status );
    }


    /// <summary />
    [Fact]
    public void Linker_ResolvesTheWholeChain()
    {
        var account = Map<AzStorageAccount>( AccountJson );
        var endpoint = Map<AzPrivateEndpoint>( EndpointJson );
        var nic = Map<AzNetworkInterface>( NicJson );
        var nsg = Resource<AzNetworkSecurityGroup>( NsgId );
        var network = Network();

        Linker.Link( [ account, endpoint, nic, nsg, network ] );

        // account -> endpoint -> nic -> subnet -> nsg
        Assert.Same( endpoint, Assert.Single( account.PrivateEndpoints ) );
        Assert.Same( nic, Assert.Single( endpoint.NetworkInterfaces ) );
        Assert.Same( network.Subnets[ 0 ], endpoint.Subnet );
        Assert.Same( network.Subnets[ 0 ], nic.IPConfigurations[ 0 ].Subnet );
        Assert.Same( nsg, nic.NetworkSecurityGroup );
    }


    /// <summary />
    /// <remarks>
    /// The guarantee the inline serialization depends on: neither the interface
    /// nor the connection resolves back to what points at it.
    /// </remarks>
    [Fact]
    public void Linker_DoesNotCloseTheLoop()
    {
        var account = Map<AzStorageAccount>( AccountJson );
        var endpoint = Map<AzPrivateEndpoint>( EndpointJson );
        var nic = Map<AzNetworkInterface>( NicJson );

        Linker.Link( [ account, endpoint, nic, Network() ] );

        Assert.Equal( EndpointId, nic.PrivateEndpointId );
        Assert.Equal( AccountId, endpoint.Connections[ 0 ].PrivateLinkServiceId );

        // The whole graph must serialize without running away.
        var json = JsonSerializer.Serialize<List<AzResource>>( [ account, endpoint, nic ] );

        Assert.Contains( "pe-stone", json );
        Assert.Contains( "AzStorageAccount", json );
    }


    /// <summary />
    private static AzVirtualNetwork Network()
    {
        var network = Resource<AzVirtualNetwork>( "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/virtualNetworks/vnet-one" );

        network.AddressPrefixes = [ "10.200.0.0/16" ];
        network.DnsServers = [];
        network.Subnets =
        [
            new AzSubnet
            {
                Id = SubnetId,
                Name = "snet-pep",
                Type = "Microsoft.Network/virtualNetworks/subnets",
                AddressPrefix = "10.200.7.0/24",
            },
        ];

        return network;
    }


    /// <summary />
    private static T Resource<T>( string id )
        where T : AzResource
    {
        var resource = Activator.CreateInstance<T>();

        resource.Id = id;
        resource.Name = id.Split( '/' ).Last();
        resource.Type = "unspecified";
        resource.Location = "westeurope";

        return resource;
    }


    private const string AccountJson = """
        {
          "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Storage/storageAccounts/stone",
          "name": "stone",
          "type": "Microsoft.Storage/storageAccounts",
          "location": "westeurope",
          "kind": "StorageV2",
          "sku": { "name": "Standard_ZRS", "tier": "Standard" },
          "properties": {
            "accessTier": "Hot",
            "primaryLocation": "westeurope",
            "statusOfPrimary": "available",
            "supportsHttpsTrafficOnly": true,
            "minimumTlsVersion": "TLS1_2",
            "allowBlobPublicAccess": false,
            "allowSharedKeyAccess": false,
            "publicNetworkAccess": "Disabled",
            "isHnsEnabled": true,
            "encryption": {
              "keySource": "Microsoft.Keyvault",
              "requireInfrastructureEncryption": true,
              "keyvaultproperties": { "keyvaulturi": "https://kv-cmk.vault.azure.net", "keyname": "storage-cmk" }
            },
            "networkAcls": {
              "bypass": "AzureServices",
              "defaultAction": "Deny",
              "ipRules": [ { "value": "203.0.113.10", "action": "Allow" } ],
              "virtualNetworkRules": [ { "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/virtualNetworks/vnet-one/subnets/snet-pep", "action": "Allow" } ]
            },
            "primaryEndpoints": { "blob": "https://stone.blob.core.windows.net/", "dfs": "https://stone.dfs.core.windows.net/" },
            "privateEndpointConnections": [
              {
                "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Storage/storageAccounts/stone/privateEndpointConnections/one",
                "properties": {
                  "privateEndpoint": { "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/privateEndpoints/pe-stone" },
                  "privateLinkServiceConnectionState": { "status": "Approved" }
                }
              }
            ]
          }
        }
        """;

    private const string NicJson = """
        {
          "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/networkInterfaces/pe-stone.nic.abc",
          "name": "pe-stone.nic.abc",
          "type": "Microsoft.Network/networkInterfaces",
          "location": "westeurope",
          "properties": {
            "macAddress": "00-11-22-33-44-55",
            "enableAcceleratedNetworking": false,
            "enableIPForwarding": false,
            "nicType": "Standard",
            "dnsSettings": { "dnsServers": [], "appliedDnsServers": [] },
            "networkSecurityGroup": { "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/networkSecurityGroups/nsg-one" },
            "privateEndpoint": { "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/privateEndpoints/pe-stone" },
            "ipConfigurations": [
              {
                "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/networkInterfaces/pe-stone.nic.abc/ipConfigurations/one",
                "name": "privateEndpointIpConfig",
                "properties": {
                  "privateIPAddress": "10.200.7.244",
                  "privateIPAllocationMethod": "Dynamic",
                  "privateIPAddressVersion": "IPv4",
                  "primary": true,
                  "subnet": { "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/virtualNetworks/vnet-one/subnets/snet-pep" }
                }
              }
            ]
          }
        }
        """;

    private const string EndpointJson = """
        {
          "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/privateEndpoints/pe-stone",
          "name": "pe-stone",
          "type": "Microsoft.Network/privateEndpoints",
          "location": "westeurope",
          "properties": {
            "provisioningState": "Succeeded",
            "customNetworkInterfaceName": "",
            "subnet": { "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/virtualNetworks/vnet-one/subnets/snet-pep" },
            "networkInterfaces": [ { "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/networkInterfaces/pe-stone.nic.abc" } ],
            "privateLinkServiceConnections": [
              {
                "name": "psc-stone-blob-0",
                "properties": {
                  "privateLinkServiceId": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Storage/storageAccounts/stone",
                  "groupIds": [ "blob" ],
                  "privateLinkServiceConnectionState": { "status": "Approved", "description": "Auto-Approved" }
                }
              }
            ],
            "customDnsConfigs": [ { "fqdn": "stone.blob.core.windows.net", "ipAddresses": [ "10.200.7.244" ] } ]
          }
        }
        """;
}
