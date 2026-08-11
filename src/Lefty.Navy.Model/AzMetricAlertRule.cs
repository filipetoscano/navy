namespace Lefty.Navy.Model;

/// <summary />
/// <remarks>
/// Fires on a metric emitted by the resources it watches crossing a threshold.
/// </remarks>
public class AzMetricAlertRule : AzResource
{
    /// <summary />
    public string? Description { get; set; }

    /// <summary />
    /// <remarks>
    /// Zero is the most severe and four the least.
    /// </remarks>
    public int Severity { get; set; }

    /// <summary />
    public bool Enabled { get; set; }

    /// <summary />
    /// <remarks>
    /// True when the alert closes itself once the metric comes back within its
    /// threshold, rather than waiting to be closed by hand.
    /// </remarks>
    public bool AutoMitigate { get; set; }


    /// <summary />
    /// <remarks>
    /// How often the rule runs, as an ISO 8601 duration: PT1M, PT5M and so on.
    /// </remarks>
    public string? EvaluationFrequency { get; set; }

    /// <summary>
    /// Span of metric data each evaluation looks at, as an ISO 8601 duration.
    /// </summary>
    public string? WindowSize { get; set; }


    /// <summary>
    /// Resources the rule watches.
    /// </summary>
    /// <remarks>
    /// Deliberately left as identifiers: a subscription holds far more rules
    /// than resources, and resolving these would write a copy of a watched
    /// resource under each of the rules which watch it.
    /// </remarks>
    public List<string> Scopes { get; set; } = [];

    /// <summary>
    /// Type of the watched resources, when the rule watches a whole region
    /// rather than resources named one by one.
    /// </summary>
    public string? TargetResourceType { get; set; }

    /// <summary />
    public string? TargetResourceRegion { get; set; }


    /// <summary />
    /// <remarks>
    /// The shape of the criteria, shortened from the fully qualified name Azure
    /// reports: MultipleResourceMultipleMetricCriteria,
    /// SingleResourceMultipleMetricCriteria or
    /// WebtestLocationAvailabilityCriteria.
    /// </remarks>
    public string? CriteriaType { get; set; }

    /// <summary>
    /// Conditions on the metric, all of which have to hold for the rule to fire.
    /// </summary>
    public List<AzMetricAlertCriterion> Criteria { get; set; } = [];


    /// <summary>
    /// Availability test the rule watches.
    /// </summary>
    /// <remarks>
    /// Set only for a web test rule, which states its condition here rather
    /// than in <see cref="Criteria" />.
    /// </remarks>
    public string? WebTestId { get; set; }

    /// <summary>
    /// Application Insights component behind the web test.
    /// </summary>
    public string? ComponentId { get; set; }

    /// <summary>
    /// How many test locations have to fail before a web test rule fires.
    /// </summary>
    public int FailedLocationCount { get; set; }


    /// <summary />
    public List<string> ActionGroupIds { get; set; } = [];

    /// <summary />
    public List<AzActionGroup> ActionGroups { get; set; } = [];
}


/// <summary />
public class AzMetricAlertCriterion
{
    /// <summary />
    /// <remarks>
    /// Names the criterion within the rule, and is what the fired alert refers
    /// back to. Azure generates Metric1, Metric2 and so on when the rule is
    /// written in the portal.
    /// </remarks>
    public required string Name { get; set; }

    /// <summary />
    /// <remarks>
    /// StaticThresholdCriterion, or DynamicThresholdCriterion for a criterion
    /// whose threshold Azure works out for itself.
    /// </remarks>
    public string? CriterionType { get; set; }

    /// <summary />
    public string? MetricName { get; set; }

    /// <summary />
    /// <remarks>
    /// The resource type which publishes the metric, such as
    /// Microsoft.Storage/storageAccounts.
    /// </remarks>
    public string? MetricNamespace { get; set; }

    /// <summary />
    /// <remarks>
    /// GreaterThan, LessThan, GreaterOrLessThan and so on.
    /// </remarks>
    public string? Operator { get; set; }

    /// <summary />
    /// <remarks>
    /// Reported as a fractional number even where the metric is a count.
    /// Meaningless on a dynamic criterion, which has no fixed threshold.
    /// </remarks>
    public double Threshold { get; set; }

    /// <summary />
    /// <remarks>
    /// Average, Minimum, Maximum, Total or Count.
    /// </remarks>
    public string? TimeAggregation { get; set; }

    /// <summary />
    /// <remarks>
    /// True when the rule was written against a metric which Azure could not
    /// confirm exists, which is ordinary for a custom metric and a mistake
    /// otherwise.
    /// </remarks>
    public bool SkipMetricValidation { get; set; }


    /// <summary>
    /// Restricts the criterion to part of the metric.
    /// </summary>
    public List<AzMetricAlertDimension> Dimensions { get; set; } = [];


    /// <summary>
    /// How far the metric has to stray for a dynamic criterion to fire.
    /// </summary>
    /// <remarks>
    /// Low, Medium or High. Set only on a dynamic criterion.
    /// </remarks>
    public string? AlertSensitivity { get; set; }

    /// <summary>
    /// How many evaluations within the window have to fail, on a dynamic
    /// criterion, before it fires.
    /// </summary>
    public int FailingPeriodsToAlert { get; set; }

    /// <summary>
    /// How many evaluations the window holds, on a dynamic criterion.
    /// </summary>
    public int FailingPeriodsWindow { get; set; }
}


/// <summary />
public class AzMetricAlertDimension
{
    /// <summary />
    public required string Name { get; set; }

    /// <summary />
    /// <remarks>
    /// Include or Exclude.
    /// </remarks>
    public string? Operator { get; set; }

    /// <summary />
    /// <remarks>
    /// A single entry of * stands for every value the dimension takes.
    /// </remarks>
    public List<string> Values { get; set; } = [];
}
