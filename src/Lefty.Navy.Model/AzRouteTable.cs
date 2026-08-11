namespace Lefty.Navy.Model;

/// <summary />
public class AzRouteTable : AzResource
{
    /// <summary />
    /// <remarks>
    /// True when routes learned over BGP, from an ExpressRoute circuit or a VPN
    /// gateway, are kept out of the subnets this table is attached to.
    /// </remarks>
    public bool DisableBgpRoutePropagation { get; set; }

    /// <summary />
    public string? ProvisioningState { get; set; }


    /// <summary />
    public List<AzRoute> Routes { get; set; } = [];

    /// <summary>
    /// Subnets this table is attached to.
    /// </summary>
    /// <remarks>
    /// Deliberately left as identifiers. A subnet already holds the table which
    /// applies to it, so resolving these would close a loop and make the inline
    /// serialization non-terminating.
    /// </remarks>
    public List<string> SubnetIds { get; set; } = [];
}


/// <summary />
public class AzRoute : AzChildResource
{
    /// <summary />
    public string? AddressPrefix { get; set; }

    /// <summary />
    /// <remarks>
    /// VirtualAppliance, VirtualNetworkGateway, VnetLocal, Internet or None.
    /// </remarks>
    public string? NextHopType { get; set; }

    /// <summary />
    /// <remarks>
    /// Only set when <see cref="NextHopType" /> is VirtualAppliance.
    /// </remarks>
    public string? NextHopIpAddress { get; set; }

    /// <summary />
    public bool HasBgpOverride { get; set; }
}
