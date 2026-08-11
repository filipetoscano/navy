namespace Lefty.Navy.Model;

/// <summary />
/// <remarks>
/// A web app or API app: a <c>Microsoft.Web/sites</c> resource whose kind does
/// not make it a function app. Everything it reports in common with the other
/// kinds is on <see cref="AzWebSite" />.
/// </remarks>
public class AzAppService : AzWebSite
{
    /// <summary />
    /// <remarks>
    /// True when a client is pinned to the instance which first served it, by
    /// way of a cookie. Worth knowing, because it keeps a site from scaling
    /// evenly.
    /// </remarks>
    public bool ClientAffinityEnabled { get; set; }

    /// <summary>
    /// How many instances the plan is running this site on.
    /// </summary>
    public int NumberOfWorkers { get; set; }

    /// <summary />
    /// <remarks>
    /// None, Manual, Failover, ActiveActive or GeoRedundant.
    /// </remarks>
    public string? RedundancyMode { get; set; }

    /// <summary />
    /// <remarks>
    /// True when the site answers only on its custom hostnames, and its
    /// azurewebsites.net name is refused.
    /// </remarks>
    public bool HostNamesDisabled { get; set; }
}
