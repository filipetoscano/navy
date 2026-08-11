namespace Lefty.Navy.Model;

/// <summary />
/// <remarks>
/// Fires on an entry written to the activity log, which is to say on something
/// having been done to a resource, rather than on anything the resource itself
/// reports.
/// </remarks>
public class AzActivityLogAlertRule : AzResource
{
    /// <summary />
    public string? Description { get; set; }

    /// <summary />
    public bool Enabled { get; set; }


    /// <summary>
    /// What the rule watches: a subscription, a resource group or a resource.
    /// </summary>
    /// <remarks>
    /// Deliberately left as identifiers. A scope is most often the subscription
    /// itself, which is not a resource in the inventory, and resolving the rest
    /// would write a copy of every watched resource under every rule which
    /// watches it.
    /// </remarks>
    public List<string> Scopes { get; set; } = [];

    /// <summary>
    /// Conditions which an entry has to meet, all of them, for the rule to fire.
    /// </summary>
    public List<AzActivityLogAlertCondition> Conditions { get; set; } = [];


    /// <summary />
    public List<string> ActionGroupIds { get; set; } = [];

    /// <summary />
    public List<AzActionGroup> ActionGroups { get; set; } = [];
}


/// <summary />
/// <remarks>
/// A single test against one field of the activity log entry, such as category
/// or operationName.
/// </remarks>
public class AzActivityLogAlertCondition
{
    /// <summary />
    /// <remarks>
    /// Null on a condition which is itself a set of alternatives, in which case
    /// <see cref="AnyOf" /> holds them.
    /// </remarks>
    public string? Field { get; set; }

    /// <summary>
    /// Value the field has to hold.
    /// </summary>
    /// <remarks>
    /// Azure names this one <c>equals</c>, which cannot be used here without
    /// hiding <see cref="object.Equals(object)" />.
    /// </remarks>
    public string? EqualTo { get; set; }

    /// <summary>
    /// Values, any one of which the field may hold.
    /// </summary>
    /// <remarks>
    /// The alternative to <see cref="EqualTo" />, and never set alongside it.
    /// </remarks>
    public List<string> ContainsAny { get; set; } = [];

    /// <summary>
    /// Conditions, any one of which is enough to satisfy this one.
    /// </summary>
    /// <remarks>
    /// Azure allows one level of nesting here and no more, so the conditions
    /// held by this list never hold conditions of their own.
    /// </remarks>
    public List<AzActivityLogAlertCondition> AnyOf { get; set; } = [];
}
