using Lefty.Navy.Azure;
using Lefty.Navy.Model;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lefty.Navy.Tests;

/// <summary />
public class NetworkLayoutTest
{
    private const string Providers = "/subscriptions/s/resourceGroups/rg-one/providers";
    private const string NetworkId = "/subscriptions/s/resourceGroups/rg-net/providers/Microsoft.Network/virtualNetworks/vnet-one";
    private const string SubnetId = NetworkId + "/subnets/snet-one";
    private const string OtherSubnetId = NetworkId + "/subnets/snet-two";
    private const string MachineId = Providers + "/Microsoft.Compute/virtualMachines/vm-one";
    private const string EndpointId = Providers + "/Microsoft.Network/privateEndpoints/pe-one";
    private const string VaultId = Providers + "/Microsoft.KeyVault/vaults/kv-one";

    private static readonly NetworkLayout Layout = new( NullLogger<NetworkLayout>.Instance );


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
    private static AzVirtualNetwork Network()
    {
        var network = Resource<AzVirtualNetwork>( NetworkId, "Microsoft.Network/virtualNetworks" );

        network.AddressPrefixes = [ "10.0.0.0/16" ];
        network.DnsServers = [];
        network.Subnets =
        [
            Subnet( SubnetId, "10.0.1.0/24" ),
            Subnet( OtherSubnetId, "10.0.2.0/24" ),
        ];

        return network;
    }


    /// <summary />
    private static AzSubnet Subnet( string id, string prefix )
    {
        return new AzSubnet
        {
            Id = id,
            Name = id.Split( '/' ).Last(),
            Type = "Microsoft.Network/virtualNetworks/subnets",
            AddressPrefix = prefix,
        };
    }


    /// <summary />
    private static AzNetworkInterface Nic( string name, string address, string? machineId = null, string? endpointId = null )
    {
        var id = Providers + "/Microsoft.Network/networkInterfaces/" + name;

        var nic = Resource<AzNetworkInterface>( id, "Microsoft.Network/networkInterfaces" );

        nic.VirtualMachineId = machineId;
        nic.PrivateEndpointId = endpointId;
        nic.IPConfigurations =
        [
            new AzNetworkInterfaceIPConfiguration
            {
                Id = id + "/ipConfigurations/one",
                Name = "one",
                Type = "Microsoft.Network/networkInterfaces/ipConfigurations",
                PrivateIPAddress = address,
                SubnetId = SubnetId,
            },
        ];

        return nic;
    }


    /// <summary>
    /// One resource group holding the network, so that every subnet reference
    /// resolves, plus whatever the test is about.
    /// </summary>
    private static List<AzResourceGroup> Groups( params AzResource[] resources )
    {
        var group = new AzResourceGroup
        {
            Id = "/subscriptions/s/resourceGroups/rg-one",
            Name = "rg-one",
            Tags = new Dictionary<string, string> { [ "Environment" ] = "prod" },
            Resources = [ Network(), .. resources ],
        };

        new ResourceLinker( NullLogger<ResourceLinker>.Instance ).Link( group.Resources! );

        return [ group ];
    }


    /// <summary />
    private static AzPrivateEndpoint Endpoint( AzNetworkInterface nic, string? serviceId = null )
    {
        var endpoint = Resource<AzPrivateEndpoint>( EndpointId, "Microsoft.Network/privateEndpoints" );

        endpoint.SubnetId = SubnetId;
        endpoint.NetworkInterfaceIds = [ nic.Id ];

        if ( serviceId != null )
        {
            endpoint.Connections =
            [
                new AzPrivateEndpointConnection
                {
                    Id = EndpointId + "/privateLinkServiceConnections/one",
                    Name = "one",
                    Type = "Microsoft.Network/privateEndpoints/privateLinkServiceConnections",
                    PrivateLinkServiceId = serviceId,
                    Status = "Approved",
                },
            ];
        }

        return endpoint;
    }


