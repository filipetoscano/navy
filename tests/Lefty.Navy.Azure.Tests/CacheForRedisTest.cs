using Lefty.Navy.Azure;
using Lefty.Navy.Model;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace Lefty.Navy.Tests;

/// <summary />
public class CacheForRedisTest
{
    private const string EndpointId = "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/privateEndpoints/pe-redis-one";
    private const string SubnetId = "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/virtualNetworks/vnet-one/subnets/snet-cache";

    private static readonly ResourceMapper Mapper = new( NullLogger<ResourceMapper>.Instance );
    private static readonly ResourceLinker Linker = new( NullLogger<ResourceLinker>.Instance );


    /// <summary />
    private static T Map<T>( string json )
        where T : AzResource
    {
        return Assert.IsType<T>( Mapper.Map( JsonDocument.Parse( json ).RootElement.Clone() ) );
    }


    /// <summary />
    /// <remarks>
    /// The sku is inside the properties, and the sku column of the resources
    /// table is null: the one type which reports it this way.
    /// </remarks>
    [Fact]
    public void CacheForRedis_SkuComesFromTheProperties()
    {
        var cache = Map<AzCacheForRedis>( CacheJson );

        Assert.Equal( "Premium", cache.Sku );
        Assert.Equal( "P", cache.SkuFamily );
        Assert.Equal( 1, cache.SkuCapacity );
    }


    /// <summary />
    [Fact]
    public void CacheForRedis_IsFullyMapped()
    {
        var cache = Map<AzCacheForRedis>( CacheJson );

        Assert.Equal( "Succeeded", cache.ProvisioningState );
        Assert.Equal( "6.0", cache.RedisVersion );
        Assert.Equal( "Stable", cache.UpdateChannel );

        Assert.Equal( "redis-one.redis.cache.windows.net", cache.HostName );
        Assert.Equal( 6379, cache.Port );
        Assert.Equal( 6380, cache.SslPort );
        Assert.False( cache.EnableNonSslPort );
        Assert.Equal( "1.2", cache.MinimumTlsVersion );
        Assert.Equal( "Disabled", cache.PublicNetworkAccess );
        Assert.True( cache.DisableAccessKeyAuthentication );

        Assert.Equal( 1, cache.ReplicasPerPrimary );
        Assert.Equal( 0, cache.ShardCount );
        Assert.Equal( "Automatic", cache.ZonalAllocationPolicy );
    }


    /// <summary />
    /// <remarks>
    /// Every value in the Redis configuration is a string, whether it is a
    /// number or a flag.
    /// </remarks>
    [Fact]
    public void CacheForRedis_ConfigurationStringsAreParsed()
    {
        var cache = Map<AzCacheForRedis>( CacheJson );

        Assert.Equal( "volatile-lru", cache.MaxMemoryPolicy );
        Assert.Equal( 642, cache.MaxMemoryReservedMB );
        Assert.Equal( 642, cache.MaxFragmentationMemoryReservedMB );
        Assert.Equal( 7500, cache.MaxClients );
        Assert.True( cache.AadEnabled );
        Assert.False( cache.RdbBackupEnabled );
        Assert.False( cache.AofBackupEnabled );
    }


    /// <summary />
    /// <remarks>
    /// Where a cache backs itself up, the configuration carries the storage
    /// connection string it writes with, account key and all. Nothing from the
    /// configuration is kept except the settings which are asked for by name.
    /// </remarks>
    [Fact]
    public void CacheForRedis_BackupConnectionStringIsNotMapped()
    {
        var cache = Map<AzCacheForRedis>( BackedUpCacheJson );

        Assert.True( cache.RdbBackupEnabled );

        var json = JsonSerializer.Serialize<AzResource>( cache );

        Assert.DoesNotContain( "AccountKey", json );
        Assert.DoesNotContain( "rdb-storage-connection-string", json );
    }


    /// <summary />
    /// <remarks>
    /// The access keys are null in the row whether or not the caller could read
    /// them, and are not modelled either way.
    /// </remarks>
    [Fact]
    public void CacheForRedis_AccessKeysAreNotMapped()
    {
        var cache = Map<AzCacheForRedis>( CacheJson );

        var json = JsonSerializer.Serialize<AzResource>( cache );

        Assert.DoesNotContain( "accessKeys", json );
        Assert.DoesNotContain( "AccessKeys", json );
    }


    /// <summary />
    /// <remarks>
    /// An Enterprise cache reports the older name for the same number.
    /// </remarks>
    [Fact]
    public void CacheForRedis_ReplicasPerMasterIsAccepted()
    {
        var cache = Map<AzCacheForRedis>( CacheJson
            .Replace( "\"replicasPerPrimary\": 1,", "" )
            .Replace( "\"replicasPerMaster\": 1", "\"replicasPerMaster\": 2" ) );

        Assert.Equal( 2, cache.ReplicasPerPrimary );
    }


    /// <summary />
    [Fact]
    public void CacheForRedis_PrivateEndpointIsResolved()
    {
        var cache = Map<AzCacheForRedis>( CacheJson );
        var endpoint = Map<AzPrivateEndpoint>( EndpointJson );

        Linker.Link( [ cache, endpoint ] );

        Assert.Same( endpoint, Assert.Single( cache.PrivateEndpoints ) );
        Assert.Null( cache.Subnet );
    }


