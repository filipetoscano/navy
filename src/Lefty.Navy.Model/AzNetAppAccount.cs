namespace Lefty.Navy.Model;

/// <summary />
/// <remarks>
/// The container for Azure NetApp Files in a region. It holds capacity pools,
/// which in turn hold the volumes, and carries the settings the volumes under
/// it inherit: which key encrypts them, and which directory authenticates the
/// clients which mount them.
/// </remarks>
public class AzNetAppAccount : AzResource
{
    /// <summary />
    public string? ProvisioningState { get; set; }

    /// <summary />
    /// <remarks>
    /// Enabled on an account which may join more than one Active Directory
    /// forest.
    /// </remarks>
    public string? MultiAdStatus { get; set; }

    /// <summary>
    /// Domain which NFSv4.1 maps user and group names through.
    /// </summary>
    public string? NfsV4IdDomain { get; set; }

    /// <summary />
    /// <remarks>
    /// True when clients may not list the exports the account offers.
    /// </remarks>
    public bool DisableShowmount { get; set; }


    /// <summary />
    /// <remarks>
    /// Microsoft.NetApp for a platform-managed key, Microsoft.KeyVault for a
    /// customer-managed one.
    /// </remarks>
    public string? EncryptionKeySource { get; set; }

    /// <summary>
    /// Vault holding the customer-managed key.
    /// </summary>
    /// <remarks>
    /// Null on an account encrypted with a platform-managed key, which is the
    /// default.
    /// </remarks>
    public string? EncryptionKeyVaultId { get; set; }

    /// <summary />
    public string? EncryptionKeyName { get; set; }

    /// <summary />
    public string? EncryptionKeyVaultUri { get; set; }

    /// <summary>
    /// Identity the account reaches the vault with.
    /// </summary>
    /// <remarks>
    /// Set only where a user-assigned identity was given; an account otherwise
    /// uses a system-assigned one, which is not a resource of its own.
    /// </remarks>
    public string? EncryptionIdentityId { get; set; }


    /// <summary>
    /// Directories the SMB and LDAP-enabled volumes authenticate against.
    /// </summary>
    /// <remarks>
    /// Ordinarily empty: an account serving only NFS without LDAP joins no
    /// directory at all.
    /// </remarks>
    public List<AzNetAppDirectory> ActiveDirectories { get; set; } = [];


    /// <summary>
    /// Capacity pools held by this account.
    /// </summary>
    /// <remarks>
    /// A pool is returned as a resource in its own right rather than as part of
    /// the account, and is attached here once everything has been read, the
    /// same way a SQL database is attached to its server. The volumes hang off
    /// the pool they were carved out of rather than off the account, so the
    /// three levels Azure reports flat are a hierarchy again here.
    /// </remarks>
    public List<AzNetAppCapacityPool> CapacityPools { get; set; } = [];


    /// <summary />
    public AzKeyVault? EncryptionKeyVault { get; set; }

    /// <summary />
    public AzManagedIdentity? EncryptionIdentity { get; set; }
}


/// <summary />
/// <remarks>
/// The password the account joins the directory with is never returned by
/// Azure, and the certificate it trusts is not mapped.
/// </remarks>
public class AzNetAppDirectory
{
    /// <summary />
    public string? ActiveDirectoryId { get; set; }

    /// <summary />
    public string? Domain { get; set; }

    /// <summary>
    /// Account used to join the directory.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Domain controllers to authenticate against, as addresses.
    /// </summary>
    /// <remarks>
    /// Reported as one comma-separated string rather than as a list.
    /// </remarks>
    public string? Dns { get; set; }

    /// <summary>
    /// Name the SMB server is known by within the directory.
    /// </summary>
    public string? SmbServerName { get; set; }

    /// <summary />
    public string? OrganizationalUnit { get; set; }

    /// <summary />
    /// <remarks>
    /// Created, InUse, Deleted, Error or Updating.
    /// </remarks>
    public string? Status { get; set; }


    /// <summary />
    /// <remarks>
    /// True when AES is used for the Kerberos tickets rather than RC4.
    /// </remarks>
    public bool AesEncryption { get; set; }

    /// <summary />
    /// <remarks>
    /// True when LDAP queries are signed, which keeps them from being tampered
    /// with in flight.
    /// </remarks>
    public bool LdapSigning { get; set; }

    /// <summary />
    /// <remarks>
    /// True when LDAP runs over TLS, which is what keeps the queries private
    /// rather than merely intact.
    /// </remarks>
    public bool LdapOverTls { get; set; }

    /// <summary />
    /// <remarks>
    /// True when the connection to the domain controllers is encrypted.
    /// </remarks>
    public bool EncryptDCConnections { get; set; }

    /// <summary />
    /// <remarks>
    /// True when a local NFS user may reach a volume which has LDAP turned on.
    /// </remarks>
    public bool AllowLocalNfsUsersWithLdap { get; set; }
}
