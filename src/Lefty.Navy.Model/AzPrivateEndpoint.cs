namespace Lefty.Navy.Model;

/// <summary />
public class AzPrivateEndpoint : AzResource
{
    /// <summary />
    public string? ProvisioningState { get; set; }

    /// <summary />
    public string? CustomNetworkInterfaceName { get; set; }

    /// <summary />
    public string? SubnetId { get; set; }

    /// <summary />
    public List<string> NetworkInterfaceIds { get; set; } = [];


    /// <summary />
    public List<AzPrivateEndpointConnection> Connections { get; set; } = [];

    /// <summary />
    public List<AzPrivateEndpointDnsConfig> CustomDnsConfigs { get; set; } = [];


    /// <summary />
    public AzSubnet? Subnet { get; set; }

    /// <summary />
    public List<AzNetworkInterface> NetworkInterfaces { get; set; } = [];
}


/// <summary />
public class AzPrivateEndpointConnection : AzChildResource
{
    /// <summary>
    /// Resource which the endpoint grants access to.
    /// </summary>
    /// <remarks>
    /// Deliberately left as an identifier. The resource on the other end is
    /// commonly one which itself points back at this endpoint, so resolving it
    /// would make the graph cyclic and serialization non-terminating.
    /// </remarks>
    public string? PrivateLinkServiceId { get; set; }

    /// <summary />
    /// <remarks>
    /// Sub-resources reached through the endpoint, such as blob or vault.
    /// </remarks>
    public List<string> GroupIds { get; set; } = [];

    /// <summary />
    /// <remarks>
    /// Approved, Pending, Rejected or Disconnected.
    /// </remarks>
    public string? Status { get; set; }

    /// <summary />
    public string? StatusDescription { get; set; }

    /// <summary />
    /// <remarks>
    /// True when the connection awaited approval by the owner of the target,
    /// rather than being approved automatically.
    /// </remarks>
    public bool IsManual { get; set; }
}


/// <summary />
public class AzPrivateEndpointDnsConfig
{
    /// <summary />
    public string? Fqdn { get; set; }

    /// <summary />
    public List<string> IPAddresses { get; set; } = [];
}
