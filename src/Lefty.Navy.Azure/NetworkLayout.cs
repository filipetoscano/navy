using Lefty.Navy.Model;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;

namespace Lefty.Navy.Azure;

/// <summary />
/// <remarks>
/// Reports which subnet, and which address within it, each resource of an
/// environment occupies. The inventory must have been stitched: an address is
/// only worth reporting next to the subnet it belongs to, and a subnet is
/// reached by resolving the identifier which the resource holds.
/// <para>
/// An address is reported under the resource which makes use of it, which is
/// seldom the resource which holds it. Azure records an address on a network
/// interface, and an interface belongs either to a virtual machine or to a
/// private endpoint; an endpoint in turn fronts a resource which has no
/// address of its own, such as a key vault or a storage account. The address
/// of that endpoint is therefore reported as the vault's, with the endpoint
/// and the interface named alongside it, so that the row reads as the path by
/// which the address is reached.
/// </para>
/// <para>
/// Every interface is reported once, claimed by whichever resource explains
/// the address best: the resource an endpoint fronts, then the machine or
/// endpoint which owns the interface, and last the interface itself, for one
/// which belongs to nothing or to something outside this environment.
/// </para>
/// <para>
/// A resource which sits in a subnet without an address of its own — an app
/// service integrated into a virtual network, a Kubernetes node pool, a scale
/// set — is reported with no address rather than left out, because what
/// occupies a subnet is the point of the layout.
/// </para>
/// </remarks>
public class NetworkLayout
{
    private readonly ILogger _logger;


    /// <summary />
    public NetworkLayout( ILogger logger )
    {
        _logger = logger;
    }


    /// <summary />
    /// <param name="groups">
    /// Resource groups of one environment. A subnet which those resources sit
    /// in is reported wherever it lives, including a resource group which is
    /// not among these, and so is a private endpoint which fronts one of them.
    /// </param>
    public List<NetworkRow> Build( IEnumerable<AzResourceGroup> groups )
    {
        var list = groups.ToList();
        var rows = new List<NetworkRow>();

        var claims = new Dictionary<string, InterfaceClaim>( StringComparer.OrdinalIgnoreCase );
        var fronted = new HashSet<string>( StringComparer.OrdinalIgnoreCase );

        void Walk( Action<AzResourceGroup, AzResource> visit )
        {
            foreach ( var group in list )
            {
                foreach ( var resource in group.Resources ?? [] )
                    visit( group, resource );
            }
        }


        /*
         * Claimed in order of how well the claimant accounts for the address,
         * because an interface is reported once and the first claim on it
         * stands.
         */
        Walk( ( group, resource ) => Fronting( rows, claims, fronted, group, resource ) );
        Walk( ( group, resource ) => Owning( rows, claims, fronted, group, resource ) );
        Walk( ( group, resource ) => Loose( claims, group, resource ) );

        foreach ( var claim in claims.Values )
        {
            foreach ( var configuration in claim.Interface.IPConfigurations )
                Emit( rows, claim.Occupant, configuration.Subnet, configuration.SubnetId, configuration.PrivateIPAddress );
        }


        /*
         * Resources which report an address, or a subnet, without an interface
         * standing between them and it.
         */
        Walk( ( group, resource ) => Direct( rows, group, resource ) );

        _logger.LogDebug( "{Rows} address(es) across {Groups} resource group(s)", rows.Count, list.Count );

        return [ .. rows
            .OrderBy( x => x.Vnet, StringComparer.OrdinalIgnoreCase )
            .ThenBy( x => x.Snet, StringComparer.OrdinalIgnoreCase )
            .ThenBy( x => Ordinal( x.Ip ) )
            .ThenBy( x => x.Resource, StringComparer.OrdinalIgnoreCase ) ];
    }


    /// <summary>
    /// Addresses which a resource is reached on through the private endpoints
    /// which front it.
    /// </summary>
    private static void Fronting( List<NetworkRow> rows, Dictionary<string, InterfaceClaim> claims, HashSet<string> fronted, AzResourceGroup group, AzResource resource )
    {
        foreach ( var endpoint in EndpointsOf( resource ) )
        {
            /*
             * Noted so that the endpoint, which is commonly a resource of this
             * environment in its own right, is not reported a second time under
             * its own name.
             */
            fronted.Add( endpoint.Id );

            Endpoint( rows, claims, group, resource.Name, endpoint );
        }
    }