    /// <summary />
    /// <remarks>
    /// An older Premium cache sits directly in a virtual network rather than
    /// behind a private endpoint.
    /// </remarks>
    [Fact]
    public void CacheForRedis_InjectedIntoASubnet_IsResolved()
    {
        var cache = Map<AzCacheForRedis>( InjectedCacheJson );
        var network = Network();

        Linker.Link( [ cache, network ] );

        Assert.Same( network.Subnets[ 0 ], cache.Subnet );
        Assert.Equal( "10.0.2.10", cache.StaticIP );
    }


    /// <summary />
    /// <remarks>
    /// Geo-replication is described from both ends, so resolving the linked
    /// caches would close a loop between the pair.
    /// </remarks>
    [Fact]
    public void CacheForRedis_LinkedServersAreNotResolved()
    {
        var cache = Map<AzCacheForRedis>( BackedUpCacheJson );
        var other = Map<AzCacheForRedis>( CacheJson );

        Linker.Link( [ cache, other ] );

        Assert.EndsWith( "/linkedServers/redis-one", Assert.Single( cache.LinkedServerIds ) );

        var json = JsonSerializer.Serialize<List<AzResource>>( [ cache, other ] );

        Assert.Contains( "redis-two", json );
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
                Name = "snet-cache",
                Type = "Microsoft.Network/virtualNetworks/subnets",
                AddressPrefix = "10.0.2.0/24",
            },
        ];

        return network;
    }


    private const string CacheJson = """
        {
          "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Cache/Redis/redis-one",
          "name": "redis-one",
          "type": "microsoft.cache/redis",
          "location": "westeurope",
          "sku": null,
          "properties": {
            "accessKeys": null,
            "disableAccessKeyAuthentication": true,
            "enableNonSslPort": false,
            "hostName": "redis-one.redis.cache.windows.net",
            "instances": [
              { "isMaster": false, "isPrimary": false, "shardId": 0, "sslPort": 15000 },
              { "isMaster": true, "isPrimary": true, "shardId": 0, "sslPort": 15001 }
            ],
            "linkedServers": [],
            "minimumTlsVersion": "1.2",
            "port": 6379,
            "privateEndpointConnections": [
              {
                "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Cache/Redis/redis-one/privateEndpointConnections/one",
                "properties": {
                  "privateEndpoint": { "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/privateEndpoints/pe-redis-one" },
                  "privateLinkServiceConnectionState": { "actionsRequired": "None", "description": "Auto-Approved", "status": "Approved" }
                }
              }
            ],
            "provisioningState": "Succeeded",
            "publicNetworkAccess": "Disabled",
            "redisConfiguration": {
              "aad-enabled": "true",
              "maxclients": "7500",
              "maxfragmentationmemory-reserved": "642",
              "maxmemory-delta": "642",
              "maxmemory-policy": "volatile-lru",
              "maxmemory-reserved": "642",
              "rdb-backup-enabled": "false"
            },
            "redisVersion": "6.0",
            "replicasPerMaster": 1,
            "replicasPerPrimary": 1,
            "sku": { "capacity": 1, "family": "P", "name": "Premium" },
            "sslPort": 6380,
            "updateChannel": "Stable",
            "zonalAllocationPolicy": "Automatic"
          }
        }
        """;

    private const string BackedUpCacheJson = """
        {
          "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Cache/Redis/redis-two",
          "name": "redis-two",
          "type": "microsoft.cache/redis",
          "location": "westeurope",
          "sku": null,
          "properties": {
            "enableNonSslPort": false,
            "hostName": "redis-two.redis.cache.windows.net",
            "linkedServers": [
              { "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Cache/Redis/redis-two/linkedServers/redis-one" }
            ],
            "minimumTlsVersion": "1.2",
            "port": 6379,
            "provisioningState": "Succeeded",
            "publicNetworkAccess": "Enabled",
            "redisConfiguration": {
              "aof-backup-enabled": "false",
              "maxmemory-policy": "allkeys-lru",
              "rdb-backup-enabled": "true",
              "rdb-backup-frequency": "60",
              "rdb-storage-connection-string": "DefaultEndpointsProtocol=https;AccountName=stbackup;AccountKey=Zm9vYmFyYmF6cXV4MTIzNDU2Nzg5MA==;EndpointSuffix=core.windows.net"
            },
            "redisVersion": "6.0",
            "replicasPerPrimary": 1,
            "sku": { "capacity": 3, "family": "P", "name": "Premium" },
            "sslPort": 6380
          }
        }
        """;

    private const string InjectedCacheJson = """
        {
          "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Cache/Redis/redis-three",
          "name": "redis-three",
          "type": "microsoft.cache/redis",
          "location": "westeurope",
          "sku": null,
          "properties": {
            "enableNonSslPort": true,
            "hostName": "redis-three.redis.cache.windows.net",
            "minimumTlsVersion": "1.0",
            "port": 6379,
            "provisioningState": "Succeeded",
            "redisConfiguration": { "maxmemory-policy": "noeviction" },
            "redisVersion": "4.0",
            "sku": { "capacity": 1, "family": "P", "name": "Premium" },
            "sslPort": 6380,
            "staticIP": "10.0.2.10",
            "subnetId": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/virtualNetworks/vnet-one/subnets/snet-cache"
          }
        }
        """;

    private const string EndpointJson = """
        {
          "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/privateEndpoints/pe-redis-one",
          "name": "pe-redis-one",
          "type": "microsoft.network/privateendpoints",
          "location": "westeurope",
          "properties": { "provisioningState": "Succeeded" }
        }
        """;
}
