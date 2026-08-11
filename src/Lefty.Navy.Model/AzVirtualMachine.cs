namespace Lefty.Navy.Model;

/// <summary />
public class AzVirtualMachine : AzResource
{
    /// <summary>
    /// Identifier Azure gives the machine internally, which is not its resource
    /// identifier and which survives the machine being resized or moved.
    /// </summary>
    public string? VmId { get; set; }

    /// <summary />
    /// <remarks>
    /// The size, such as Standard_D2s_v5. Reported inside the hardware profile
    /// rather than as a sku of its own, unlike a scale set.
    /// </remarks>
    public string? VmSize { get; set; }

    /// <summary />
    public string? ProvisioningState { get; set; }

    /// <summary>
    /// Whether the machine is running.
    /// </summary>
    /// <remarks>
    /// Reported as PowerState/running, PowerState/deallocated and so on. A
    /// deallocated machine is not charged for compute but still holds, and
    /// pays for, its disks.
    /// </remarks>
    public string? PowerState { get; set; }

    /// <summary />
    public DateTimeOffset? TimeCreated { get; set; }

    /// <summary />
    /// <remarks>
    /// Regular, or Spot for a machine which Azure may evict.
    /// </remarks>
    public string? Priority { get; set; }

    /// <summary>
    /// Existing licence brought to the machine, such as Windows_Server.
    /// </summary>
    /// <remarks>
    /// Null on a machine paying the full rate for its operating system.
    /// </remarks>
    public string? LicenseType { get; set; }


    /// <summary />
    public string? ComputerName { get; set; }

    /// <summary />
    public string? AdminUsername { get; set; }

    /// <summary />
    /// <remarks>
    /// Reported by the instance view, so it is what the machine is actually
    /// running rather than what it was built from: Windows Server 2022
    /// Datacenter and so on.
    /// </remarks>
    public string? OsName { get; set; }

    /// <summary />
    public string? OsVersion { get; set; }

    /// <summary />
    /// <remarks>
    /// Linux or Windows.
    /// </remarks>
    public string? OsType { get; set; }

    /// <summary />
    /// <remarks>
    /// V1 or V2. Trusted launch and confidential machines need V2.
    /// </remarks>
    public string? HyperVGeneration { get; set; }

    /// <summary />
    /// <remarks>
    /// True on a Linux machine which accepts only key authentication. The
    /// public keys themselves are not mapped.
    /// </remarks>
    public bool DisablePasswordAuthentication { get; set; }

    /// <summary />
    /// <remarks>
    /// AutomaticByPlatform, AutomaticByOS, Manual or ImageDefault. Which of
    /// them applies decides whether the machine patches itself.
    /// </remarks>
    public string? PatchMode { get; set; }


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
    public bool SecureBootEnabled { get; set; }

    /// <summary />
    public bool VTpmEnabled { get; set; }

    /// <summary />
    public bool BootDiagnosticsEnabled { get; set; }


    /// <summary>
    /// Image the machine was built from, when it comes from a gallery.
    /// </summary>
    /// <remarks>
    /// Commonly lives in a subscription owned by whoever publishes it, and so
    /// does not resolve.
    /// </remarks>
    public string? ImageReferenceId { get; set; }

    /// <summary>
    /// Image the machine was built from, when it comes from the marketplace.
    /// </summary>
    /// <remarks>
    /// Reported as publisher, offer, sku and version, joined here with colons.
    /// </remarks>
    public string? ImageReference { get; set; }


    /// <summary />
    public AzVirtualMachineDisk? OsDisk { get; set; }

    /// <summary />
    public List<AzVirtualMachineDisk> DataDisks { get; set; } = [];

    /// <summary>
    /// Set which encrypts the operating system disk with a customer-managed key.
    /// </summary>
    /// <remarks>
    /// A data disk may in principle name a different set; where one does, it is
    /// recorded on the disk itself.
    /// </remarks>
    public string? DiskEncryptionSetId { get; set; }


    /// <summary>
    /// Availability set the machine belongs to.
    /// </summary>
    /// <remarks>
    /// Availability sets are not modelled, so this does not resolve.
    /// </remarks>
    public string? AvailabilitySetId { get; set; }

    /// <summary>
    /// Scale set the machine belongs to.
    /// </summary>
    /// <remarks>
    /// Set only on a machine in a Flexible scale set, where the instances are
    /// virtual machines in their own right. Left as an identifier: the scale
    /// set is a resource of its own in the inventory.
    /// </remarks>
    public string? VirtualMachineScaleSetId { get; set; }

    /// <summary />
    public List<string> NetworkInterfaceIds { get; set; } = [];


    /// <summary />
    public List<AzNetworkInterface> NetworkInterfaces { get; set; } = [];

    /// <summary />
    public AzDiskEncryptionSet? DiskEncryptionSet { get; set; }
}


/// <summary />
/// <remarks>
/// A managed disk attached to the machine. Disks are resources in their own
/// right, but are reported here as part of the machine which holds them; only
/// the identifier is kept, so a disk is not described twice.
/// </remarks>
public class AzVirtualMachineDisk
{
    /// <summary />
    public required string Name { get; set; }

    /// <summary />
    public string? ManagedDiskId { get; set; }

    /// <summary>
    /// Slot the disk is attached at.
    /// </summary>
    /// <remarks>
    /// Meaningful on a data disk only; the operating system disk has no unit
    /// number.
    /// </remarks>
    public int Lun { get; set; }

    /// <summary />
    public int DiskSizeGB { get; set; }

    /// <summary />
    /// <remarks>
    /// Premium_LRS, StandardSSD_LRS and so on.
    /// </remarks>
    public string? StorageAccountType { get; set; }

    /// <summary />
    /// <remarks>
    /// None, ReadOnly or ReadWrite.
    /// </remarks>
    public string? Caching { get; set; }

    /// <summary />
    /// <remarks>
    /// Delete or Detach: what becomes of the disk when the machine goes.
    /// </remarks>
    public string? DeleteOption { get; set; }

    /// <summary />
    public string? DiskEncryptionSetId { get; set; }

    /// <summary />
    public bool WriteAcceleratorEnabled { get; set; }
}