    /// <summary>
    /// Addresses of the interfaces which a machine, or an endpoint whose
    /// resource is not part of this environment, owns.
    /// </summary>
    private static void Owning( List<NetworkRow> rows, Dictionary<string, InterfaceClaim> claims, HashSet<string> fronted, AzResourceGroup group, AzResource resource )
    {
        if ( resource is AzVirtualMachine machine )
        {
            foreach ( var nic in machine.NetworkInterfaces )
                Claim( claims, nic, Of( group, machine.Name, null, nic ) );
        }

        /*
         * The resource an endpoint fronts is named by the connection rather
         * than resolved, because that resource commonly points back at the
         * endpoint and following both directions would close a loop.
         */
        if ( resource is AzPrivateEndpoint endpoint && fronted.Contains( endpoint.Id ) == false )
            Endpoint( rows, claims, group, ServiceOf( endpoint ) ?? endpoint.Name, endpoint );
    }


    /// <summary>
    /// An interface which nothing above laid claim to: it belongs to nothing,
    /// or to a resource outside this environment, which the identifier it holds
    /// still names.
    /// </summary>
    private static void Loose( Dictionary<string, InterfaceClaim> claims, AzResourceGroup group, AzResource resource )
    {
        if ( resource is not AzNetworkInterface nic )
            return;

        if ( string.IsNullOrEmpty( nic.VirtualMachineId ) == false )
        {
            Claim( claims, nic, Of( group, NameOf( nic.VirtualMachineId )!, null, nic ) );

            return;
        }

        if ( string.IsNullOrEmpty( nic.PrivateEndpointId ) == false )
        {
            var name = NameOf( nic.PrivateEndpointId )!;

            Claim( claims, nic, new Occupant
            {
                ResourceGroup = group.Name,
                Resource = name,
                PrivateEndpoint = name,
                NetworkInterface = nic.Name,
            } );

            return;
        }

        Claim( claims, nic, Of( group, nic.Name, null, nic ) );
    }


    /// <summary>
    /// Addresses which a resource reports itself, rather than through an
    /// interface.
    /// </summary>
    private static void Direct( List<NetworkRow> rows, AzResourceGroup group, AzResource resource )
    {
        if ( resource is AzApiManagement service )
            Emit( rows, Of( group, service.Name ), service.Subnet, service.SubnetId, service.PrivateIPAddresses );

        if ( resource is AzCacheForRedis cache )
            Emit( rows, Of( group, cache.Name ), cache.Subnet, cache.SubnetId, cache.StaticIP );

        if ( resource is AzNetAppVolume volume )
            Emit( rows, Of( group, volume.Name ), volume.Subnet, volume.SubnetId, volume.MountTargetIPAddresses );

        /*
         * Only the frontends placed inside a subnet: a public frontend has no
         * address in this subscription's address space.
         */
        if ( resource is AzLoadBalancer balancer )
        {
            foreach ( var frontend in balancer.FrontendIPConfigurations )
                Emit( rows, Of( group, $"{balancer.Name}/{frontend.Name}" ), frontend.Subnet, frontend.SubnetId, frontend.PrivateIPAddress );
        }

        /*
         * Regional virtual network integration: the site holds no address of
         * its own, but does hold a delegated subnet.
         */
        if ( resource is AzWebSite site )
            Emit( rows, Of( group, site.Name ), site.Subnet, site.VirtualNetworkSubnetId, default( string? ) );

        /*
         * The nodes themselves are scale set instances, which Resource Graph
         * does not report, so the pool is reported instead.
         */
        if ( resource is AzKubernetesService cluster )
        {
            foreach ( var pool in cluster.NodePools )
                Emit( rows, Of( group, $"{cluster.Name}/{pool.Name}" ), pool.Subnet, pool.SubnetId, default( string? ) );
        }

        /*
         * An instance holds the address, and is not inventoried: what the set
         * describes is the subnet each of its interfaces is created in.
         */
        if ( resource is AzVirtualMachineScaleSet scaleSet )
        {
            foreach ( var template in scaleSet.NetworkInterfaces )
            {
                var occupant = new Occupant
                {
                    ResourceGroup = group.Name,
                    Resource = scaleSet.Name,
                    NetworkInterface = template.Name,
                };

                foreach ( var configuration in template.IPConfigurations )
                    Emit( rows, occupant, configuration.Subnet, configuration.SubnetId, default( string? ) );
            }
        }
    }