    /// <summary>
    /// A machine reports its addresses only through its interfaces, and it is
    /// the machine which makes use of the address.
    /// </summary>
    [Fact]
    public void VirtualMachine_MakesUseOfTheAddressOfItsInterface()
    {
        var machine = Resource<AzVirtualMachine>( MachineId, "Microsoft.Compute/virtualMachines" );
        var nic = Nic( "nic-one", "10.0.1.4", machineId: MachineId );

        machine.NetworkInterfaceIds = [ nic.Id ];

        var row = Assert.Single( Layout.Build( Groups( machine, nic ) ) );

        Assert.Equal( "vnet-one", row.Vnet );
        Assert.Equal( "snet-one", row.Snet );
        Assert.Equal( "10.0.1.4", row.Ip );
        Assert.Equal( "vm-one", row.Resource );
        Assert.Null( row.PrivateEndpoint );
        Assert.Equal( "nic-one", row.NetworkInterface );
        Assert.Equal( "rg-one", row.ResourceGroup );
    }


    /// <summary>
    /// The address of an endpoint belongs to the resource it fronts: a vault
    /// has no address of its own, and is what the address is reached for.
    /// </summary>
    [Fact]
    public void PrivateEndpoint_IsReportedUnderTheResourceItFronts()
    {
        var nic = Nic( "nic-pe", "10.0.1.5", endpointId: EndpointId );
        var endpoint = Endpoint( nic, VaultId );

        var vault = Resource<AzKeyVault>( VaultId, "Microsoft.KeyVault/vaults" );

        vault.Sku = "standard";
        vault.PrivateEndpointId = EndpointId;

        var row = Assert.Single( Layout.Build( Groups( vault, endpoint, nic ) ) );

        Assert.Equal( "10.0.1.5", row.Ip );
        Assert.Equal( "kv-one", row.Resource );
        Assert.Equal( "pe-one", row.PrivateEndpoint );
        Assert.Equal( "nic-pe", row.NetworkInterface );
    }


    /// <summary>
    /// The resource an endpoint fronts is named by the connection even when it
    /// is not part of this environment, and so was never walked.
    /// </summary>
    [Fact]
    public void PrivateEndpoint_WhoseResourceIsElsewhere_IsNamedFromItsConnection()
    {
        var nic = Nic( "nic-pe", "10.0.1.5", endpointId: EndpointId );
        var endpoint = Endpoint( nic, "/subscriptions/other/resourceGroups/rg/providers/Microsoft.KeyVault/vaults/kv-far" );

        var row = Assert.Single( Layout.Build( Groups( endpoint, nic ) ) );

        Assert.Equal( "kv-far", row.Resource );
        Assert.Equal( "pe-one", row.PrivateEndpoint );
    }


    /// <summary>
    /// An endpoint which names no connection is all that is known of the
    /// address, and stands for the resource itself.
    /// </summary>
    [Fact]
    public void PrivateEndpoint_WithoutAConnection_IsNamedAfterItself()
    {
        var nic = Nic( "nic-pe", "10.0.1.5", endpointId: EndpointId );
        var endpoint = Endpoint( nic );

        var row = Assert.Single( Layout.Build( Groups( endpoint, nic ) ) );

        Assert.Equal( "pe-one", row.Resource );
        Assert.Equal( "pe-one", row.PrivateEndpoint );
        Assert.Equal( "nic-pe", row.NetworkInterface );
    }


    /// <summary>
    /// The interface of a machine which is not part of this environment is
    /// still reported, under the name the identifier gives it.
    /// </summary>
    [Fact]
    public void Interface_WhoseOwnerIsElsewhere_IsNamedAfterIt()
    {
        var nic = Nic( "nic-one", "10.0.1.4", machineId: MachineId );

        var row = Assert.Single( Layout.Build( Groups( nic ) ) );

        Assert.Equal( "vm-one", row.Resource );
        Assert.Equal( "nic-one", row.NetworkInterface );
    }


    /// <summary />
    [Fact]
    public void Interface_WhichBelongsToNothing_IsNamedAfterItself()
    {
        var nic = Nic( "nic-loose", "10.0.1.9" );

        var row = Assert.Single( Layout.Build( Groups( nic ) ) );

        Assert.Equal( "nic-loose", row.Resource );
        Assert.Equal( "nic-loose", row.NetworkInterface );
    }


