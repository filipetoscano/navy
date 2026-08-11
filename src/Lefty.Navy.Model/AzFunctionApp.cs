namespace Lefty.Navy.Model;

/// <summary />
/// <remarks>
/// A <c>Microsoft.Web/sites</c> resource whose kind marks it as a function app.
/// Logic Apps in the Standard tier are reported as function apps too, kind
/// <c>functionapp,workflowapp</c>, and so arrive here; the kind is kept so they
/// can be told apart.
/// <para>
/// Which functions a site holds, and what triggers them, is not part of the
/// resource: it lives in the deployed package. The storage account a function
/// app keeps its state in is named in the application settings, which are not
/// read.
/// </para>
/// </remarks>
public class AzFunctionApp : AzWebSite
{
    /// <summary />
    /// <remarks>
    /// True when the kind marks the site as a Standard Logic App rather than an
    /// ordinary function app.
    /// </remarks>
    public bool IsWorkflowApp { get; set; }

    /// <summary>
    /// Memory given to each instance, in MB.
    /// </summary>
    public int ContainerSize { get; set; }

    /// <summary>
    /// Consumption allowance, in GB-seconds per day.
    /// </summary>
    /// <remarks>
    /// Zero means no quota, which is the ordinary case; a site which exceeds a
    /// quota is stopped until the day rolls over.
    /// </remarks>
    public long DailyMemoryTimeQuota { get; set; }

    /// <summary>
    /// Most instances the app may scale out to.
    /// </summary>
    /// <remarks>
    /// Zero means the limit of the plan applies. Set on a consumption or
    /// elastic premium app to hold the bill down.
    /// </remarks>
    public int FunctionAppScaleLimit { get; set; }

    /// <summary>
    /// Instances kept warm on an elastic premium plan.
    /// </summary>
    public int MinimumElasticInstanceCount { get; set; }
}