    /// <summary>
    /// Claims the interfaces of an endpoint on behalf of the resource it is
    /// reached for.
    /// </summary>
    /// <remarks>
    /// An endpoint whose interfaces were not returned still knows which subnet
    /// it sits in, and the addresses it answers on are repeated in the DNS
    /// configuration it hands out.
    /// </remarks>
    private static void Endpoint( List<NetworkRow> rows, Dictionary<string, InterfaceClaim> claims, AzResourceGroup group, string resource, AzPrivateEndpoint endpoint )
    {
        if ( endpoint.NetworkInterfaces.Count == 0 )
        {
            var addresses = endpoint.CustomDnsConfigs
                .SelectMany( x => x.IPAddresses )
                .Distinct( StringComparer.OrdinalIgnoreCase )
                .ToList();

            Emit( rows, Of( group, resource, endpoint, null ), endpoint.Subnet, endpoint.SubnetId, addresses );

            return;
        }

        foreach ( var nic in endpoint.NetworkInterfaces )
            Claim( claims, nic, Of( group, resource, endpoint, nic ) );
    }


    /// <summary>
    /// Private endpoints which front a resource, for the kinds of resource
    /// which are reached through one.
    /// </summary>
    private static List<AzPrivateEndpoint> EndpointsOf( AzResource resource )
    {
        if ( resource is AzKeyVault vault )
            return vault.PrivateEndpoint == null ? [] : [ vault.PrivateEndpoint ];

        if ( resource is AzStorageAccount account )
            return account.PrivateEndpoints;

        if ( resource is AzSqlServer server )
            return server.PrivateEndpoints;

        if ( resource is AzWebSite site )
            return site.PrivateEndpoints;

        if ( resource is AzCacheForRedis cache )
            return cache.PrivateEndpoints;

        if ( resource is AzEventHubNamespace space )
            return space.PrivateEndpoints;

        return [];
    }


    /// <summary>
    /// Name of the resource which an endpoint is reached for, taken from the
    /// connection it was approved on.
    /// </summary>
    private static string? ServiceOf( AzPrivateEndpoint endpoint )
    {
        foreach ( var connection in endpoint.Connections )
        {
            var name = NameOf( connection.PrivateLinkServiceId );

            if ( name != null )
                return name;
        }

        return null;
    }


    /// <summary />
    private static void Claim( Dictionary<string, InterfaceClaim> claims, AzNetworkInterface nic, Occupant occupant )
    {
        claims.TryAdd( nic.Id, new InterfaceClaim
        {
            Interface = nic,
            Occupant = occupant,
        } );
    }


    /// <summary />
    private static Occupant Of( AzResourceGroup group, string resource )
    {
        return new Occupant
        {
            ResourceGroup = group.Name,
            Resource = resource,
        };
    }


    /// <summary />
    private static Occupant Of( AzResourceGroup group, string resource, AzPrivateEndpoint? endpoint, AzNetworkInterface? nic )
    {
        return new Occupant
        {
            ResourceGroup = group.Name,
            Resource = resource,
            PrivateEndpoint = endpoint?.Name,
            NetworkInterface = nic?.Name,
        };
    }


    /// <summary />
    private static void Emit( List<NetworkRow> rows, Occupant occupant, AzSubnet? subnet, string? subnetId, List<string> addresses )
    {
        if ( addresses.Count == 0 )
        {
            Emit( rows, occupant, subnet, subnetId, default( string? ) );

            return;
        }

        foreach ( var address in addresses )
            Emit( rows, occupant, subnet, subnetId, address );
    }


