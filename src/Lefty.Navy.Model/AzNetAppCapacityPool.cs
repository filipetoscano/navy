namespace Lefty.Navy.Model;

/// <summary />
/// <remarks>
/// The unit which Azure NetApp Files is bought in: a pool is provisioned at a
/// size and a service level, and the volumes carved out of it share what it
/// paid for. Reported as a child of the NetApp account, which is why its name
/// arrives as account/pool.
/// </remarks>
public class AzNetAppCapacityPool : AzResource
{
    /// <summary>
    /// NetApp account which holds the pool.
    /// </summary>
    /// <remarks>
    /// Taken from the identifier, which is the only place the relationship is
    /// stated. Deliberately left as one: the account holds its pools, and
    /// resolving this would close a loop.
    /// </remarks>
    public string? NetAppAccountId { get; set; }

    /// <summary />
    public string? ProvisioningState { get; set; }

    /// <summary>
    /// Identifier Azure gives the pool internally, which is not its resource
    /// identifier.
    /// </summary>
    public string? PoolId { get; set; }


    /// <summary />
    /// <remarks>
    /// Standard, Premium or Ultra. A volume may only be created at the level of
    /// the pool it comes from.
    /// </remarks>
    public string? ServiceLevel { get; set; }

    /// <summary>
    /// Provisioned size, in bytes.
    /// </summary>
    /// <remarks>
    /// Charged in full whether the volumes use it or not, which is what makes
    /// this worth having in an inventory.
    /// </remarks>
    public long Size { get; set; }

    /// <summary />
    /// <remarks>
    /// Auto, where throughput follows the size of each volume, or Manual, where
    /// each volume is given a share of the pool by hand.
    /// </remarks>
    public string? QosType { get; set; }

    /// <summary>
    /// Throughput the pool has to give out, in MiB per second.
    /// </summary>
    public double TotalThroughputMibps { get; set; }

    /// <summary>
    /// Throughput the volumes have taken of it.
    /// </summary>
    public double UtilizedThroughputMibps { get; set; }

    /// <summary />
    /// <remarks>
    /// True when infrequently read blocks are moved to cheaper storage.
    /// </remarks>
    public bool CoolAccess { get; set; }

    /// <summary />
    /// <remarks>
    /// Single or Double. Fixed when the pool is created and not changeable
    /// afterwards.
    /// </remarks>
    public string? EncryptionType { get; set; }


    /// <summary>
    /// Volumes carved out of this pool.
    /// </summary>
    /// <remarks>
    /// A volume is returned as a resource in its own right rather than as part
    /// of the pool, and is attached here once everything has been read.
    /// </remarks>
    public List<AzNetAppVolume> Volumes { get; set; } = [];
}
