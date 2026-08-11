namespace Lefty.Navy.Model;

/// <summary />
/// <remarks>
/// Fires on a pattern which Azure works out for itself, rather than on a
/// condition someone wrote down. Created by Azure alongside an Application
/// Insights component more often than by anyone deliberately.
/// </remarks>
public class AzSmartDetectorAlertRule : AzResource
{
    /// <summary />
    public string? Description { get; set; }

    /// <summary />
    /// <remarks>
    /// Enabled or Disabled. Reported as a state rather than as the boolean the
    /// other alert rules use.
    /// </remarks>
    public string? State { get; set; }

    /// <summary />
    /// <remarks>
    /// Sev0 through Sev4, again unlike the other alert rules, which report a
    /// number.
    /// </remarks>
    public string? Severity { get; set; }

    /// <summary />
    /// <remarks>
    /// How often the detector runs, as an ISO 8601 duration.
    /// </remarks>
    public string? Frequency { get; set; }


    /// <summary>
    /// Which detector the rule runs.
    /// </summary>
    /// <remarks>
    /// FailureAnomaliesDetector and so on. The detector is part of Azure rather
    /// than of the subscription, so only enough to recognize it is kept: the
    /// description Azure returns alongside it is several paragraphs of marketing
    /// HTML, identical for every rule which runs the same detector.
    /// </remarks>
    public string? DetectorId { get; set; }

    /// <summary />
    public string? DetectorName { get; set; }

    /// <summary />
    public List<string> DetectorSupportedResourceTypes { get; set; } = [];


    /// <summary>
    /// Resources the detector watches.
    /// </summary>
    /// <remarks>
    /// Left as identifiers, as on the other alert rules.
    /// </remarks>
    public List<string> Scopes { get; set; } = [];

    /// <summary>
    /// Shortest interval between two alerts from this rule, as an ISO 8601
    /// duration.
    /// </summary>
    /// <remarks>
    /// Null when the rule is not throttled, which is the default.
    /// </remarks>
    public string? ThrottlingDuration { get; set; }


    /// <summary />
    public string? CustomEmailSubject { get; set; }

    /// <summary />
    public string? CustomWebhookPayload { get; set; }

    /// <summary />
    public List<string> ActionGroupIds { get; set; } = [];

    /// <summary />
    public List<AzActionGroup> ActionGroups { get; set; } = [];
}
