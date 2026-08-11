namespace Lefty.Navy.Model;

/// <summary />
/// <remarks>
/// A hub within an <see cref="AzEventHubNamespace" />. Not indexed by Resource
/// Graph, so it arrives from the management plane rather than as a resource of
/// its own, and is held by the namespace which owns it.
/// </remarks>
public class AzEventHub : AzChildResource
{
    /// <summary />
    /// <remarks>
    /// Active, Disabled, SendDisabled or ReceiveDisabled.
    /// </remarks>
    public string? Status { get; set; }

    /// <summary>
    /// How many partitions the hub is split across.
    /// </summary>
    /// <remarks>
    /// Fixed when the hub is created, except on a Premium or Dedicated
    /// namespace, and it caps how many consumers can read in parallel.
    /// </remarks>
    public int PartitionCount { get; set; }

    /// <summary>
    /// How long an event is kept, in hours.
    /// </summary>
    /// <remarks>
    /// Azure reports this twice: as whole days on every namespace, and as hours
    /// where the finer setting is available. The hours are used here, falling
    /// back to the days where only those are given.
    /// </remarks>
    public int RetentionInHours { get; set; }

    /// <summary />
    /// <remarks>
    /// Delete, or Compact on a hub which keeps the last event per key instead
    /// of expiring events by age.
    /// </remarks>
    public string? CleanupPolicy { get; set; }

    /// <summary />
    public DateTimeOffset? CreatedAt { get; set; }


    /// <summary />
    /// <remarks>
    /// True when events are written to storage as they arrive, which is what
    /// gives a hub a record older than its retention.
    /// </remarks>
    public bool CaptureEnabled { get; set; }

    /// <summary>
    /// Storage account or lake which captured events are written to.
    /// </summary>
    /// <remarks>
    /// Left as an identifier: it names a storage account which is a resource of
    /// its own in the inventory.
    /// </remarks>
    public string? CaptureDestinationId { get; set; }


    /// <summary>
    /// Consumer groups reading the hub.
    /// </summary>
    /// <remarks>
    /// Every hub has $Default whether anything uses it or not. Read with a call
    /// per hub, and left empty where the caller was not allowed to list them.
    /// </remarks>
    public List<string> ConsumerGroups { get; set; } = [];
}
