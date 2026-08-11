namespace Lefty.Navy.Model;

/// <summary />
public class AzNetworkInterface : AzResource
{
    /// <summary />
    public string? MacAddress { get; set; }

    /// <summary />
    /// <remarks>
    /// Standard or Elastic.
    /// </remarks>
    public string? NicType { get; set; }

    /// <summary />
    public bool EnableAcceleratedNetworking { get; set; }

    /// <summary />
    public bool EnableIPForwarding { get; set; }

    /// <summary />
    public bool DisableTcpStateTracking { get; set; }

    /// <summary />
    public string? ProvisioningState { get; set; }


    /// <summary />
    public List<string> DnsServers { get; set; } = [];

    /// <summary />
    public List<string> AppliedDnsServers { get; set; } = [];

    /// <summary />
    public string? InternalDomainNameSuffix { get; set; }


    /// <summary />
    public string? NetworkSecurityGroupId { get; set; }

    /// <summary />
    /// <remarks>
    /// Set for an interface attached to a virtual machine. Virtual machines are
    /// not modelled, so this does not resolve.
    /// </remarks>
    public string? VirtualMachineId { get; set; }

    /// <summary />
    /// <remarks>
    /// Set for an interface which Azure created to back a private endpoint.
    /// Deliberately not resolved to the endpoint itself: the endpoint already
    /// holds its interfaces, and resolving both directions would make the graph
    /// cyclic and serialization non-terminating.
    /// </remarks>
    public string? PrivateEndpointId { get; set; }


    /// <summary />
    public List<AzNetworkInterfaceIPConfiguration> IPConfigurations { get; set; } = [];


    /// <summary />
    public AzNetworkSecurityGroup? NetworkSecurityGroup { get; set; }
}


/// <summary />
public class AzNetworkInterfaceIPConfiguration : AzChildResource
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
    public bool Primary { get; set; }

    /// <summary />
    public string? SubnetId { get; set; }

    /// <summary />
    public string? PublicIPAddressId { get; set; }


    /// <summary />
    public AzSubnet? Subnet { get; set; }
}
