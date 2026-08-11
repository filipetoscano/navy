namespace Lefty.Navy.Model;

/// <summary />
/// <remarks>
/// Where an alert rule sends what it has to say. The same group is normally
/// named by many rules.
/// </remarks>
public class AzActionGroup : AzResource
{
    /// <summary>
    /// Abbreviated name, which is what appears in an SMS or a voice call.
    /// </summary>
    /// <remarks>
    /// Limited by Azure to twelve characters.
    /// </remarks>
    public string? GroupShortName { get; set; }

    /// <summary />
    /// <remarks>
    /// A disabled group is still named by its rules, and silently drops
    /// everything they send it.
    /// </remarks>
    public bool Enabled { get; set; }


    /// <summary />
    /// <remarks>
    /// Azure reports the receivers as one array per kind, eleven of them, all
    /// but one of which is ordinarily empty. They are flattened into a single
    /// list here, with <see cref="AzActionGroupReceiver.Kind" /> recording which
    /// array a receiver came from.
    /// </remarks>
    public List<AzActionGroupReceiver> Receivers { get; set; } = [];
}


/// <summary />
/// <remarks>
/// One destination within a group. Receivers are not resources and carry no
/// identifier of their own; a name is unique within a group.
/// </remarks>
public class AzActionGroupReceiver
{
    /// <summary />
    /// <remarks>
    /// Email, Sms, Webhook, ArmRole, AzureFunction, LogicApp, EventHub, Voice,
    /// AutomationRunbook, Itsm or AzureAppPush.
    /// </remarks>
    public required string Kind { get; set; }

    /// <summary />
    public required string Name { get; set; }

    /// <summary>
    /// Where the notification goes.
    /// </summary>
    /// <remarks>
    /// What this holds depends on <see cref="Kind" />: an email address, a
    /// phone number, a URI for a webhook, a role identifier for ArmRole, and
    /// the identifier of the target resource for the kinds which invoke one.
    /// Null for a receiver whose destination Azure does not report.
    /// </remarks>
    public string? Target { get; set; }

    /// <summary />
    /// <remarks>
    /// Reported for the kinds which have to be confirmed by whoever owns the
    /// address, which is email and SMS: Enabled once they have.
    /// </remarks>
    public string? Status { get; set; }

    /// <summary />
    /// <remarks>
    /// True when the receiver is sent the common alert schema rather than a
    /// payload in the shape of whichever rule fired.
    /// </remarks>
    public bool UseCommonAlertSchema { get; set; }
}
