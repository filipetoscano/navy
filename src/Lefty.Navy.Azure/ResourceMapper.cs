using Lefty.Navy.Model;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Lefty.Navy.Azure;

/// <summary />
/// <remarks>
/// Maps a single row of the Resource Graph <c>resources</c> table onto the
/// model. Every resource type declared on <see cref="AzResource" /> is mapped
/// onto its own class, so that references between resources can be resolved by
/// <see cref="ResourceLinker" />; only the types which model properties beyond
/// the common ones have those properties populated. Any type which is not
/// recognized falls back to <see cref="AzResource" /> itself.
/// </remarks>
public class ResourceMapper
{
    /// <summary>
    /// Subnets are returned inline within the virtual network, and so carry no
    /// resource type of their own.
    /// </summary>
    private const string SubnetType = "Microsoft.Network/virtualNetworks/subnets";

    /// <summary>
    /// Hostname configurations are likewise returned inline within the service.
    /// </summary>
    private const string HostnameType = "Microsoft.ApiManagement/service/hostnameConfigurations";

    private readonly ILogger _logger;


    /// <summary />
    public ResourceMapper( ILogger logger )
    {
        _logger = logger;
    }


    /// <summary />
    public AzResource Map( JsonElement row )
    {
        var type = row.Str( "type" ) ?? "";

        return type.ToLowerInvariant() switch
        {
            /*
             * Types which model properties of their own.
             */
            "microsoft.apimanagement/service" => MapApiManagement( row ),
            "microsoft.keyvault/vaults" => MapKeyVault( row ),
            "microsoft.network/virtualnetworks" => MapVirtualNetwork( row ),

            /*
             * Types which are modelled, but carry no properties beyond the
             * common ones. Mapping them onto their own class is what allows
             * references to them to resolve.
             */
            "microsoft.compute/diskencryptionsets" => Basic<AzDiskEncryptionSet>( row ),
            "microsoft.databricks/accessconnectors" => Basic<AzDatabricksConnector>( row ),
            "microsoft.databricks/workspaces" => Basic<AzDatabricksWorkspace>( row ),
            "microsoft.insights/components" => Basic<AzApplicationInsights>( row ),
            "microsoft.managedidentity/userassignedidentities" => Basic<AzManagedIdentity>( row ),
            "microsoft.network/networkinterfaces" => Basic<AzNetworkInterface>( row ),
            "microsoft.network/networksecuritygroups" => Basic<AzNetworkSecurityGroup>( row ),
            "microsoft.network/privateendpoints" => Basic<AzPrivateEndpoint>( row ),
            "microsoft.network/routetables" => Basic<AzRouteTable>( row ),
            "microsoft.sql/servers" => Basic<AzSqlServer>( row ),
            "microsoft.sql/servers/databases" => Basic<AzSqlDatabase>( row ),
            "microsoft.storage/storageaccounts" => Basic<AzStorageAccount>( row ),

            _ => Basic<AzResource>( row ),
        };
    }


    /// <summary />
    private AzResource MapApiManagement( JsonElement row )
    {
        var properties = row.Obj( "properties" );
        var service = Basic<AzApiManagement>( row );

        /*
         * sku is a column of the resources table in its own right, rather than
         * a member of properties.
         */
        service.Sku = row.Obj( "sku" ).Str( "name" ) ?? "";
        service.SkuCapacity = row.Obj( "sku" ).Int( "capacity" );

        service.PublisherName = properties.Str( "publisherName" );
        service.PublisherEmail = properties.Str( "publisherEmail" );
        service.PlatformVersion = properties.Str( "platformVersion" );

        service.GatewayUrl = properties.Str( "gatewayUrl" );
        service.DeveloperPortalUrl = properties.Str( "developerPortalUrl" );
        service.ManagementApiUrl = properties.Str( "managementApiUrl" );
        service.ScmUrl = properties.Str( "scmUrl" );

        service.VirtualNetworkType = properties.Str( "virtualNetworkType" );
        service.PublicNetworkAccess = properties.Str( "publicNetworkAccess" );
        service.SubnetId = properties.Obj( "virtualNetworkConfiguration" ).Str( "subnetResourceId" );
        service.PublicIPAddresses = properties.StrList( "publicIPAddresses" );
        service.PrivateIPAddresses = properties.StrList( "privateIPAddresses" );

        foreach ( var item in properties.Items( "hostnameConfigurations" ) )
        {
            var hostName = item.Str( "hostName" ) ?? "";

            service.HostnameConfigurations.Add( new AzApiManagementHostname
            {
                Id = service.Id + "/hostnameConfigurations/" + hostName,
                Name = hostName,
                Type = HostnameType,
                HostnameType = item.Str( "type" ),
                CertificateSource = item.Str( "certificateSource" ),
                CertificateStatus = item.Str( "certificateStatus" ),
                DefaultSslBinding = item.Bool( "defaultSslBinding" ),
                NegotiateClientCertificate = item.Bool( "negotiateClientCertificate" ),
                KeyVaultId = item.Str( "keyVaultId" ),
            } );
        }

        return service;
    }


