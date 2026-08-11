namespace Lefty.Navy.Model;

/// <summary />
/// <remarks>
/// An Azure NetApp Files volume. Volumes are grandchildren of a NetApp account,
/// held by a capacity pool, and Resource Graph reports the whole path in the
/// name: account/pool/volume.
/// </remarks>
public class AzNetAppVolume : AzResource
{
    /// <summary>
    /// Capacity pool the volume is carved out of.
    /// </summary>
    /// <remarks>
    /// Taken from the identifier, which is the only place the relationship is
    /// stated. Deliberately left as one: the pool holds its volumes, and
    /// resolving this would close a loop.
    /// </remarks>
    public string? CapacityPoolId { get; set; }

    /// <summary>
    /// NetApp account the capacity pool belongs to.
    /// </summary>
    /// <remarks>
    /// Also taken from the identifier, and left as one for the same reason: the
    /// account is reached through the pool rather than from here.
    /// </remarks>
    public string? NetAppAccountId { get; set; }

    /// <summary />
    public string? ProvisioningState { get; set; }

    /// <summary>
    /// Name of the export path, which is what a client mounts.
    /// </summary>
    public string? CreationToken { get; set; }

    /// <summary />
    public string? FileSystemId { get; set; }


    /// <summary />
    /// <remarks>
    /// Standard, Premium or Ultra. Fixes the throughput a given size is given.
    /// </remarks>
    public string? ServiceLevel { get; set; }

    /// <summary>
    /// Provisioned size, in bytes.
    /// </summary>
    /// <remarks>
    /// Azure calls this the usage threshold, and it is a hard limit rather than
    /// a point at which anything is merely reported.
    /// </remarks>
    public long UsageThreshold { get; set; }

    /// <summary />
    public double ThroughputMibps { get; set; }

    /// <summary />
    public long MaximumNumberOfFiles { get; set; }

    /// <summary />
    /// <remarks>
    /// True when infrequently read blocks are moved to cheaper storage.
    /// </remarks>
    public bool CoolAccess { get; set; }


    /// <summary />
    /// <remarks>
    /// NFSv3, NFSv4.1 and CIFS, in any combination.
    /// </remarks>
    public List<string> ProtocolTypes { get; set; } = [];

    /// <summary />
    /// <remarks>
    /// Unix or Ntfs.
    /// </remarks>
    public string? SecurityStyle { get; set; }

    /// <summary />
    /// <remarks>
    /// The Unix mode of the export root, as four octal digits.
    /// </remarks>
    public string? UnixPermissions { get; set; }

    /// <summary />
    public bool KerberosEnabled { get; set; }

    /// <summary />
    public bool LdapEnabled { get; set; }

    /// <summary />
    /// <remarks>
    /// True when the snapshot directory is visible to clients, which lets them
    /// restore a file without help.
    /// </remarks>
    public bool SnapshotDirectoryVisible { get; set; }

    /// <summary />
    /// <remarks>
    /// Microsoft.NetApp for a platform-managed key, Microsoft.KeyVault for a
    /// customer-managed one.
    /// </remarks>
    public string? EncryptionKeySource { get; set; }


    /// <summary>
    /// Subnet the volume is delegated into.
    /// </summary>
    public string? SubnetId { get; set; }

    /// <summary />
    /// <remarks>
    /// Basic or Standard, which decides whether the volume is subject to
    /// network security groups and user-defined routes.
    /// </remarks>
    public string? NetworkFeatures { get; set; }

    /// <summary>
    /// Addresses clients mount the volume at.
    /// </summary>
    public List<string> MountTargetIPAddresses { get; set; } = [];

    /// <summary>
    /// Who may mount the volume, and how.
    /// </summary>
    public List<AzNetAppVolumeExportRule> ExportRules { get; set; } = [];


    /// <summary />
    public AzSubnet? Subnet { get; set; }
}


/// <summary />
/// <remarks>
/// Export rules are numbered rather than named, and the lowest index which
/// matches a client decides its access.
/// </remarks>
public class AzNetAppVolumeExportRule
{
    /// <summary />
    public int RuleIndex { get; set; }

    /// <summary>
    /// Clients the rule applies to, as addresses or CIDR ranges.
    /// </summary>
    /// <remarks>
    /// Reported as one comma-separated string rather than as a list. A value of
    /// 0.0.0.0/0 is every client which can reach the subnet.
    /// </remarks>
    public string? AllowedClients { get; set; }

    /// <summary />
    public bool Nfsv3 { get; set; }

    /// <summary />
    public bool Nfsv41 { get; set; }

    /// <summary />
    public bool Cifs { get; set; }

    /// <summary />
    public bool UnixReadOnly { get; set; }

    /// <summary />
    public bool UnixReadWrite { get; set; }

    /// <summary />
    /// <remarks>
    /// True when root on the client stays root on the volume, rather than being
    /// squashed to an unprivileged user.
    /// </remarks>
    public bool HasRootAccess { get; set; }

    /// <summary />
    /// <remarks>
    /// Restricted or Unrestricted.
    /// </remarks>
    public string? ChownMode { get; set; }
}
