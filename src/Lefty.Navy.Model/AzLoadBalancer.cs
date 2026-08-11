namespace Lefty.Navy.Model;

/// <summary />
public class AzLoadBalancer : AzResource
{
    /// <summary />
    /// <remarks>
    /// Basic or Standard. A Basic balancer has been retired by Azure and cannot
    /// be created any more.
    /// </remarks>
    public string? Sku { get; set; }

    /// <summary />
    /// <remarks>
    /// Regional or Global.
    /// </remarks>
    public string? SkuTier { get; set; }

    /// <summary />
    public string? ProvisioningState { get; set; }


    /// <summary>
    /// Addresses the balancer listens on.
    /// </summary>
    /// <remarks>
    /// A configuration holding a subnet is an internal balancer, one holding a
    /// public address is an external one.
    /// </remarks>
    public List<AzLoadBalancerFrontend> FrontendIPConfigurations { get; set; } = [];

    /// <summary />
    public List<AzLoadBalancerBackendPool> BackendPools { get; set; } = [];

    /// <summary />
    public List<AzLoadBalancerRule> LoadBalancingRules { get; set; } = [];

    /// <summary />
    public List<AzLoadBalancerProbe> Probes { get; set; } = [];

    /// <summary>
    /// Rules which forward a frontend port to a single member of the pool.
    /// </summary>
    public List<AzLoadBalancerNatRule> InboundNatRules { get; set; } = [];

    /// <summary>
    /// Rules which give the pool outbound access through a frontend address.
    /// </summary>
    public List<AzLoadBalancerOutboundRule> OutboundRules { get; set; } = [];
}


/// <summary />
public class AzLoadBalancerFrontend : AzChildResource
{
    /// <summary />
    public string? PrivateIPAddress { get; set; }

    /// <summary />
    /// <remarks>
    /// Static or Dynamic.
    /// </remarks>
    public string? PrivateIPAllocationMethod { get; set; }

    /// <summary />
    public string? PrivateIPAddressVersion { get; set; }

    /// <summary />
    public string? SubnetId { get; set; }

    /// <summary />
    /// <remarks>
    /// Public addresses are not modelled, so this does not resolve.
    /// </remarks>
    public string? PublicIPAddressId { get; set; }

    /// <summary />
    /// <remarks>
    /// The availability zones the address is reachable from. Empty for an
    /// address which is not zone redundant.
    /// </remarks>
    public List<string> Zones { get; set; } = [];


    /// <summary />
    public AzSubnet? Subnet { get; set; }
}


/// <summary />
/// <remarks>
/// The members of a pool are deliberately not mapped. They are the individual
/// instance interfaces behind the balancer, which come and go as the scale set
/// behind it grows and shrinks, and there are commonly hundreds of them.
/// </remarks>
public class AzLoadBalancerBackendPool : AzChildResource
{
    /// <summary />
    public string? ProvisioningState { get; set; }

    /// <summary>
    /// How many interfaces are in the pool.
    /// </summary>
    public int MemberCount { get; set; }
}


/// <summary />
public class AzLoadBalancerRule : AzChildResource
{
    /// <summary />
    /// <remarks>
    /// Tcp, Udp or All.
    /// </remarks>
    public string? Protocol { get; set; }

    /// <summary />
    /// <remarks>
    /// Zero stands for every port, on a rule which balances them all.
    /// </remarks>
    public int FrontendPort { get; set; }

    /// <summary />
    public int BackendPort { get; set; }

    /// <summary />
    public string? FrontendIPConfigurationId { get; set; }

    /// <summary />
    public string? BackendPoolId { get; set; }

    /// <summary />
    public string? ProbeId { get; set; }

    /// <summary />
    /// <remarks>
    /// Default, SourceIP or SourceIPProtocol. Default spreads connections by
    /// five-tuple, the others pin a client to one member.
    /// </remarks>
    public string? LoadDistribution { get; set; }

    /// <summary />
    public int IdleTimeoutInMinutes { get; set; }

    /// <summary />
    /// <remarks>
    /// True on a rule which forwards the port unchanged, as a SQL availability
    /// group listener needs.
    /// </remarks>
    public bool EnableFloatingIP { get; set; }

    /// <summary />
    public bool EnableTcpReset { get; set; }

    /// <summary />
    /// <remarks>
    /// True when the rule gives the pool no outbound access of its own, which
    /// is what an outbound rule or a NAT gateway is then there for.
    /// </remarks>
    public bool DisableOutboundSnat { get; set; }
}


/// <summary />
public class AzLoadBalancerProbe : AzChildResource
{
    /// <summary />
    /// <remarks>
    /// Tcp, Http or Https.
    /// </remarks>
    public string? Protocol { get; set; }

    /// <summary />
    public int Port { get; set; }

    /// <summary />
    /// <remarks>
    /// Set for an HTTP or HTTPS probe only.
    /// </remarks>
    public string? RequestPath { get; set; }

    /// <summary />
    public int IntervalInSeconds { get; set; }

    /// <summary>
    /// How many consecutive failures take a member out of the pool.
    /// </summary>
    public int ProbeThreshold { get; set; }
}


/// <summary />
public class AzLoadBalancerNatRule : AzChildResource
{
    /// <summary />
    public string? Protocol { get; set; }

    /// <summary />
    public int FrontendPort { get; set; }

    /// <summary />
    public int BackendPort { get; set; }

    /// <summary />
    /// <remarks>
    /// Set on a rule which forwards a range of ports, one per member, rather
    /// than a single port to a single member.
    /// </remarks>
    public int FrontendPortRangeStart { get; set; }

    /// <summary />
    public int FrontendPortRangeEnd { get; set; }

    /// <summary />
    public string? FrontendIPConfigurationId { get; set; }

    /// <summary />
    public string? BackendPoolId { get; set; }

    /// <summary />
    public int IdleTimeoutInMinutes { get; set; }

    /// <summary />
    public bool EnableTcpReset { get; set; }
}


/// <summary />
public class AzLoadBalancerOutboundRule : AzChildResource
{
    /// <summary />
    public string? Protocol { get; set; }

    /// <summary>
    /// Ports given to each member for outbound connections.
    /// </summary>
    /// <remarks>
    /// Zero means Azure works the number out from the size of the pool.
    /// </remarks>
    public int AllocatedOutboundPorts { get; set; }

    /// <summary />
    public int IdleTimeoutInMinutes { get; set; }

    /// <summary />
    public bool EnableTcpReset { get; set; }

    /// <summary />
    public List<string> FrontendIPConfigurationIds { get; set; } = [];

    /// <summary />
    public string? BackendPoolId { get; set; }
}