    /// <summary>
    /// A set describes the subnet its interfaces are created in, and the
    /// instances which hold the addresses are not inventoried.
    /// </summary>
    [Fact]
    public void ScaleSet_IsReportedWithItsInterfaceTemplate()
    {
        var scaleSet = Resource<AzVirtualMachineScaleSet>( Providers + "/Microsoft.Compute/virtualMachineScaleSets/vmss-one", "Microsoft.Compute/virtualMachineScaleSets" );

        scaleSet.NetworkInterfaces =
        [
            new AzScaleSetNetworkInterface
            {
                Name = "nic-template",
                IPConfigurations =
                [
                    new AzScaleSetIPConfiguration
                    {
                        Name = "ipconfig1",
                        SubnetId = OtherSubnetId,
                    },
                ],
            },
        ];

        var row = Assert.Single( Layout.Build( Groups( scaleSet ) ) );

        Assert.Equal( "snet-two", row.Snet );
        Assert.Null( row.Ip );
        Assert.Equal( "vmss-one", row.Resource );
        Assert.Equal( "nic-template", row.NetworkInterface );
    }


    /// <summary>
    /// A site integrated into a virtual network holds no address of its own,
    /// and is reported for the subnet it occupies.
    /// </summary>
    [Fact]
    public void AppService_IsReportedWithoutAnAddress()
    {
        var site = Resource<AzAppService>( Providers + "/Microsoft.Web/sites/app-one", "Microsoft.Web/sites" );

        site.VirtualNetworkSubnetId = OtherSubnetId;

        var row = Assert.Single( Layout.Build( Groups( site ) ) );

        Assert.Equal( "snet-two", row.Snet );
        Assert.Null( row.Ip );
        Assert.Equal( "app-one", row.Resource );
    }


    /// <summary>
    /// A resource which is in no subnet is not part of the network layout.
    /// </summary>
    [Fact]
    public void ResourceWithoutNetwork_IsPassedOver()
    {
        var account = Resource<AzStorageAccount>( Providers + "/Microsoft.Storage/storageAccounts/stone", "Microsoft.Storage/storageAccounts" );

        Assert.Empty( Layout.Build( Groups( account ) ) );
    }


    /// <summary>
    /// Addresses are ordered as the subnet hands them out, which is not the
    /// order they sort in as text.
    /// </summary>
    [Fact]
    public void Addresses_AreOrderedNumerically()
    {
        var rows = Layout.Build( Groups(
            Nic( "nic-a", "10.0.1.10" ),
            Nic( "nic-b", "10.0.1.9" ),
            Nic( "nic-c", "10.0.1.100" ) ) );

        Assert.Equal( new[] { "10.0.1.9", "10.0.1.10", "10.0.1.100" }, rows.Select( x => x.Ip ) );
    }


    /// <summary>
    /// A cache injected into a subnet reports its address itself, rather than
    /// through an interface.
    /// </summary>
    [Fact]
    public void CacheForRedis_ReportsItsStaticAddress()
    {
        var cache = Resource<AzCacheForRedis>( Providers + "/Microsoft.Cache/redis/redis-one", "Microsoft.Cache/redis" );

        cache.SubnetId = SubnetId;
        cache.StaticIP = "10.0.1.20";

        var row = Assert.Single( Layout.Build( Groups( cache ) ) );

        Assert.Equal( "10.0.1.20", row.Ip );
        Assert.Equal( "redis-one", row.Resource );
    }


    /// <summary>
    /// A subnet in a network which was not read is still named, from the
    /// identifier the resource holds.
    /// </summary>
    [Fact]
    public void SubnetOfAnotherSubscription_IsNamedFromItsIdentifier()
    {
        var cache = Resource<AzCacheForRedis>( Providers + "/Microsoft.Cache/redis/redis-one", "Microsoft.Cache/redis" );

        cache.SubnetId = "/subscriptions/other/resourceGroups/rg/providers/Microsoft.Network/virtualNetworks/vnet-far/subnets/snet-far";
        cache.StaticIP = "10.9.1.20";

        var row = Assert.Single( Layout.Build( Groups( cache ) ) );

        Assert.Equal( "vnet-far", row.Vnet );
        Assert.Equal( "snet-far", row.Snet );
    }
}
