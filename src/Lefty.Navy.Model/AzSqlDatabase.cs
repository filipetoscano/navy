namespace Lefty.Navy.Model;

/// <summary />
/// <remarks>
/// Every server also carries a master database, which is returned alongside the
/// user databases and is told apart by its kind.
/// </remarks>
public class AzSqlDatabase : AzResource
{
    /// <summary>
    /// Server which hosts the database.
    /// </summary>
    /// <remarks>
    /// Deliberately left as an identifier. The server holds its databases, so
    /// resolving this would close a loop and make the inline serialization
    /// non-terminating.
    /// </remarks>
    public string? ServerId { get; set; }

    /// <summary />
    /// <remarks>
    /// Ends in ,system for the master database and ,user for the rest.
    /// </remarks>
    public string? Kind { get; set; }

    /// <summary />
    public string? DatabaseId { get; set; }

    /// <summary />
    public string? Status { get; set; }


    /// <summary />
    public string? Sku { get; set; }

    /// <summary />
    public string? SkuTier { get; set; }

    /// <summary />
    public int SkuCapacity { get; set; }

    /// <summary />
    public string? CurrentServiceObjectiveName { get; set; }

    /// <summary />
    public string? RequestedServiceObjectiveName { get; set; }

    /// <summary />
    public string? ElasticPoolId { get; set; }

    /// <summary />
    public string? LicenseType { get; set; }


    /// <summary />
    public long MaxSizeBytes { get; set; }

    /// <summary />
    public string? Collation { get; set; }

    /// <summary />
    public string? CatalogCollation { get; set; }


    /// <summary />
    public bool ZoneRedundant { get; set; }

    /// <summary />
    public string? AvailabilityZone { get; set; }

    /// <summary />
    /// <remarks>
    /// Enabled when the database serves read-only workloads from a secondary
    /// replica.
    /// </remarks>
    public string? ReadScale { get; set; }

    /// <summary />
    /// <remarks>
    /// Local, Zone or Geo.
    /// </remarks>
    public string? RequestedBackupStorageRedundancy { get; set; }

    /// <summary />
    public string? CurrentBackupStorageRedundancy { get; set; }


    /// <summary />
    public bool IsLedgerOn { get; set; }

    /// <summary />
    public bool IsInfraEncryptionEnabled { get; set; }

    /// <summary />
    public DateTimeOffset? CreationDate { get; set; }

    /// <summary />
    public DateTimeOffset? EarliestRestoreDate { get; set; }
}