    /// <summary />
    /// <remarks>
    /// A resource which is in no subnet and holds no address is not part of the
    /// network layout, and is passed over rather than reported as a row of
    /// blanks.
    /// </remarks>
    private static void Emit( List<NetworkRow> rows, Occupant occupant, AzSubnet? subnet, string? subnetId, string? address )
    {
        var id = subnet?.Id ?? subnetId;

        if ( string.IsNullOrEmpty( id ) == true && string.IsNullOrEmpty( address ) == true )
            return;

        rows.Add( new NetworkRow
        {
            Vnet = Segment( id, "virtualNetworks" ),
            Snet = subnet?.Name ?? Segment( id, "subnets" ),
            Ip = string.IsNullOrEmpty( address ) == true ? null : address,
            PrivateEndpoint = occupant.PrivateEndpoint,
            NetworkInterface = occupant.NetworkInterface,
            Resource = occupant.Resource,
            ResourceGroup = occupant.ResourceGroup,
        } );
    }


    /// <summary>
    /// Name a resource identifier ends in, for a reference which is deliberately
    /// left unresolved.
    /// </summary>
    private static string? NameOf( string? id )
    {
        if ( string.IsNullOrEmpty( id ) == true )
            return null;

        return id.Split( '/' ).Last();
    }


    /// <summary>
    /// Segment of a resource identifier which follows the named one, such as
    /// the network in <c>.../virtualNetworks/vnet-one/subnets/snet-one</c>.
    /// </summary>
    private static string? Segment( string? id, string name )
    {
        if ( string.IsNullOrEmpty( id ) == true )
            return null;

        var parts = id.Split( '/' );

        for ( var i = 0; i < parts.Length - 1; i++ )
        {
            if ( string.Equals( parts[ i ], name, StringComparison.OrdinalIgnoreCase ) == true )
                return parts[ i + 1 ];
        }

        return null;
    }


    /// <summary>
    /// Sort key which puts addresses in the order the subnet hands them out,
    /// rather than the order the digits happen to spell.
    /// </summary>
    /// <remarks>
    /// A row without an address is one for a resource which occupies a subnet
    /// without holding an address of its own, and leads the subnet it sits in.
    /// An IPv6 address is not ordered, only kept apart from the IPv4 ones.
    /// </remarks>
    private static long Ordinal( string? address )
    {
        if ( IPAddress.TryParse( address, out var parsed ) == false )
            return -1;

        if ( parsed.AddressFamily != AddressFamily.InterNetwork )
            return long.MaxValue;

        var bytes = parsed.GetAddressBytes();

        return ( (long) bytes[ 0 ] << 24 ) | ( (long) bytes[ 1 ] << 16 ) | ( (long) bytes[ 2 ] << 8 ) | bytes[ 3 ];
    }


    /// <summary>
    /// The resource an address is reported under, and the path by which it is
    /// reached.
    /// </summary>
    private class Occupant
    {
        /// <summary />
        public required string ResourceGroup { get; init; }

        /// <summary />
        public required string Resource { get; init; }

        /// <summary />
        public string? PrivateEndpoint { get; init; }

        /// <summary />
        public string? NetworkInterface { get; init; }
    }


    /// <summary>
    /// An interface, and the resource which laid claim to reporting it.
    /// </summary>
    private class InterfaceClaim
    {
        /// <summary />
        public required AzNetworkInterface Interface { get; init; }

        /// <summary />
        public required Occupant Occupant { get; init; }
    }
}


/// <summary />
public class NetworkRow
{
    /// <summary>
    /// Virtual network which holds the subnet, null when the resource names a
    /// subnet which could not be read.
    /// </summary>
    public string? Vnet { get; set; }

    /// <summary />
    public string? Snet { get; set; }

    /// <summary>
    /// Address the resource holds, null when it occupies the subnet without
    /// holding one.
    /// </summary>
    public string? Ip { get; set; }

    /// <summary>
    /// Name of the Private Endpoint resource.
    /// </summary>
    /// <remarks>
    /// Only for resources which have PE.
    /// </remarks>
    public string? PrivateEndpoint { get; set; }

    /// <summary>
    /// Name of the Network Interface resource.
    /// </summary>
    public string? NetworkInterface { get; set; }

    /// <summary>
    /// Name of the resource which makes use of that IP.
    /// </summary>
    public required string Resource { get; set; }

    /// <summary />
    public required string ResourceGroup { get; set; }
}
