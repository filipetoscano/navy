namespace Lefty.Navy.Model;

/// <summary />
/// <remarks>
/// The instrumentation key and the connection string are deliberately not
/// mapped. They are ingestion credentials, and an inventory is read by more
/// people, and kept in more places, than a credential should be.
/// </remarks>
public class AzApplicationInsights : AzResource
{
    /// <summary />
    /// <remarks>
    /// web, other, or one of the mobile kinds.
    /// </remarks>
    public string? Kind { get; set; }

    /// <summary />
    /// <remarks>
    /// Reported as Application_Type, and separate from the kind.
    /// </remarks>
    public string? ApplicationType { get; set; }

    /// <summary>
    /// Identifier used to read the telemetry back out through the query API.
    /// </summary>
    /// <remarks>
    /// Not a credential: a query also needs a token or an API key.
    /// </remarks>
    public string? AppId { get; set; }

    /// <summary />
    public string? ProvisioningState { get; set; }

    /// <summary />
    public DateTimeOffset? CreationDate { get; set; }


    /// <summary />
    /// <remarks>
    /// LogAnalytics for a workspace-based component, which is the only kind
    /// Azure still creates.
    /// </remarks>
    public string? IngestionMode { get; set; }

    /// <summary>
    /// Log Analytics workspace which holds the telemetry.
    /// </summary>
    /// <remarks>
    /// Workspaces are not modelled, so this does not resolve; it also points
    /// outside the subscription often enough that it could not always resolve
    /// anyway.
    /// </remarks>
    public string? WorkspaceResourceId { get; set; }

    /// <summary />
    public int RetentionInDays { get; set; }

    /// <summary />
    /// <remarks>
    /// 100 unless telemetry is being sampled away to hold the bill down.
    /// </remarks>
    public double SamplingPercentage { get; set; }


    /// <summary />
    /// <remarks>
    /// True when client IP addresses are recorded in full rather than being
    /// masked to a prefix.
    /// </remarks>
    public bool DisableIpMasking { get; set; }

    /// <summary />
    /// <remarks>
    /// True when the instrumentation key is refused and telemetry has to be
    /// sent with an Entra token.
    /// </remarks>
    public bool DisableLocalAuth { get; set; }

    /// <summary />
    public string? PublicNetworkAccessForIngestion { get; set; }

    /// <summary />
    public string? PublicNetworkAccessForQuery { get; set; }


    /// <summary>
    /// Private link scopes the component has been placed in.
    /// </summary>
    /// <remarks>
    /// Scopes are not modelled, and commonly live in a different subscription
    /// from the component.
    /// </remarks>
    public List<string> PrivateLinkScopedResourceIds { get; set; } = [];
}
