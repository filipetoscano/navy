namespace Lefty.Navy.Model;

/// <summary />
/// <remarks>
/// The individual instances are not resources of their own and are not
/// reported here; a scale set describes the machine it makes copies of.
/// </remarks>
public class AzVirtualMachineScaleSet : AzResource
{
    /// <summary>
    /// Size of the machines, such as Standard_D8s_v4.
    /// </summary>
    public string? Sku { get; set; }

    /// <summary />
    public string? SkuTier { get; set; }

    /// <summary>
    /// How many instances the set is asked to hold.
    /// </summary>
    /// <remarks>
    /// What the set is set to, which is not necessarily how many are running.
    /// </remarks>
    public int SkuCapacity { get; set; }

    /// <summary />
    /// <remarks>
    /// Uniform, where every instance is a copy of the same profile, or
    /// Flexible, where instances are ordinary virtual machines.
    /// </remarks>
    public string? OrchestrationMode { get; set; }

    /// <summary />
    /// <remarks>
    /// Manual, Automatic or Rolling. Manual means an upgraded profile does not
    /// reach the instances until someone says so.
    /// </remarks>
    public string? UpgradeMode { get; set; }

    /// <summary />
    public string? ProvisioningState { get; set; }

    /// <summary />
    public DateTimeOffset? TimeCreated { get; set; }

    /// <summary />
    public bool Overprovision { get; set; }

    /// <summary />
    public bool SinglePlacementGroup { get; set; }

    /// <summary />
    public int PlatformFaultDomainCount { get; set; }


    /// <summary />
    public string? ComputerNamePrefix { get; set; }

    /// <summary />
    public string? AdminUsername { get; set; }

    /// <summary />
    /// <remarks>
    /// True on a Linux set which accepts only key authentication. The public
    /// keys themselves are not mapped.
    /// </remarks>
    public bool DisablePasswordAuthentication { get; set; }


    /// <summary />
    /// <remarks>
    /// Standard, TrustedLaunch or ConfidentialVM.
    /// </remarks>
    public string? SecurityType { get; set; }

    /// <summary />
    /// <remarks>
    /// True when the temporary disk and the caches are encrypted on the host as
    /// well, and not only the managed disks.
    /// </remarks>
    public bool EncryptionAtHost { get; set; }


    /// <summary />
    /// <remarks>
    /// Linux or Windows.
    /// </remarks>
    public string? OsType { get; set; }

    /// <summary />
    public int OsDiskSizeGB { get; set; }

    /// <summary />
    /// <remarks>
    /// None, ReadOnly or ReadWrite.
    /// </remarks>
    public string? OsDiskCaching { get; set; }

    /// <summary />
    /// <remarks>
    /// Premium_LRS, StandardSSD_LRS and so on.
    /// </remarks>
    public string? OsDiskStorageAccountType { get; set; }

    /// <summary />
    public string? DiskEncryptionSetId { get; set; }

    /// <summary>
    /// Image the instances are built from.
    /// </summary>
    /// <remarks>
    /// An identifier for an image from a gallery, which commonly lives in a
    /// subscription owned by whoever publishes it and so does not resolve.
    /// </remarks>
    public string? ImageReferenceId { get; set; }

    /// <summary>
    /// Image the instances are built from, when it comes from the marketplace.
    /// </summary>
    /// <remarks>
    /// Reported as publisher, offer, sku and version, joined here with colons.
    /// </remarks>
    public string? ImageReference { get; set; }


    /// <summary />
    public List<AzScaleSetNetworkInterface> NetworkInterfaces { get; set; } = [];

    /// <summary />
    public List<AzScaleSetExtension> Extensions { get; set; } = [];


    /// <summary />
    public AzDiskEncryptionSet? DiskEncryptionSet { get; set; }
}


/// <summary />
/// <remarks>
/// The interface each instance is given, rather than an interface which exists.
/// The instance interfaces are not indexed by Resource Graph.
/// </remarks>
public class AzScaleSetNetworkInterface
{
    /// <summary />
    public required string Name { get; set; }

    /// <summary />
    public bool Primary { get; set; }

    /// <summary />
    public bool EnableAcceleratedNetworking { get; set; }

    /// <summary />
    public bool EnableIPForwarding { get; set; }

    /// <summary />
    public string? NetworkSecurityGroupId { get; set; }

    /// <summary />
    public List<AzScaleSetIPConfiguration> IPConfigurations { get; set; } = [];


    /// <summary />
    public AzNetworkSecurityGroup? NetworkSecurityGroup { get; set; }
}


/// <summary />
public class AzScaleSetIPConfiguration
{
    /// <summary />
    public required string Name { get; set; }

    /// <summary />
    public bool Primary { get; set; }

    /// <summary />
    public string? PrivateIPAddressVersion { get; set; }

    /// <summary />
    public string? SubnetId { get; set; }

    /// <summary>
    /// Backend pools the instances are placed into.
    /// </summary>
    /// <remarks>
    /// Deliberately left as identifiers: they name a pool within a balancer,
    /// and resolving them would write a copy of the balancer under every scale
    /// set behind it.
    /// </remarks>
    public List<string> LoadBalancerBackendPoolIds { get; set; } = [];


    /// <summary />
    public AzSubnet? Subnet { get; set; }
}


/// <summary />
public class AzScaleSetExtension
{
    /// <summary />
    public required string Name { get; set; }

    /// <summary />
    public string? Publisher { get; set; }

    /// <summary />
    /// <remarks>
    /// The type of the extension, such as CustomScript, which together with the
    /// publisher names it.
    /// </remarks>
    public string? ExtensionType { get; set; }

    /// <summary />
    public string? TypeHandlerVersion { get; set; }

    /// <summary />
    public bool AutoUpgradeMinorVersion { get; set; }
}
