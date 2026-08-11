namespace Lefty.Navy.Model;

/// <summary />
public class AzNetworkSecurityGroup : AzResource
{
    /// <summary />
    public string? ProvisioningState { get; set; }

    /// <summary />
    /// <remarks>
    /// True when a change to the rules is applied to connections which are
    /// already established, rather than only to new ones.
    /// </remarks>
    public bool FlushConnection { get; set; }


    /// <summary>
    /// Rules written by whoever owns the group.
    /// </summary>
    /// <remarks>
    /// The default rules which Azure adds to every group are deliberately not
    /// mapped: they are the same everywhere, and carry no information about the
    /// subscription being inventoried.
    /// </remarks>
    public List<AzSecurityRule> SecurityRules { get; set; } = [];

    /// <summary>
    /// Subnets this group is attached to.
    /// </summary>
    /// <remarks>
    /// Deliberately left as identifiers. A subnet already holds the group which
    /// applies to it, so resolving these would close a loop and make the inline
    /// serialization non-terminating.
    /// </remarks>
    public List<string> SubnetIds { get; set; } = [];

    /// <summary>
    /// Network interfaces this group is attached to.
    /// </summary>
    /// <remarks>
    /// Left as identifiers for the same reason as <see cref="SubnetIds" />: an
    /// interface already holds its group.
    /// </remarks>
    public List<string> NetworkInterfaceIds { get; set; } = [];
}


/// <summary />
public class AzSecurityRule : AzChildResource
{
    /// <summary />
    public string? Description { get; set; }

    /// <summary />
    /// <remarks>
    /// Inbound or Outbound.
    /// </remarks>
    public string? Direction { get; set; }

    /// <summary />
    /// <remarks>
    /// Allow or Deny.
    /// </remarks>
    public string? Access { get; set; }

    /// <summary />
    /// <remarks>
    /// Rules are evaluated from the lowest priority upwards, and the first one
    /// which matches decides the traffic.
    /// </remarks>
    public int Priority { get; set; }

    /// <summary />
    /// <remarks>
    /// Tcp, Udp, Icmp, Esp, Ah, or * for any of them.
    /// </remarks>
    public string? Protocol { get; set; }


    /// <summary />
    /// <remarks>
    /// A CIDR range, a single address, a service tag such as Internet or
    /// VirtualNetwork, or * for any source. Azure states this either as one
    /// value or as a list, never as both; the model keeps only the list, so
    /// that a reader never has to look in two places.
    /// </remarks>
    public List<string> SourceAddressPrefixes { get; set; } = [];

    /// <summary />
    /// <remarks>
    /// A port, a range such as 1024-1039, or * for any. Stated as one value or
    /// as a list, as with <see cref="SourceAddressPrefixes" />.
    /// </remarks>
    public List<string> SourcePortRanges { get; set; } = [];

    /// <summary />
    /// <remarks>
    /// Stated as one value or as a list, as with
    /// <see cref="SourceAddressPrefixes" />.
    /// </remarks>
    public List<string> DestinationAddressPrefixes { get; set; } = [];

    /// <summary />
    /// <remarks>
    /// Stated as one value or as a list, as with
    /// <see cref="SourcePortRanges" />.
    /// </remarks>
    public List<string> DestinationPortRanges { get; set; } = [];


    /// <summary>
    /// Application security groups named as the source, in place of an address.
    /// </summary>
    /// <remarks>
    /// Application security groups are not modelled, so these do not resolve.
    /// </remarks>
    public List<string> SourceApplicationSecurityGroupIds { get; set; } = [];

    /// <summary>
    /// Application security groups named as the destination.
    /// </summary>
    /// <remarks>
    /// Not modelled, as with <see cref="SourceApplicationSecurityGroupIds" />.
    /// </remarks>
    public List<string> DestinationApplicationSecurityGroupIds { get; set; } = [];
}
