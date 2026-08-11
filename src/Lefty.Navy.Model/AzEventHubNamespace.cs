namespace Lefty.Navy.Model;

/// <summary />
/// <remarks>
/// The namespace is what Resource Graph indexes and what is charged for; the
/// hubs inside it are read separately, from the management plane, and attached
/// to <see cref="Hubs" />.
/// </remarks>
public class AzEventHubNamespace : AzResource
{
    /// <summary />
    /// <remarks>
    /// Basic, Standard, Premium or Dedicated.
    /// </remarks>
    public string? Sku { get; set; }

    /// <summary />
    public string? SkuTier { get; set; }

    /// <summary>
    /// Throughput units the namespace is provisioned with.
    /// </summary>
    /// <remarks>
    /// One unit is a megabyte a second in, and twice that out.
    /// </remarks>
    public int SkuCapacity { get; set; }

    /// <summary />
    public string? ProvisioningState { get; set; }

    /// <summary />
    /// <remarks>
    /// Active, Creating, Removing and so on.
    /// </remarks>
    public string? Status { get; set; }

    /// <summary />
    public DateTimeOffset? CreatedAt { get; set; }


    /// <summary>
    /// Address the namespace is reached at.
    /// </summary>
    public string? ServiceBusEndpoint { get; set; }

    /// <summary />
    /// <remarks>
    /// True when the namespace also speaks the Kafka protocol.
    /// </remarks>
    public bool KafkaEnabled { get; set; }

    /// <summary />
    /// <remarks>
    /// True when the namespace adds throughput units by itself under load,
    /// up to <see cref="MaximumThroughputUnits" />.
    /// </remarks>
    public bool IsAutoInflateEnabled { get; set; }

    /// <summary />
    /// <remarks>
    /// Zero when auto-inflate is off.
    /// </remarks>
    public int MaximumThroughputUnits { get; set; }

    /// <summary />
    public bool ZoneRedundant { get; set; }


    /// <summary />
    /// <remarks>
    /// True when shared access keys are refused and callers have to hold an
    /// Entra token.
    /// </remarks>
    public bool DisableLocalAuth { get; set; }

    /// <summary />
    public string? MinimumTlsVersion { get; set; }

    /// <summary />
    /// <remarks>
    /// Enabled, Disabled or SecuredByPerimeter.
    /// </remarks>
    public string? PublicNetworkAccess { get; set; }

    /// <summary />
    public List<string> PrivateEndpointIds { get; set; } = [];


    /// <summary>
    /// Hubs held by this namespace.
    /// </summary>
    /// <remarks>
    /// Resource Graph does not index hubs, so they are read from the management
    /// plane one namespace at a time, the way a storage account's containers
    /// are. Empty where the caller was not allowed to list them.
    /// </remarks>
    public List<AzEventHub> Hubs { get; set; } = [];


    /// <summary />
    public List<AzPrivateEndpoint> PrivateEndpoints { get; set; } = [];
}
