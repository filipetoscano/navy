namespace Lefty.Navy.Model;

/// <summary />
/// <remarks>
/// Azure reports web apps, function apps, logic apps and API apps as the one
/// resource type, <c>Microsoft.Web/sites</c>, told apart only by the kind. This
/// holds what all of them report; <see cref="AzAppService" /> and
/// <see cref="AzFunctionApp" /> add what is theirs alone.
/// <para>
/// The application settings, which is where a site's connection strings and
/// keys live, are not part of the resource as Resource Graph returns it and are
/// not read separately. They would be the most sensitive thing in the
/// inventory.
/// </para>
/// </remarks>
public abstract class AzWebSite : AzResource
{
    /// <summary />
    /// <remarks>
    /// The kind as Azure reports it, which is a comma-separated list rather
    /// than a single word: app, app,linux, functionapp,linux and so on.
    /// </remarks>
    public string? Kind { get; set; }

    /// <summary />
    /// <remarks>
    /// Running or Stopped.
    /// </remarks>
    public string? State { get; set; }

    /// <summary />
    /// <remarks>
    /// False on a site which has been disabled outright, which is not the same
    /// as one which is merely stopped.
    /// </remarks>
    public bool Enabled { get; set; }

    /// <summary />
    /// <remarks>
    /// True on a site which runs on Linux. Azure calls the field reserved.
    /// </remarks>
    public bool IsLinux { get; set; }


    /// <summary />
    public string? DefaultHostName { get; set; }

    /// <summary>
    /// Every hostname the site answers on, including the default one.
    /// </summary>
    public List<string> HostNames { get; set; } = [];

    /// <summary />
    /// <remarks>
    /// True when the site refuses plain HTTP rather than redirecting it.
    /// </remarks>
    public bool HttpsOnly { get; set; }

    /// <summary />
    /// <remarks>
    /// 1.0, 1.1 or 1.2. Anything below 1.2 is worth noticing.
    /// </remarks>
    public string? MinTlsVersion { get; set; }

    /// <summary />
    /// <remarks>
    /// AllAllowed, FtpsOnly or Disabled. AllAllowed leaves plain FTP open.
    /// </remarks>
    public string? FtpsState { get; set; }

    /// <summary />
    public bool Http20Enabled { get; set; }

    /// <summary />
    /// <remarks>
    /// True when a client certificate is demanded of callers.
    /// </remarks>
    public bool ClientCertEnabled { get; set; }

    /// <summary />
    /// <remarks>
    /// Required, Optional or OptionalInteractiveUser.
    /// </remarks>
    public string? ClientCertMode { get; set; }

    /// <summary />
    /// <remarks>
    /// Enabled or Disabled. Disabled leaves the site reachable only through a
    /// private endpoint.
    /// </remarks>
    public string? PublicNetworkAccess { get; set; }


    /// <summary>
    /// Plan the site runs on.
    /// </summary>
    /// <remarks>
    /// App Service plans are not modelled, so this does not resolve. A plan is
    /// shared by many sites, and is where the compute is actually bought.
    /// </remarks>
    public string? ServerFarmId { get; set; }

    /// <summary />
    /// <remarks>
    /// The runtime stack, as Azure records it: DOTNETCORE|8.0, PYTHON|3.12 and
    /// so on for Linux, or the .NET version for Windows.
    /// </remarks>
    public string? RuntimeStack { get; set; }

    /// <summary />
    public bool AlwaysOn { get; set; }

    /// <summary>
    /// Subnet the site sends its outbound traffic through.
    /// </summary>
    public string? VirtualNetworkSubnetId { get; set; }

    /// <summary />
    /// <remarks>
    /// True when all outbound traffic goes through the subnet, rather than only
    /// traffic bound for private addresses.
    /// </remarks>
    public bool VnetRouteAllEnabled { get; set; }

    /// <summary>
    /// Addresses the site is currently seen to come from.
    /// </summary>
    /// <remarks>
    /// Reported as one comma-separated string rather than as a list. The
    /// possible set is larger, and changes when the site changes plan.
    /// </remarks>
    public string? OutboundIpAddresses { get; set; }

    /// <summary />
    public List<string> PrivateEndpointIds { get; set; } = [];


    /// <summary />
    public AzSubnet? Subnet { get; set; }

    /// <summary />
    public List<AzPrivateEndpoint> PrivateEndpoints { get; set; } = [];
}
