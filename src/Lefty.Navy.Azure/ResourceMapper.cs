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

    /// <summary />
    private const string IPConfigurationType = "Microsoft.Network/networkInterfaces/ipConfigurations";

    /// <summary />
    private const string ConnectionType = "Microsoft.Network/privateEndpoints/privateLinkServiceConnections";

    /// <summary />
    private const string RouteType = "Microsoft.Network/routeTables/routes";

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
            "microsoft.network/networkinterfaces" => MapNetworkInterface( row ),
            "microsoft.network/privateendpoints" => MapPrivateEndpoint( row ),
            "microsoft.network/routetables" => MapRouteTable( row ),
            "microsoft.network/virtualnetworks" => MapVirtualNetwork( row ),
            "microsoft.sql/servers" => MapSqlServer( row ),
            "microsoft.sql/servers/databases" => MapSqlDatabase( row ),
            "microsoft.storage/storageaccounts" => MapStorageAccount( row ),

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
            "microsoft.network/networksecuritygroups" => Basic<AzNetworkSecurityGroup>( row ),

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
    private static AzResource MapRouteTable( JsonElement row )
    {
        var properties = row.Obj( "properties" );
        var table = Basic<AzRouteTable>( row );

        table.DisableBgpRoutePropagation = properties.Bool( "disableBgpRoutePropagation" );
        table.ProvisioningState = properties.Str( "provisioningState" );

        foreach ( var item in properties.Items( "routes" ) )
        {
            var route = item.Obj( "properties" );

            table.Routes.Add( new AzRoute
            {
                Id = item.Str( "id" ) ?? "",
                Name = item.Str( "name" ) ?? "",
                Type = item.Str( "type" ) ?? RouteType,
                AddressPrefix = route.Str( "addressPrefix" ),
                NextHopType = route.Str( "nextHopType" ),
                NextHopIpAddress = route.Str( "nextHopIpAddress" ),
                HasBgpOverride = route.Bool( "hasBgpOverride" ),
            } );
        }

        foreach ( var subnet in properties.Items( "subnets" ) )
        {
            var id = subnet.Str( "id" );

            if ( id != null )
                table.SubnetIds.Add( id );
        }

        return table;
    }


    /// <summary />
    private static AzResource MapSqlServer( JsonElement row )
    {
        var properties = row.Obj( "properties" );
        var server = Basic<AzSqlServer>( row );

        server.Version = properties.Str( "version" );
        server.State = properties.Str( "state" );
        server.FullyQualifiedDomainName = properties.Str( "fullyQualifiedDomainName" );
        server.AdministratorLogin = properties.Str( "administratorLogin" );

        var administrators = properties.Obj( "administrators" );

        server.EntraAdministratorLogin = administrators.Str( "login" );
        server.EntraAdministratorPrincipalType = administrators.Str( "principalType" );
        server.EntraAdministratorObjectId = administrators.Str( "sid" );
        server.EntraOnlyAuthentication = administrators.Bool( "azureADOnlyAuthentication" );

        server.PublicNetworkAccess = properties.Str( "publicNetworkAccess" );
        server.MinimalTlsVersion = properties.Str( "minimalTlsVersion" );
        server.RestrictOutboundNetworkAccess = properties.Str( "restrictOutboundNetworkAccess" );
        server.ExternalGovernanceStatus = properties.Str( "externalGovernanceStatus" );
        server.PrimaryUserAssignedIdentityId = properties.Str( "primaryUserAssignedIdentityId" );

        foreach ( var connection in properties.Items( "privateEndpointConnections" ) )
        {
            var id = connection.Obj( "properties" ).Obj( "privateEndpoint" ).Str( "id" );

            if ( id != null )
                server.PrivateEndpointIds.Add( id );
        }

        return server;
    }


    /// <summary />
    private static AzResource MapSqlDatabase( JsonElement row )
    {
        var properties = row.Obj( "properties" );
        var database = Basic<AzSqlDatabase>( row );

        database.ServerId = ParentOf( database.Id, "/databases/" );
        database.Kind = row.Str( "kind" );
        database.DatabaseId = properties.Str( "databaseId" );
        database.Status = properties.Str( "status" );

        database.Sku = row.Obj( "sku" ).Str( "name" );
        database.SkuTier = row.Obj( "sku" ).Str( "tier" );
        database.SkuCapacity = row.Obj( "sku" ).Int( "capacity" );
        database.CurrentServiceObjectiveName = properties.Str( "currentServiceObjectiveName" );
        database.RequestedServiceObjectiveName = properties.Str( "requestedServiceObjectiveName" );
        database.ElasticPoolId = properties.Str( "elasticPoolId" );
        database.LicenseType = properties.Str( "licenseType" );

        database.MaxSizeBytes = properties.Long( "maxSizeBytes" );
        database.Collation = properties.Str( "collation" );
        database.CatalogCollation = properties.Str( "catalogCollation" );

        database.ZoneRedundant = properties.Bool( "zoneRedundant" );
        database.AvailabilityZone = properties.Str( "availabilityZone" );
        database.ReadScale = properties.Str( "readScale" );
        database.RequestedBackupStorageRedundancy = properties.Str( "requestedBackupStorageRedundancy" );
        database.CurrentBackupStorageRedundancy = properties.Str( "currentBackupStorageRedundancy" );

        database.IsLedgerOn = properties.Bool( "isLedgerOn" );
        database.IsInfraEncryptionEnabled = properties.Bool( "isInfraEncryptionEnabled" );
        database.CreationDate = properties.Moment( "creationDate" );
        database.EarliestRestoreDate = properties.Moment( "earliestRestoreDate" );

        return database;
    }


    /// <summary />
    /// <remarks>
    /// A child resource carries its parent within its own identifier, which is
    /// the only place the relationship is reported.
    /// </remarks>
    private static string? ParentOf( string id, string separator )
    {
        var at = id.LastIndexOf( separator, StringComparison.OrdinalIgnoreCase );

        return at < 0 ? null : id[ ..at ];
    }


    /// <summary />
    private static AzResource MapStorageAccount( JsonElement row )
    {
        var properties = row.Obj( "properties" );
        var account = Basic<AzStorageAccount>( row );

        account.Kind = row.Str( "kind" );
        account.Sku = row.Obj( "sku" ).Str( "name" );
        account.SkuTier = row.Obj( "sku" ).Str( "tier" );

        account.AccessTier = properties.Str( "accessTier" );
        account.PrimaryLocation = properties.Str( "primaryLocation" );
        account.StatusOfPrimary = properties.Str( "statusOfPrimary" );
        account.ProvisioningState = properties.Str( "provisioningState" );

        account.SupportsHttpsTrafficOnly = properties.Bool( "supportsHttpsTrafficOnly" );
        account.MinimumTlsVersion = properties.Str( "minimumTlsVersion" );
        account.AllowBlobPublicAccess = properties.Bool( "allowBlobPublicAccess" );
        account.AllowSharedKeyAccess = properties.Bool( "allowSharedKeyAccess" );
        account.AllowCrossTenantReplication = properties.Bool( "allowCrossTenantReplication" );
        account.DefaultToOAuthAuthentication = properties.Bool( "defaultToOAuthAuthentication" );
        account.PublicNetworkAccess = properties.Str( "publicNetworkAccess" );
        account.DnsEndpointType = properties.Str( "dnsEndpointType" );

        account.IsHnsEnabled = properties.Bool( "isHnsEnabled" );
        account.IsSftpEnabled = properties.Bool( "isSftpEnabled" );
        account.IsNfsV3Enabled = properties.Bool( "isNfsV3Enabled" );
        account.IsLocalUserEnabled = properties.Bool( "isLocalUserEnabled" );

        var encryption = properties.Obj( "encryption" );

        account.EncryptionKeySource = encryption.Str( "keySource" );
        account.RequireInfrastructureEncryption = encryption.Bool( "requireInfrastructureEncryption" );
        account.EncryptionKeyVaultUri = encryption.Obj( "keyvaultproperties" ).Str( "keyvaulturi" );
        account.EncryptionKeyName = encryption.Obj( "keyvaultproperties" ).Str( "keyname" );

        var acls = properties.Obj( "networkAcls" );

        account.NetworkAclsDefaultAction = acls.Str( "defaultAction" );
        account.NetworkAclsBypass = acls.Str( "bypass" );

        foreach ( var rule in acls.Items( "ipRules" ) )
        {
            var value = rule.Str( "value" );

            if ( value != null )
                account.NetworkAclsIpRules.Add( value );
        }

        foreach ( var rule in acls.Items( "virtualNetworkRules" ) )
        {
            var id = rule.Str( "id" );

            if ( id != null )
                account.NetworkAclsVirtualNetworkRules.Add( id );
        }

        foreach ( var endpoint in properties.Obj( "primaryEndpoints" ).Pairs() )
            account.PrimaryEndpoints[ endpoint.Key ] = endpoint.Value;

        foreach ( var connection in properties.Items( "privateEndpointConnections" ) )
        {
            var id = connection.Obj( "properties" ).Obj( "privateEndpoint" ).Str( "id" );

            if ( id != null )
                account.PrivateEndpointIds.Add( id );
        }

        return account;
    }


    /// <summary />
    private static AzResource MapNetworkInterface( JsonElement row )
    {
        var properties = row.Obj( "properties" );
        var nic = Basic<AzNetworkInterface>( row );

        nic.MacAddress = properties.Str( "macAddress" );
        nic.NicType = properties.Str( "nicType" );
        nic.EnableAcceleratedNetworking = properties.Bool( "enableAcceleratedNetworking" );
        nic.EnableIPForwarding = properties.Bool( "enableIPForwarding" );
        nic.DisableTcpStateTracking = properties.Bool( "disableTcpStateTracking" );
        nic.ProvisioningState = properties.Str( "provisioningState" );

        var dns = properties.Obj( "dnsSettings" );

        nic.DnsServers = dns.StrList( "dnsServers" );
        nic.AppliedDnsServers = dns.StrList( "appliedDnsServers" );
        nic.InternalDomainNameSuffix = dns.Str( "internalDomainNameSuffix" );

        nic.NetworkSecurityGroupId = properties.Obj( "networkSecurityGroup" ).Str( "id" );
        nic.VirtualMachineId = properties.Obj( "virtualMachine" ).Str( "id" );
        nic.PrivateEndpointId = properties.Obj( "privateEndpoint" ).Str( "id" );

        foreach ( var item in properties.Items( "ipConfigurations" ) )
        {
            var configuration = item.Obj( "properties" );

            nic.IPConfigurations.Add( new AzNetworkInterfaceIPConfiguration
            {
                Id = item.Str( "id" ) ?? "",
                Name = item.Str( "name" ) ?? "",
                Type = IPConfigurationType,
                PrivateIPAddress = configuration.Str( "privateIPAddress" ),
                PrivateIPAllocationMethod = configuration.Str( "privateIPAllocationMethod" ),
                PrivateIPAddressVersion = configuration.Str( "privateIPAddressVersion" ),
                Primary = configuration.Bool( "primary" ),
                SubnetId = configuration.Obj( "subnet" ).Str( "id" ),
                PublicIPAddressId = configuration.Obj( "publicIPAddress" ).Str( "id" ),
            } );
        }

        return nic;
    }


    /// <summary />
    private static AzResource MapPrivateEndpoint( JsonElement row )
    {
        var properties = row.Obj( "properties" );
        var endpoint = Basic<AzPrivateEndpoint>( row );

        endpoint.ProvisioningState = properties.Str( "provisioningState" );
        endpoint.CustomNetworkInterfaceName = properties.Str( "customNetworkInterfaceName" );
        endpoint.SubnetId = properties.Obj( "subnet" ).Str( "id" );

        foreach ( var nic in properties.Items( "networkInterfaces" ) )
        {
            var id = nic.Str( "id" );

            if ( id != null )
                endpoint.NetworkInterfaceIds.Add( id );
        }

        /*
         * A connection is manual when it had to be approved by the owner of the
         * target rather than by whoever created the endpoint; the two are
         * reported as separate collections.
         */
        MapConnections( endpoint, properties.Items( "privateLinkServiceConnections" ), false );
        MapConnections( endpoint, properties.Items( "manualPrivateLinkServiceConnections" ), true );

        foreach ( var config in properties.Items( "customDnsConfigs" ) )
        {
            endpoint.CustomDnsConfigs.Add( new AzPrivateEndpointDnsConfig
            {
                Fqdn = config.Str( "fqdn" ),
                IPAddresses = config.StrList( "ipAddresses" ),
            } );
        }

        return endpoint;
    }


    /// <summary />
    private static void MapConnections( AzPrivateEndpoint endpoint, List<JsonElement> items, bool isManual )
    {
        foreach ( var item in items )
        {
            var properties = item.Obj( "properties" );
            var state = properties.Obj( "privateLinkServiceConnectionState" );
            var name = item.Str( "name" ) ?? "";

            endpoint.Connections.Add( new AzPrivateEndpointConnection
            {
                Id = item.Str( "id" ) ?? endpoint.Id + "/privateLinkServiceConnections/" + name,
                Name = name,
                Type = ConnectionType,
                PrivateLinkServiceId = properties.Str( "privateLinkServiceId" ),
                GroupIds = properties.StrList( "groupIds" ),
                Status = state.Str( "status" ),
                StatusDescription = state.Str( "description" ),
                IsManual = isManual,
            } );
        }
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
