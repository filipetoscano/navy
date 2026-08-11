using Lefty.Navy.Model;
using Microsoft.Extensions.Logging;

namespace Lefty.Navy.Azure;

/// <summary />
/// <remarks>
/// Resolves the references which resources hold to one another: Resource Graph
/// returns them as resource identifiers, and this turns each one into the
/// object it points at.
/// <para>
/// The resulting object graph is acyclic, because no model class holds a
/// reference back to the resource which points at it. That matters, because the
/// navigation properties are serialized inline: introducing a back-reference
/// would turn the graph cyclic and make serialization fail on depth.
/// </para>
/// <para>
/// Azure describes two of these relationships from both ends, and in each case
/// only one end is resolved here. A private endpoint holds its network
/// interfaces, so <see cref="AzNetworkInterface.PrivateEndpointId" /> stays an
/// identifier; and a resource such as a key vault or a storage account holds
/// the endpoints which front it, so
/// <see cref="AzPrivateEndpointConnection.PrivateLinkServiceId" /> stays an
/// identifier too. Resolving either would close a loop.
/// </para>
/// <para>
/// A resolved object is shared rather than copied, so a subnet reached through
/// an API Management service is the same instance as the one held by its
/// virtual network. Serialization writes it out at both places.
/// </para>
/// </remarks>
public class ResourceLinker
{
    private readonly ILogger _logger;


    /// <summary />
    public ResourceLinker( ILogger logger )
    {
        _logger = logger;
    }


    /// <summary />
    /// <param name="resources">
    /// Every resource in the subscription, flattened. References to resources
    /// outside this set cannot be resolved and are left null.
    /// </param>
    public void Link( List<AzResource> resources )
    {
        var byId = new Dictionary<string, AzResource>( StringComparer.OrdinalIgnoreCase );
        var subnetsById = new Dictionary<string, AzSubnet>( StringComparer.OrdinalIgnoreCase );

        foreach ( var resource in resources )
        {
            if ( byId.TryAdd( resource.Id, resource ) == false )
                _logger.LogWarning( "resource {Id} was returned more than once", resource.Id );

            /*
             * Subnets are referenced from outside the network which contains
             * them, so they are indexed alongside the resources themselves.
             */
            if ( resource is AzVirtualNetwork network )
            {
                foreach ( var subnet in network.Subnets )
                    subnetsById.TryAdd( subnet.Id, subnet );
            }
        }

        foreach ( var resource in resources )
        {
            if ( resource is AzVirtualNetwork network )
            {
                foreach ( var subnet in network.Subnets )
                {
                    subnet.NetworkSecurityGroup = Lookup<AzNetworkSecurityGroup>( byId, subnet.NetworkSecurityGroupId );
                    subnet.RouteTable = Lookup<AzRouteTable>( byId, subnet.RouteTableId );
                }
            }

            if ( resource is AzKeyVault vault )
                vault.PrivateEndpoint = Lookup<AzPrivateEndpoint>( byId, vault.PrivateEndpointId );

            if ( resource is AzApiManagement service )
                service.Subnet = SubnetLookup( subnetsById, service.SubnetId );

            if ( resource is AzNetworkInterface nic )
            {
                nic.NetworkSecurityGroup = Lookup<AzNetworkSecurityGroup>( byId, nic.NetworkSecurityGroupId );

                foreach ( var configuration in nic.IPConfigurations )
                    configuration.Subnet = SubnetLookup( subnetsById, configuration.SubnetId );
            }

            if ( resource is AzPrivateEndpoint endpoint )
            {
                endpoint.Subnet = SubnetLookup( subnetsById, endpoint.SubnetId );

                foreach ( var id in endpoint.NetworkInterfaceIds )
                {
                    var attached = Lookup<AzNetworkInterface>( byId, id );

                    if ( attached != null )
                        endpoint.NetworkInterfaces.Add( attached );
                }
            }

            if ( resource is AzStorageAccount account )
            {
                foreach ( var id in account.PrivateEndpointIds )
                {
                    var linked = Lookup<AzPrivateEndpoint>( byId, id );

                    if ( linked != null )
                        account.PrivateEndpoints.Add( linked );
                }
            }
        }
    }


    /// <summary />
    private AzSubnet? SubnetLookup( Dictionary<string, AzSubnet> subnetsById, string? id )
    {
        if ( string.IsNullOrEmpty( id ) == true )
            return null;

        if ( subnetsById.TryGetValue( id, out var subnet ) == false )
        {
            _logger.LogDebug( "subnet {Id} was not found in this subscription", id );

            return null;
        }

        return subnet;
    }


    /// <summary />
    private T? Lookup<T>( Dictionary<string, AzResource> byId, string? id )
        where T : AzResource
    {
        if ( string.IsNullOrEmpty( id ) == true )
            return null;

        if ( byId.TryGetValue( id, out var resource ) == false )
        {
            /*
             * Entirely expected: the target may live in another subscription,
             * or may have been deleted since Resource Graph last indexed it.
             */
            _logger.LogDebug( "reference {Id} was not found in this subscription", id );

            return null;
        }

        if ( resource is T typed )
            return typed;

        _logger.LogDebug( "reference {Id} resolved to {Actual}, expected {Expected}", id, resource.GetType().Name, typeof( T ).Name );

        return null;
    }
}
