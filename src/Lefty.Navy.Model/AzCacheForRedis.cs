namespace Lefty.Navy.Model;

/// <summary />
/// <remarks>
/// The access keys are deliberately not mapped, and neither is the whole of the
/// Redis configuration: where a cache backs itself up, the configuration holds
/// the storage connection string it writes with, key and all. Only the settings
/// named below are taken from it.
/// </remarks>
public class AzCacheForRedis : AzResource
{
    /// <summary />
    /// <remarks>
    /// Basic, Standard, Premium, Enterprise or EnterpriseFlash. Reported inside
    /// the properties rather than in the sku column every other type uses.
    /// </remarks>
    public string? Sku { get; set; }

    /// <summary />
    /// <remarks>
    /// C for the Basic and Standard families, P for Premium.
    /// </remarks>
    public string? SkuFamily { get; set; }

    /// <summary>
    /// Size within the family, which is what fixes the memory and the bandwidth.
    /// </summary>
    public int SkuCapacity { get; set; }

    /// <summary />
    public string? ProvisioningState { get; set; }

    /// <summary />
    public string? RedisVersion { get; set; }

    /// <summary />
    /// <remarks>
    /// Stable or Preview: which wave of updates the cache is on.
    /// </remarks>
    public string? UpdateChannel { get; set; }


    /// <summary />
    public string? HostName { get; set; }

    /// <summary />
    /// <remarks>
    /// The plain port, 6379. Only reachable when
    /// <see cref="EnableNonSslPort" /> is set.
    /// </remarks>
    public int Port { get; set; }

    /// <summary />
    public int SslPort { get; set; }

    /// <summary />
    /// <remarks>
    /// True when the cache also answers unencrypted, which is worth noticing.
    /// </remarks>
    public bool EnableNonSslPort { get; set; }

    /// <summary />
    public string? MinimumTlsVersion { get; set; }

    /// <summary />
    /// <remarks>
    /// Enabled or Disabled. Disabled leaves the cache reachable only through a
    /// private endpoint.
    /// </remarks>
    public string? PublicNetworkAccess { get; set; }

    /// <summary />
    /// <remarks>
    /// True when the shared access keys are refused and callers have to hold an
    /// Entra token.
    /// </remarks>
    public bool DisableAccessKeyAuthentication { get; set; }

    /// <summary />
    /// <remarks>
    /// True when Entra authentication is turned on at all, which is the
    /// prerequisite for refusing the keys.
    /// </remarks>
    public bool AadEnabled { get; set; }


    /// <summary>
    /// Replicas kept behind each primary.
    /// </summary>
    /// <remarks>
    /// Zero on a Basic cache, which has no replica and therefore no
    /// availability guarantee.
    /// </remarks>
    public int ReplicasPerPrimary { get; set; }

    /// <summary>
    /// Shards the keyspace is split across.
    /// </summary>
    /// <remarks>
    /// Zero on a cache which is not clustered.
    /// </remarks>
    public int ShardCount { get; set; }

    /// <summary />
    /// <remarks>
    /// Automatic or NoZones, on the caches which report it.
    /// </remarks>
    public string? ZonalAllocationPolicy { get; set; }


    /// <summary />
    /// <remarks>
    /// volatile-lru, allkeys-lru, noeviction and so on: what the cache throws
    /// away when it fills up. noeviction means writes start failing instead.
    /// </remarks>
    public string? MaxMemoryPolicy { get; set; }

    /// <summary>
    /// Memory held back for non-cache overhead, in MB.
    /// </summary>
    public int MaxMemoryReservedMB { get; set; }

    /// <summary>
    /// Memory held back for fragmentation, in MB.
    /// </summary>
    public int MaxFragmentationMemoryReservedMB { get; set; }

    /// <summary />
    public int MaxClients { get; set; }

    /// <summary />
    /// <remarks>
    /// True when the cache is snapshotted to storage. Which account it is
    /// written to is not mapped: the connection string Azure reports alongside
    /// it carries the account key.
    /// </remarks>
    public bool RdbBackupEnabled { get; set; }

    /// <summary />
    /// <remarks>
    /// True when every write is appended to storage, the same caveat applying.
    /// </remarks>
    public bool AofBackupEnabled { get; set; }


    /// <summary>
    /// Subnet the cache is injected into.
    /// </summary>
    /// <remarks>
    /// Only ever set on an older Premium cache placed directly in a virtual
    /// network; the arrangement Azure recommends now is a private endpoint.
    /// </remarks>
    public string? SubnetId { get; set; }

    /// <summary />
    /// <remarks>
    /// Set alongside <see cref="SubnetId" /> only.
    /// </remarks>
    public string? StaticIP { get; set; }

    /// <summary>
    /// Caches this one is geo-replicated with.
    /// </summary>
    /// <remarks>
    /// Deliberately left as identifiers. Geo-replication is described from both
    /// ends, so resolving them would close a loop between the two caches.
    /// </remarks>
    public List<string> LinkedServerIds { get; set; } = [];

    /// <summary />
    public List<string> PrivateEndpointIds { get; set; } = [];


    /// <summary />
    public AzSubnet? Subnet { get; set; }

    /// <summary />
    public List<AzPrivateEndpoint> PrivateEndpoints { get; set; } = [];
}
