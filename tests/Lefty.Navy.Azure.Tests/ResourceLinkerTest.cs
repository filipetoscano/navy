using Lefty.Navy.Azure;
using Lefty.Navy.Model;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace Lefty.Navy.Tests;

/// <summary />
public class ResourceLinkerTest
{
    private const string NsgId = "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/networkSecurityGroups/nsg-one";
    private const string RouteTableId = "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/routeTables/rt-one";
    private const string EndpointId = "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/privateEndpoints/pe-one";

    private static readonly ResourceLinker Linker = new( NullLogger.Instance );


    /// <summary />
    private static T Resource<T>( string id, string type )
        where T : AzResource
    {
        var resource = Activator.CreateInstance<T>();

        resource.Id = id;
        resource.Name = id.Split( '/' ).Last();
        resource.Type = type;
        resource.Location = "westeurope";

        return resource;
    }


    /// <summary />
    private static AzVirtualNetwork Network( params AzSubnet[] subnets )
    {
        var network = Resource<AzVirtualNetwork>( "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/virtualNetworks/vnet-one", "Microsoft.Network/virtualNetworks" );

        network.AddressPrefixes = [ "10.0.0.0/16" ];
        network.DnsServers = [];
        network.Subnets = [ .. subnets ];

        return network;
    }


    /// <summary />
    private static AzSubnet Subnet( string? nsgId, string? routeTableId )
    {
        return new AzSubnet
        {
            Id = "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/virtualNetworks/vnet-one/subnets/snet-one",
            Name = "snet-one",
            Type = "Microsoft.Network/virtualNetworks/subnets",
            AddressPrefix = "10.0.1.0/24",
            NetworkSecurityGroupId = nsgId,
            RouteTableId = routeTableId,
        };
    }


    /// <summary />
    [Fact]
    public void Subnet_ReferencesAreResolved()
    {
        var subnet = Subnet( NsgId, RouteTableId );
        var network = Network( subnet );

        var nsg = Resource<AzNetworkSecurityGroup>( NsgId, "Microsoft.Network/networkSecurityGroups" );
        var routeTable = Resource<AzRouteTable>( RouteTableId, "Microsoft.Network/routeTables" );

        Linker.Link( [ network, nsg, routeTable ] );

        Assert.Same( nsg, subnet.NetworkSecurityGroup );
        Assert.Same( routeTable, subnet.RouteTable );
    }


    /// <summary />
    /// <remarks>
    /// Resource identifiers are not case sensitive, and Resource Graph is not
    /// consistent about the casing it returns them in.
    /// </remarks>
    [Fact]
    public void Subnet_ReferencesResolveIgnoringCase()
    {
        var subnet = Subnet( NsgId.ToUpperInvariant(), null );
        var network = Network( subnet );

        var nsg = Resource<AzNetworkSecurityGroup>( NsgId, "Microsoft.Network/networkSecurityGroups" );

        Linker.Link( [ network, nsg ] );

        Assert.Same( nsg, subnet.NetworkSecurityGroup );
    }


    /// <summary />
    /// <remarks>
    /// The target may live in a subscription which was not read.
    /// </remarks>
    [Fact]
    public void Subnet_UnresolvableReference_IsLeftNull()
    {
        var subnet = Subnet( NsgId, RouteTableId );
        var network = Network( subnet );

        Linker.Link( [ network ] );

        Assert.Null( subnet.NetworkSecurityGroup );
        Assert.Null( subnet.RouteTable );
    }


    /// <summary />
    [Fact]
    public void Subnet_WithoutReferences_IsLeftNull()
    {
        var subnet = Subnet( null, null );
        var network = Network( subnet );

        var nsg = Resource<AzNetworkSecurityGroup>( NsgId, "Microsoft.Network/networkSecurityGroups" );

        Linker.Link( [ network, nsg ] );

        Assert.Null( subnet.NetworkSecurityGroup );
        Assert.Null( subnet.RouteTable );
    }


    /// <summary />
    /// <remarks>
    /// Guards the reconciliation which makes the linker work at all: were the
    /// private endpoint mapped onto the base class rather than
    /// <see cref="AzPrivateEndpoint" />, this would silently resolve to null.
    /// </remarks>
    [Fact]
    public void KeyVault_PrivateEndpointIsResolved()
    {
        var vault = Resource<AzKeyVault>( "/subscriptions/s/resourceGroups/rg/providers/Microsoft.KeyVault/vaults/kv-one", "Microsoft.KeyVault/vaults" );

        vault.Sku = "standard";
        vault.PrivateEndpointId = EndpointId;

        var endpoint = Resource<AzPrivateEndpoint>( EndpointId, "Microsoft.Network/privateEndpoints" );

        Linker.Link( [ vault, endpoint ] );

        Assert.Same( endpoint, vault.PrivateEndpoint );
    }


    /// <summary />
    /// <remarks>
    /// A reference which resolves to something of the wrong type is a mapping
    /// fault, and must not be forced into place.
    /// </remarks>
    [Fact]
    public void ReferenceOfWrongType_IsLeftNull()
    {
        var subnet = Subnet( NsgId, null );
        var network = Network( subnet );

        var impostor = Resource<AzStorageAccount>( NsgId, "Microsoft.Storage/storageAccounts" );

        Linker.Link( [ network, impostor ] );

        Assert.Null( subnet.NetworkSecurityGroup );
    }


    /// <summary />
    /// <remarks>
    /// The linked graph is serialized inline, so it must stay acyclic.
    /// </remarks>
    [Fact]
    public void LinkedGraph_Serializes()
    {
        var subnet = Subnet( NsgId, RouteTableId );
        var network = Network( subnet );

        var nsg = Resource<AzNetworkSecurityGroup>( NsgId, "Microsoft.Network/networkSecurityGroups" );
        var routeTable = Resource<AzRouteTable>( RouteTableId, "Microsoft.Network/routeTables" );

        Linker.Link( [ network, nsg, routeTable ] );

        var json = JsonSerializer.Serialize<List<AzResource>>( [ network, nsg, routeTable ] );

        Assert.Contains( "nsg-one", json );
        Assert.Contains( "AzVirtualNetwork", json );
    }
}