    /// <summary />
    private AzResource MapKeyVault( JsonElement row )
    {
        var properties = row.Obj( "properties" );
        var vault = Basic<AzKeyVault>( row );

        vault.Sku = properties.Obj( "sku" ).Str( "name" ) ?? "";
        vault.EnabledForDiskEncryption = properties.Bool( "enabledForDiskEncryption" );
        vault.EnableSoftDelete = properties.Bool( "enableSoftDelete" );
        vault.SoftDeleteRetentionInDays = properties.Int( "softDeleteRetentionInDays" );
        vault.EnableRbacAuthorization = properties.Bool( "enableRbacAuthorization" );
        vault.EnablePurgeProtection = properties.Bool( "enablePurgeProtection" );

        /*
         * A vault may have any number of private endpoint connections, but the
         * model only has room for one: take the first, and make the loss of the
         * others visible.
         */
        var connections = properties.Items( "privateEndpointConnections" );

        if ( connections.Count > 0 )
            vault.PrivateEndpointId = connections[ 0 ].Obj( "properties" ).Obj( "privateEndpoint" ).Str( "id" );

        if ( connections.Count > 1 )
            _logger.LogDebug( "vault {Vault} has {Count} private endpoint connections, only the first is modelled", vault.Name, connections.Count );

        return vault;
    }


    /// <summary />
    private AzResource MapVirtualNetwork( JsonElement row )
    {
        var properties = row.Obj( "properties" );
        var network = Basic<AzVirtualNetwork>( row );

        network.AddressPrefixes = properties.Obj( "addressSpace" ).StrList( "addressPrefixes" );
        network.DnsServers = properties.Obj( "dhcpOptions" ).StrList( "dnsServers" );
        network.Subnets = [];

        foreach ( var item in properties.Items( "subnets" ) )
        {
            var subnetProperties = item.Obj( "properties" );

            /*
             * Dual-stack subnets carry addressPrefixes rather than addressPrefix.
             */
            var prefix = subnetProperties.Str( "addressPrefix" );

            if ( prefix == null )
                prefix = subnetProperties.StrList( "addressPrefixes" ).FirstOrDefault();

            network.Subnets.Add( new AzSubnet
            {
                Id = item.Str( "id" ) ?? "",
                Name = item.Str( "name" ) ?? "",
                Type = SubnetType,
                AddressPrefix = prefix ?? "",
                NetworkSecurityGroupId = subnetProperties.Obj( "networkSecurityGroup" ).Str( "id" ),
                RouteTableId = subnetProperties.Obj( "routeTable" ).Str( "id" ),
            } );
        }

        return network;
    }


    /// <summary />
    /// <remarks>
    /// Populates the properties common to every resource. Constructed through
    /// <see cref="Activator" /> because <c>new T()</c> is not permitted on a
    /// type which declares required members, even though those members are
    /// assigned immediately below.
    /// </remarks>
    private static T Basic<T>( JsonElement row )
        where T : AzResource
    {
        var resource = Activator.CreateInstance<T>();

        resource.Id = row.Str( "id" ) ?? "";
        resource.Name = row.Str( "name" ) ?? "";
        resource.Type = row.Str( "type" ) ?? "";
        resource.Location = row.Str( "location" ) ?? "";
        resource.Tags = row.TagMap( "tags" );

        return resource;
    }
}
