namespace Lefty.Navy.Model;

/// <summary />
/// <remarks>
/// Holds the customer-managed key with which the disks attached to it are
/// encrypted at rest.
/// </remarks>
public class AzDiskEncryptionSet : AzResource
{
    /// <summary />
    /// <remarks>
    /// EncryptionAtRestWithCustomerKey,
    /// EncryptionAtRestWithPlatformAndCustomerKeys or
    /// ConfidentialVmEncryptedWithCustomerKey.
    /// </remarks>
    public string? EncryptionType { get; set; }

    /// <summary />
    public string? ProvisioningState { get; set; }


    /// <summary>
    /// Versioned URL of the key the set is currently encrypting with.
    /// </summary>
    public string? KeyUrl { get; set; }

    /// <summary>
    /// Vault which holds the key.
    /// </summary>
    /// <remarks>
    /// Absent when the key lives in a managed HSM rather than in a vault, in
    /// which case <see cref="KeyUrl" /> is the only record of where it is.
    /// </remarks>
    public string? KeyVaultId { get; set; }

    /// <summary />
    /// <remarks>
    /// True when the set moves to the latest version of the key by itself,
    /// rather than staying pinned to the version named by
    /// <see cref="KeyUrl" />.
    /// </remarks>
    public bool RotationToLatestKeyVersionEnabled { get; set; }

    /// <summary />
    public DateTimeOffset? LastKeyRotationTimestamp { get; set; }

    /// <summary>
    /// Multi-tenant application used to reach a vault in another tenant.
    /// </summary>
    /// <remarks>
    /// Reported as None when the vault is in the same tenant, which is the
    /// ordinary case.
    /// </remarks>
    public string? FederatedClientId { get; set; }


    /// <summary />
    public AzKeyVault? KeyVault { get; set; }
}
