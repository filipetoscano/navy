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
/// Azure describes several of these relationships from both ends, and in each
/// case only one end is resolved here. A private endpoint and a virtual machine
/// each hold their network interfaces, so
/// <see cref="AzNetworkInterface.PrivateEndpointId" /> and
/// <see cref="AzNetworkInterface.VirtualMachineId" /> stay identifiers; and a
/// resource such as a key vault or a storage account holds
/// the endpoints which front it, so
/// <see cref="AzPrivateEndpointConnection.PrivateLinkServiceId" /> stays an
/// identifier too. Resolving either would close a loop. The same holds for a
/// route table, whose subnets stay identifiers because a subnet already holds
/// its table; for a network security group, whose subnets and network
/// interfaces stay identifiers for that same reason; for a SQL database, whose
/// server stays an identifier because the server holds its databases; and for a
/// NetApp volume and capacity pool, whose parents stay identifiers because each
/// parent holds its children.
/// </para>
/// <para>
/// A second reason to leave a reference unresolved is weight rather than
/// cycles. What an alert rule watches, and which backend pool a scale set is
/// placed into, stay identifiers because a subscription holds far more rules
/// and scale sets than it holds watched resources and balancers: resolving
/// them would write a copy of the target under each one.
/// </para>
/// <para>
/// Geo-replicated Redis caches name each other, so
/// <see cref="AzCacheForRedis.LinkedServerIds" /> stays a list of identifiers:
/// resolving it would close a loop between the pair.
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

            if ( resource is AzSqlServer server )
            {
                server.PrimaryUserAssignedIdentity = Lookup<AzManagedIdentity>( byId, server.PrimaryUserAssignedIdentityId );

                foreach ( var id in server.PrivateEndpointIds )
                {
                    var linked = Lookup<AzPrivateEndpoint>( byId, id );

                    if ( linked != null )
                        server.PrivateEndpoints.Add( linked );
                }
            }

            if ( resource is AzDiskEncryptionSet encryptionSet )
                encryptionSet.KeyVault = Lookup<AzKeyVault>( byId, encryptionSet.KeyVaultId );

            if ( resource is AzKubernetesService cluster )
            {
                cluster.DiskEncryptionSet = Lookup<AzDiskEncryptionSet>( byId, cluster.DiskEncryptionSetId );
                cluster.KubeletIdentity = Lookup<AzManagedIdentity>( byId, cluster.KubeletIdentityId );

                foreach ( var nodePool in cluster.NodePools )
                    nodePool.Subnet = SubnetLookup( subnetsById, nodePool.SubnetId );
            }

            if ( resource is AzVirtualMachineScaleSet scaleSet )
            {
                scaleSet.DiskEncryptionSet = Lookup<AzDiskEncryptionSet>( byId, scaleSet.DiskEncryptionSetId );

                foreach ( var template in scaleSet.NetworkInterfaces )
                {
                    template.NetworkSecurityGroup = Lookup<AzNetworkSecurityGroup>( byId, template.NetworkSecurityGroupId );

                    foreach ( var configuration in template.IPConfigurations )
                        configuration.Subnet = SubnetLookup( subnetsById, configuration.SubnetId );
                }
            }

            if ( resource is AzLoadBalancer balancer )
            {
                foreach ( var frontend in balancer.FrontendIPConfigurations )
                    frontend.Subnet = SubnetLookup( subnetsById, frontend.SubnetId );
            }

            if ( resource is AzVirtualMachine machine )
            {
                machine.DiskEncryptionSet = Lookup<AzDiskEncryptionSet>( byId, machine.DiskEncryptionSetId );

                foreach ( var id in machine.NetworkInterfaceIds )
                {
                    var attached = Lookup<AzNetworkInterface>( byId, id );

                    if ( attached != null )
                        machine.NetworkInterfaces.Add( attached );
                }
            }

            if ( resource is AzWebSite site )
            {
                site.Subnet = SubnetLookup( subnetsById, site.VirtualNetworkSubnetId );

                foreach ( var id in site.PrivateEndpointIds )
                {
                    var linked = Lookup<AzPrivateEndpoint>( byId, id );

                    if ( linked != null )
                        site.PrivateEndpoints.Add( linked );
                }
            }

            if ( resource is AzCacheForRedis cache )
            {
                cache.Subnet = SubnetLookup( subnetsById, cache.SubnetId );

                foreach ( var id in cache.PrivateEndpointIds )
                {
                    var linked = Lookup<AzPrivateEndpoint>( byId, id );

                    if ( linked != null )
                        cache.PrivateEndpoints.Add( linked );
                }
            }

            if ( resource is AzEventHubNamespace space )
            {
                foreach ( var id in space.PrivateEndpointIds )
                {
                    var linked = Lookup<AzPrivateEndpoint>( byId, id );

                    if ( linked != null )
                        space.PrivateEndpoints.Add( linked );
                }
            }

            if ( resource is AzNetAppAccount netApp )
            {
                netApp.EncryptionKeyVault = Lookup<AzKeyVault>( byId, netApp.EncryptionKeyVaultId );
                netApp.EncryptionIdentity = Lookup<AzManagedIdentity>( byId, netApp.EncryptionIdentityId );
            }

            /*
             * NetApp reports its three levels flat, each naming its parent in
             * its own identifier and none of them listing its children. Both
             * relationships are assembled from the child, as a SQL database is
             * attached to its server, which puts the hierarchy back together:
             * account, capacity pool, volume.
             */
            if ( resource is AzNetAppCapacityPool pool && pool.NetAppAccountId != null )
            {
                if ( byId.TryGetValue( pool.NetAppAccountId, out var parent ) == true && parent is AzNetAppAccount host )
                    host.CapacityPools.Add( pool );
                else
                    _logger.LogDebug( "capacity pool {Id} names a NetApp account which was not found", pool.Id );
            }

            if ( resource is AzNetAppVolume volume )
            {
                volume.Subnet = SubnetLookup( subnetsById, volume.SubnetId );

                if ( volume.CapacityPoolId != null )
                {
                    if ( byId.TryGetValue( volume.CapacityPoolId, out var parent ) == true && parent is AzNetAppCapacityPool host )
                        host.Volumes.Add( volume );
                    else
                        _logger.LogDebug( "volume {Id} names a capacity pool which was not found", volume.Id );
                }
            }

            /*
             * Every kind of alert rule names the groups it notifies, and the
             * same group is normally named by a great many rules.
             */
            if ( resource is AzActivityLogAlertRule activityRule )
                ActionGroups( byId, activityRule.ActionGroupIds, activityRule.ActionGroups );

            if ( resource is AzMetricAlertRule metricRule )
                ActionGroups( byId, metricRule.ActionGroupIds, metricRule.ActionGroups );

            if ( resource is AzSmartDetectorAlertRule detectorRule )
                ActionGroups( byId, detectorRule.ActionGroupIds, detectorRule.ActionGroups );

            /*
             * A database names its server, rather than a server listing its
             * databases, so the relationship is assembled from this side.
             */
            if ( resource is AzSqlDatabase database && database.ServerId != null )
            {
                if ( byId.TryGetValue( database.ServerId, out var parent ) == true && parent is AzSqlServer host )
                    host.Databases.Add( database );
                else
                    _logger.LogDebug( "database {Id} names a server which was not found", database.Id );
            }
        }
    }


    /// <summary />
    private void ActionGroups( Dictionary<string, AzResource> byId, List<string> ids, List<AzActionGroup> groups )
    {
        foreach ( var id in ids )
        {
            var group = Lookup<AzActionGroup>( byId, id );

            if ( group != null )
                groups.Add( group );
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
