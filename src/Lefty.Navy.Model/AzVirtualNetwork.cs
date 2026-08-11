namespace Lefty.Navy.Model;

/// <summary />
public class AzVirtualNetwork : AzResource
{
    /// <summary />
    public required List<string> AddressPrefixes { get; set; }

    /// <summary />
    public required List<string> DnsServers { get; set; }

    /// <summary />
    public required List<AzSubnet> Subnets { get; set; }
}


/// <summary />
public class AzSubnet : AzChildResource
{
    /// <summary />
    public required string AddressPrefix { get; set; }

    /// <summary />
    /// <remarks>
    /// Null when the subnet has no network security group associated with it.
    /// </remarks>
    public string? NetworkSecurityGroupId { get; set; }

    /// <summary />
    /// <remarks>
    /// Null when the subnet has no route table associated with it.
    /// </remarks>
    public string? RouteTableId { get; set; }


    /// <summary />
    public AzNetworkSecurityGroup? NetworkSecurityGroup { get; set; }

    /// <summary />
    public AzRouteTable? RouteTable { get; set; }
}