namespace Lefty.Navy.Model;

/// <summary />
/// <remarks>
/// The child collections are not returned by Resource Graph, which indexes the
/// account but none of its containers, shares, queues or tables. They are read
/// separately, from the management plane, and are empty when the caller is not
/// permitted to list them.
/// </remarks>
public class AzStorageAccount : AzResource
{
    /// <summary />
    public string? Kind { get; set; }

    /// <summary />
    public string? Sku { get; set; }

    /// <summary />
    public string? SkuTier { get; set; }

    /// <summary />
    public string? AccessTier { get; set; }

    /// <summary />
    public string? PrimaryLocation { get; set; }

    /// <summary />
    public string? StatusOfPrimary { get; set; }

    /// <summary />
    public string? ProvisioningState { get; set; }


    /// <summary />
    public bool SupportsHttpsTrafficOnly { get; set; }

    /// <summary />
    public string? MinimumTlsVersion { get; set; }

    /// <summary />
    public bool AllowBlobPublicAccess { get; set; }

    /// <summary />
    public bool AllowSharedKeyAccess { get; set; }

    /// <summary />
    public bool AllowCrossTenantReplication { get; set; }

    /// <summary />
    public bool DefaultToOAuthAuthentication { get; set; }

    /// <summary />
    public string? PublicNetworkAccess { get; set; }

    /// <summary />
    public string? DnsEndpointType { get; set; }


    /// <summary />
    /// <remarks>
    /// Hierarchical namespace, which is what makes the account a Data Lake
    /// Storage Gen2 account.
    /// </remarks>
    public bool IsHnsEnabled { get; set; }

    /// <summary />
    public bool IsSftpEnabled { get; set; }

    /// <summary />
    public bool IsNfsV3Enabled { get; set; }

    /// <summary />
    public bool IsLocalUserEnabled { get; set; }


    /// <summary />
    /// <remarks>
    /// Microsoft.Storage for a platform managed key, or Microsoft.Keyvault for
    /// a customer managed one.
    /// </remarks>
    public string? EncryptionKeySource { get; set; }

    /// <summary />
    public bool RequireInfrastructureEncryption { get; set; }

    /// <summary />
    /// <remarks>
    /// Vault holding the customer managed key. A vault address rather than a
    /// resource identifier, so it does not resolve to an <see cref="AzKeyVault" />.
    /// </remarks>
    public string? EncryptionKeyVaultUri { get; set; }

    /// <summary />
    public string? EncryptionKeyName { get; set; }


    /// <summary />
    public string? NetworkAclsDefaultAction { get; set; }

    /// <summary />
    public string? NetworkAclsBypass { get; set; }

    /// <summary />
    public List<string> NetworkAclsIpRules { get; set; } = [];

    /// <summary />
    /// <remarks>
    /// Subnets permitted through the account firewall.
    /// </remarks>
    public List<string> NetworkAclsVirtualNetworkRules { get; set; } = [];


    /// <summary />
    public Dictionary<string, string> PrimaryEndpoints { get; set; } = [];

    /// <summary />
    public List<string> PrivateEndpointIds { get; set; } = [];


    /// <summary />
    public List<AzBlobContainer> BlobContainers { get; set; } = [];

    /// <summary />
    public List<AzFileShare> FileShares { get; set; } = [];

    /// <summary />
    public List<AzStorageQueue> Queues { get; set; } = [];

    /// <summary />
    public List<AzStorageTable> Tables { get; set; } = [];


    /// <summary />
    public List<AzPrivateEndpoint> PrivateEndpoints { get; set; } = [];
}


/// <summary />
public class AzBlobContainer : AzChildResource
{
    /// <summary />
    /// <remarks>
    /// None, Blob or Container. Anything other than None exposes the contents
    /// anonymously, subject to the account also allowing public access.
    /// </remarks>
    public string? PublicAccess { get; set; }

    /// <summary />
    public bool HasImmutabilityPolicy { get; set; }

    /// <summary />
    public bool HasLegalHold { get; set; }

    /// <summary />
    public bool IsVersioningEnabled { get; set; }

    /// <summary />
    public string? DefaultEncryptionScope { get; set; }

    /// <summary />
    public bool PreventEncryptionScopeOverride { get; set; }

    /// <summary />
    public string? LeaseState { get; set; }

    /// <summary />
    public string? LeaseStatus { get; set; }

    /// <summary />
    public DateTimeOffset? LastModifiedOn { get; set; }
}


/// <summary />
public class AzFileShare : AzChildResource
{
    /// <summary />
    public int? QuotaInGB { get; set; }

    /// <summary />
    /// <remarks>
    /// SMB or NFS.
    /// </remarks>
    public string? EnabledProtocol { get; set; }

    /// <summary />
    public string? AccessTier { get; set; }

    /// <summary />
    public string? LeaseState { get; set; }

    /// <summary />
    public DateTimeOffset? LastModifiedOn { get; set; }
}


/// <summary />
public class AzStorageQueue : AzChildResource
{
    /// <summary />
    public Dictionary<string, string> Metadata { get; set; } = [];
}


/// <summary />
public class AzStorageTable : AzChildResource
{
    // TODO
}
