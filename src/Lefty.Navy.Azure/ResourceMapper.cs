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

    /// <summary />
    private const string SecurityRuleType = "Microsoft.Network/networkSecurityGroups/securityRules";

    /// <summary>
    /// Agent pools are returned inline within the cluster, and carry neither an
    /// identifier nor a type of their own.
    /// </summary>
    private const string NodePoolType = "Microsoft.ContainerService/managedClusters/agentPools";

    /// <summary />
    private const string FrontendType = "Microsoft.Network/loadBalancers/frontendIPConfigurations";

    /// <summary />
    private const string BackendPoolType = "Microsoft.Network/loadBalancers/backendAddressPools";

    /// <summary />
    private const string LoadBalancingRuleType = "Microsoft.Network/loadBalancers/loadBalancingRules";

    /// <summary />
    private const string ProbeType = "Microsoft.Network/loadBalancers/probes";

    /// <summary />
    private const string InboundNatRuleType = "Microsoft.Network/loadBalancers/inboundNatRules";

    /// <summary />
    private const string OutboundRuleType = "Microsoft.Network/loadBalancers/outboundRules";

    private readonly ILogger<ResourceMapper> _logger;


    /// <summary />
    public ResourceMapper( ILogger<ResourceMapper> logger )
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
            "microsoft.alertsmanagement/smartdetectoralertrules" => MapSmartDetectorAlertRule( row ),
            "microsoft.apimanagement/service" => MapApiManagement( row ),
            "microsoft.cache/redis" => MapCacheForRedis( row ),
            "microsoft.compute/diskencryptionsets" => MapDiskEncryptionSet( row ),
            "microsoft.compute/virtualmachines" => MapVirtualMachine( row ),
            "microsoft.compute/virtualmachinescalesets" => MapVirtualMachineScaleSet( row ),
            "microsoft.containerservice/managedclusters" => MapKubernetesService( row ),
            "microsoft.eventhub/namespaces" => MapEventHubNamespace( row ),
            "microsoft.insights/actiongroups" => MapActionGroup( row ),
            "microsoft.insights/activitylogalerts" => MapActivityLogAlertRule( row ),
            "microsoft.insights/components" => MapApplicationInsights( row ),
            "microsoft.insights/metricalerts" => MapMetricAlertRule( row ),
            "microsoft.keyvault/vaults" => MapKeyVault( row ),
            "microsoft.managedidentity/userassignedidentities" => MapManagedIdentity( row ),
            "microsoft.netapp/netappaccounts" => MapNetAppAccount( row ),
            "microsoft.netapp/netappaccounts/capacitypools" => MapNetAppCapacityPool( row ),
            "microsoft.netapp/netappaccounts/capacitypools/volumes" => MapVolume( row ),
            "microsoft.network/loadbalancers" => MapLoadBalancer( row ),
            "microsoft.network/networkinterfaces" => MapNetworkInterface( row ),
            "microsoft.network/networksecuritygroups" => MapNetworkSecurityGroup( row ),
            "microsoft.network/privateendpoints" => MapPrivateEndpoint( row ),
            "microsoft.network/routetables" => MapRouteTable( row ),
            "microsoft.network/virtualnetworks" => MapVirtualNetwork( row ),
            "microsoft.sql/servers" => MapSqlServer( row ),
            "microsoft.sql/servers/databases" => MapSqlDatabase( row ),
            "microsoft.storage/storageaccounts" => MapStorageAccount( row ),

            /*
             * The one type which is not enough on its own to say what the
             * resource is: a site is told apart from a function app only by its
             * kind.
             */
            "microsoft.web/sites" => MapWebSite( row ),

            /*
             * Types which are modelled, but carry no properties beyond the
             * common ones. Mapping them onto their own class is what allows
             * references to them to resolve.
             */
            "microsoft.databricks/accessconnectors" => Basic<AzDatabricksConnector>( row ),
            "microsoft.databricks/workspaces" => Basic<AzDatabricksWorkspace>( row ),

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
    private static AzResource MapNetworkSecurityGroup( JsonElement row )
    {
        var properties = row.Obj( "properties" );
        var group = Basic<AzNetworkSecurityGroup>( row );

        group.ProvisioningState = properties.Str( "provisioningState" );
        group.FlushConnection = properties.Bool( "flushConnection" );

        /*
         * defaultSecurityRules is returned alongside these, and is skipped: the
         * same six rules are reported for every group in every subscription.
         */
        foreach ( var item in properties.Items( "securityRules" ) )
        {
            var rule = item.Obj( "properties" );

            group.SecurityRules.Add( new AzSecurityRule
            {
                Id = item.Str( "id" ) ?? "",
                Name = item.Str( "name" ) ?? "",
                Type = item.Str( "type" ) ?? SecurityRuleType,
                Description = rule.Str( "description" ),
                Direction = rule.Str( "direction" ),
                Access = rule.Str( "access" ),
                Priority = rule.Int( "priority" ),
                Protocol = rule.Str( "protocol" ),
                SourceAddressPrefixes = OneOrMany( rule, "sourceAddressPrefix", "sourceAddressPrefixes" ),
                SourcePortRanges = OneOrMany( rule, "sourcePortRange", "sourcePortRanges" ),
                DestinationAddressPrefixes = OneOrMany( rule, "destinationAddressPrefix", "destinationAddressPrefixes" ),
                DestinationPortRanges = OneOrMany( rule, "destinationPortRange", "destinationPortRanges" ),
                SourceApplicationSecurityGroupIds = IdList( rule, "sourceApplicationSecurityGroups" ),
                DestinationApplicationSecurityGroupIds = IdList( rule, "destinationApplicationSecurityGroups" ),
            } );
        }

        group.SubnetIds = IdList( properties, "subnets" );
        group.NetworkInterfaceIds = IdList( properties, "networkInterfaces" );

        return group;
    }


    /// <summary />
    /// <remarks>
    /// A security rule states each of its addresses and ports either as a single
    /// value or as a list, and never as both: whichever form was not used is
    /// reported empty, or left out of the row altogether. The model keeps only
    /// the list.
    /// </remarks>
    private static List<string> OneOrMany( JsonElement element, string one, string many )
    {
        var single = element.Str( one );

        if ( single == null || single.Length == 0 )
            return element.StrList( many );

        return [ single ];
    }


    /// <summary />
    /// <remarks>
    /// The identifiers held by an array of objects which report nothing beyond
    /// what they point at.
    /// </remarks>
    private static List<string> IdList( JsonElement element, string name )
    {
        var ids = new List<string>();

        foreach ( var item in element.Items( name ) )
        {
            var id = item.Str( "id" );

            if ( id != null )
                ids.Add( id );
        }

        return ids;
    }


    /// <summary />
    private static AzResource MapManagedIdentity( JsonElement row )
    {
        var properties = row.Obj( "properties" );
        var identity = Basic<AzManagedIdentity>( row );

        identity.PrincipalId = properties.Str( "principalId" );
        identity.ClientId = properties.Str( "clientId" );
        identity.TenantId = properties.Str( "tenantId" );

        return identity;
    }


    /// <summary />
    private static AzResource MapDiskEncryptionSet( JsonElement row )
    {
        var properties = row.Obj( "properties" );
        var set = Basic<AzDiskEncryptionSet>( row );

        set.EncryptionType = properties.Str( "encryptionType" );
        set.ProvisioningState = properties.Str( "provisioningState" );
        set.RotationToLatestKeyVersionEnabled = properties.Bool( "rotationToLatestKeyVersionEnabled" );
        set.LastKeyRotationTimestamp = properties.Moment( "lastKeyRotationTimestamp" );
        set.FederatedClientId = properties.Str( "federatedClientId" );

        /*
         * previousKeys records the keys the set has rotated away from, and is
         * skipped: only the key in force says anything about the disks today.
         */
        var key = properties.Obj( "activeKey" );

        set.KeyUrl = key.Str( "keyUrl" );
        set.KeyVaultId = key.Obj( "sourceVault" ).Str( "id" );

        return set;
    }


    /// <summary />
    private static AzResource MapActionGroup( JsonElement row )
    {
        var properties = row.Obj( "properties" );
        var group = Basic<AzActionGroup>( row );

        group.GroupShortName = properties.Str( "groupShortName" );
        group.Enabled = properties.Bool( "enabled" );

        Receivers( group, properties, "emailReceivers", "Email", "emailAddress" );
        Receivers( group, properties, "smsReceivers", "Sms", "phoneNumber" );
        Receivers( group, properties, "webhookReceivers", "Webhook", "serviceUri" );
        Receivers( group, properties, "armRoleReceivers", "ArmRole", "roleId" );
        Receivers( group, properties, "azureFunctionReceivers", "AzureFunction", "functionAppResourceId" );
        Receivers( group, properties, "logicAppReceivers", "LogicApp", "resourceId" );
        Receivers( group, properties, "eventHubReceivers", "EventHub", "eventHubName" );
        Receivers( group, properties, "voiceReceivers", "Voice", "phoneNumber" );
        Receivers( group, properties, "automationRunbookReceivers", "AutomationRunbook", "automationAccountId" );
        Receivers( group, properties, "itsmReceivers", "Itsm", "workspaceId" );
        Receivers( group, properties, "azureAppPushReceivers", "AzureAppPush", "emailAddress" );

        return group;
    }


    /// <summary />
    /// <param name="target">
    /// Property holding the destination, which differs with the kind of
    /// receiver.
    /// </param>
    private static void Receivers( AzActionGroup group, JsonElement properties, string array, string kind, string target )
    {
        foreach ( var item in properties.Items( array ) )
        {
            group.Receivers.Add( new AzActionGroupReceiver
            {
                Kind = kind,
                Name = item.Str( "name" ) ?? "",
                Target = item.Str( target ),
                Status = item.Str( "status" ),
                UseCommonAlertSchema = item.Bool( "useCommonAlertSchema" ),
            } );
        }
    }


    /// <summary />
    private static AzResource MapActivityLogAlertRule( JsonElement row )
    {
        var properties = row.Obj( "properties" );
        var rule = Basic<AzActivityLogAlertRule>( row );

        rule.Description = properties.Str( "description" );
        rule.Enabled = properties.Bool( "enabled" );
        rule.Scopes = properties.StrList( "scopes" );

        foreach ( var item in properties.Obj( "condition" ).Items( "allOf" ) )
        {
            var condition = Condition( item );

            foreach ( var alternative in item.Items( "anyOf" ) )
                condition.AnyOf.Add( Condition( alternative ) );

            rule.Conditions.Add( condition );
        }

        rule.ActionGroupIds = ActionGroupIds( properties.Obj( "actions" ).Items( "actionGroups" ) );

        return rule;
    }


    /// <summary />
    private static AzActivityLogAlertCondition Condition( JsonElement item )
    {
        return new AzActivityLogAlertCondition
        {
            Field = item.Str( "field" ),
            EqualTo = item.Str( "equals" ),
            ContainsAny = item.StrList( "containsAny" ),
        };
    }


    /// <summary />
    /// <remarks>
    /// An alert rule names its action groups as objects holding an identifier,
    /// in the same shape whichever kind of rule it is.
    /// </remarks>
    private static List<string> ActionGroupIds( List<JsonElement> items )
    {
        var ids = new List<string>();

        foreach ( var item in items )
        {
            var id = item.Str( "actionGroupId" );

            if ( id != null )
                ids.Add( id );
        }

        return ids;
    }


    /// <summary />
    private static AzResource MapMetricAlertRule( JsonElement row )
    {
        var properties = row.Obj( "properties" );
        var rule = Basic<AzMetricAlertRule>( row );

        rule.Description = properties.Str( "description" );
        rule.Severity = properties.Int( "severity" );
        rule.Enabled = properties.Bool( "enabled" );
        rule.AutoMitigate = properties.Bool( "autoMitigate" );

        rule.EvaluationFrequency = properties.Str( "evaluationFrequency" );
        rule.WindowSize = properties.Str( "windowSize" );

        rule.Scopes = properties.StrList( "scopes" );
        rule.TargetResourceType = properties.Str( "targetResourceType" );
        rule.TargetResourceRegion = properties.Str( "targetResourceRegion" );

        var criteria = properties.Obj( "criteria" );

        /*
         * The kind of criteria is reported as an OData type name, which is the
         * shape itself prefixed by the namespace it was declared in.
         */
        var kind = criteria.Str( "odata.type" );

        if ( kind != null )
            rule.CriteriaType = kind[ ( kind.LastIndexOf( '.' ) + 1 ).. ];

        rule.WebTestId = criteria.Str( "webTestId" );
        rule.ComponentId = criteria.Str( "componentId" );
        rule.FailedLocationCount = criteria.Int( "failedLocationCount" );

        foreach ( var item in criteria.Items( "allOf" ) )
        {
            var criterion = new AzMetricAlertCriterion
            {
                Name = item.Str( "name" ) ?? "",
                CriterionType = item.Str( "criterionType" ),
                MetricName = item.Str( "metricName" ),
                MetricNamespace = item.Str( "metricNamespace" ),
                Operator = item.Str( "operator" ),
                Threshold = item.Dbl( "threshold" ),
                TimeAggregation = item.Str( "timeAggregation" ),
                SkipMetricValidation = item.Bool( "skipMetricValidation" ),
                AlertSensitivity = item.Str( "alertSensitivity" ),
                FailingPeriodsToAlert = item.Obj( "failingPeriods" ).Int( "minFailingPeriodsToAlert" ),
                FailingPeriodsWindow = item.Obj( "failingPeriods" ).Int( "numberOfEvaluationPeriods" ),
            };

            foreach ( var dimension in item.Items( "dimensions" ) )
            {
                criterion.Dimensions.Add( new AzMetricAlertDimension
                {
                    Name = dimension.Str( "name" ) ?? "",
                    Operator = dimension.Str( "operator" ),
                    Values = dimension.StrList( "values" ),
                } );
            }

            rule.Criteria.Add( criterion );
        }

        rule.ActionGroupIds = ActionGroupIds( properties.Items( "actions" ) );

        return rule;
    }


    /// <summary />
    private static AzResource MapSmartDetectorAlertRule( JsonElement row )
    {
        var properties = row.Obj( "properties" );
        var rule = Basic<AzSmartDetectorAlertRule>( row );

        rule.Description = properties.Str( "description" );
        rule.State = properties.Str( "state" );
        rule.Severity = properties.Str( "severity" );
        rule.Frequency = properties.Str( "frequency" );

        var detector = properties.Obj( "detector" );

        rule.DetectorId = detector.Str( "id" );
        rule.DetectorName = detector.Str( "name" );
        rule.DetectorSupportedResourceTypes = detector.StrList( "supportedResourceTypes" );

        /*
         * Named scope rather than scopes, and holding the identifiers directly
         * rather than objects which wrap them, unlike every other alert rule.
         */
        rule.Scopes = properties.StrList( "scope" );
        rule.ThrottlingDuration = properties.Obj( "throttling" ).Str( "duration" );

        var actions = properties.Obj( "actionGroups" );

        rule.CustomEmailSubject = actions.Str( "customEmailSubject" );
        rule.CustomWebhookPayload = actions.Str( "customWebhookPayload" );
        rule.ActionGroupIds = actions.StrList( "groupIds" );

        return rule;
    }


    /// <summary />
    /// <remarks>
    /// Application Insights reports most of its properties in PascalCase, and a
    /// few of them in camelCase, which is why the names below are inconsistent.
    /// </remarks>
    private static AzResource MapApplicationInsights( JsonElement row )
    {
        var properties = row.Obj( "properties" );
        var component = Basic<AzApplicationInsights>( row );

        component.Kind = row.Str( "kind" );
        component.ApplicationType = properties.Str( "Application_Type" );
        component.AppId = properties.Str( "AppId" );
        component.ProvisioningState = properties.Str( "provisioningState" );
        component.CreationDate = properties.Moment( "CreationDate" );

        component.IngestionMode = properties.Str( "IngestionMode" );
        component.WorkspaceResourceId = properties.Str( "WorkspaceResourceId" );
        component.RetentionInDays = properties.Int( "RetentionInDays" );
        component.SamplingPercentage = properties.Dbl( "SamplingPercentage" );

        component.DisableIpMasking = properties.Bool( "DisableIpMasking" );
        component.DisableLocalAuth = properties.Bool( "DisableLocalAuth" );
        component.PublicNetworkAccessForIngestion = properties.Str( "publicNetworkAccessForIngestion" );
        component.PublicNetworkAccessForQuery = properties.Str( "publicNetworkAccessForQuery" );

        foreach ( var scope in properties.Items( "PrivateLinkScopedResources" ) )
        {
            var id = scope.Str( "ResourceId" );

            if ( id != null )
                component.PrivateLinkScopedResourceIds.Add( id );
        }

        return component;
    }


    /// <summary />
    private static AzResource MapKubernetesService( JsonElement row )
    {
        var properties = row.Obj( "properties" );
        var cluster = Basic<AzKubernetesService>( row );

        cluster.Sku = row.Obj( "sku" ).Str( "name" );
        cluster.SkuTier = row.Obj( "sku" ).Str( "tier" );
        cluster.ProvisioningState = properties.Str( "provisioningState" );
        cluster.PowerState = properties.Obj( "powerState" ).Str( "code" );

        cluster.KubernetesVersion = properties.Str( "kubernetesVersion" );
        cluster.CurrentKubernetesVersion = properties.Str( "currentKubernetesVersion" );
        cluster.UpgradeChannel = properties.Obj( "autoUpgradeProfile" ).Str( "upgradeChannel" );
        cluster.NodeOSUpgradeChannel = properties.Obj( "autoUpgradeProfile" ).Str( "nodeOSUpgradeChannel" );
        cluster.SupportPlan = properties.Str( "supportPlan" );

        cluster.DnsPrefix = properties.Str( "dnsPrefix" );
        cluster.Fqdn = properties.Str( "fqdn" );
        cluster.PrivateFqdn = properties.Str( "privateFQDN" );

        var apiServer = properties.Obj( "apiServerAccessProfile" );

        cluster.EnablePrivateCluster = apiServer.Bool( "enablePrivateCluster" );
        cluster.PrivateDnsZone = apiServer.Str( "privateDNSZone" );
        cluster.AuthorizedIPRanges = apiServer.StrList( "authorizedIPRanges" );

        cluster.NodeResourceGroup = properties.Str( "nodeResourceGroup" );
        cluster.EnableRbac = properties.Bool( "enableRBAC" );
        cluster.DisableLocalAccounts = properties.Bool( "disableLocalAccounts" );

        var aad = properties.Obj( "aadProfile" );

        cluster.EnableAzureRbac = aad.Bool( "enableAzureRBAC" );
        cluster.AadAdminGroupObjectIds = aad.StrList( "adminGroupObjectIDs" );

        var security = properties.Obj( "securityProfile" );

        cluster.WorkloadIdentityEnabled = security.Obj( "workloadIdentity" ).Bool( "enabled" );
        cluster.DefenderEnabled = security.Obj( "defender" ).Obj( "securityMonitoring" ).Bool( "enabled" );
        cluster.OidcIssuerEnabled = properties.Obj( "oidcIssuerProfile" ).Bool( "enabled" );

        var network = properties.Obj( "networkProfile" );

        cluster.NetworkPlugin = network.Str( "networkPlugin" );
        cluster.NetworkPluginMode = network.Str( "networkPluginMode" );
        cluster.NetworkPolicy = network.Str( "networkPolicy" );
        cluster.NetworkDataplane = network.Str( "networkDataplane" );
        cluster.OutboundType = network.Str( "outboundType" );
        cluster.LoadBalancerSku = network.Str( "loadBalancerSku" );
        cluster.PodCidrs = network.StrList( "podCidrs" );
        cluster.ServiceCidrs = network.StrList( "serviceCidrs" );
        cluster.DnsServiceIP = network.Str( "dnsServiceIP" );
        cluster.ServiceMeshMode = properties.Obj( "serviceMeshProfile" ).Str( "mode" );

        cluster.DiskEncryptionSetId = properties.Str( "diskEncryptionSetID" );
        cluster.KubeletIdentityId = properties.Obj( "identityProfile" ).Obj( "kubeletidentity" ).Str( "resourceId" );

        foreach ( var item in properties.Items( "agentPoolProfiles" ) )
        {
            var name = item.Str( "name" ) ?? "";

            cluster.NodePools.Add( new AzKubernetesNodePool
            {
                Id = cluster.Id + "/agentPools/" + name,
                Name = name,
                Type = NodePoolType,
                Mode = item.Str( "mode" ),
                Count = item.Int( "count" ),
                VmSize = item.Str( "vmSize" ),
                ProvisioningState = item.Str( "provisioningState" ),
                PowerState = item.Obj( "powerState" ).Str( "code" ),
                EnableAutoScaling = item.Bool( "enableAutoScaling" ),
                MinCount = item.Int( "minCount" ),
                MaxCount = item.Int( "maxCount" ),
                MaxPods = item.Int( "maxPods" ),
                AvailabilityZones = item.StrList( "availabilityZones" ),
                OsType = item.Str( "osType" ),
                OsSku = item.Str( "osSKU" ),
                OsDiskSizeGB = item.Int( "osDiskSizeGB" ),
                OsDiskType = item.Str( "osDiskType" ),
                EnableEncryptionAtHost = item.Bool( "enableEncryptionAtHost" ),
                EnableFips = item.Bool( "enableFIPS" ),
                EnableNodePublicIP = item.Bool( "enableNodePublicIP" ),
                OrchestratorVersion = item.Str( "orchestratorVersion" ),
                CurrentOrchestratorVersion = item.Str( "currentOrchestratorVersion" ),
                NodeImageVersion = item.Str( "nodeImageVersion" ),
                NodeLabels = item.TagMap( "nodeLabels" ),
                NodeTaints = item.StrList( "nodeTaints" ),
                SubnetId = item.Str( "vnetSubnetID" ),
            } );
        }

        foreach ( var addon in properties.Fields( "addonProfiles" ) )
        {
            if ( addon.Value.Bool( "enabled" ) == false )
                continue;

            cluster.Addons.Add( new AzKubernetesAddon
            {
                Name = addon.Key,
                IdentityResourceId = addon.Value.Obj( "identity" ).Str( "resourceId" ),
            } );
        }

        return cluster;
    }


    /// <summary />
    private static AzResource MapVirtualMachineScaleSet( JsonElement row )
    {
        var properties = row.Obj( "properties" );
        var set = Basic<AzVirtualMachineScaleSet>( row );

        set.Sku = row.Obj( "sku" ).Str( "name" );
        set.SkuTier = row.Obj( "sku" ).Str( "tier" );
        set.SkuCapacity = row.Obj( "sku" ).Int( "capacity" );

        set.OrchestrationMode = properties.Str( "orchestrationMode" );
        set.UpgradeMode = properties.Obj( "upgradePolicy" ).Str( "mode" );
        set.ProvisioningState = properties.Str( "provisioningState" );
        set.TimeCreated = properties.Moment( "timeCreated" );
        set.Overprovision = properties.Bool( "overprovision" );
        set.SinglePlacementGroup = properties.Bool( "singlePlacementGroup" );
        set.PlatformFaultDomainCount = properties.Int( "platformFaultDomainCount" );

        var profile = properties.Obj( "virtualMachineProfile" );
        var os = profile.Obj( "osProfile" );

        set.ComputerNamePrefix = os.Str( "computerNamePrefix" );
        set.AdminUsername = os.Str( "adminUsername" );
        set.DisablePasswordAuthentication = os.Obj( "linuxConfiguration" ).Bool( "disablePasswordAuthentication" );

        set.SecurityType = profile.Obj( "securityProfile" ).Str( "securityType" );
        set.EncryptionAtHost = profile.Obj( "securityProfile" ).Bool( "encryptionAtHost" );

        var storage = profile.Obj( "storageProfile" );
        var disk = storage.Obj( "osDisk" );

        set.OsType = disk.Str( "osType" );
        set.OsDiskSizeGB = disk.Int( "diskSizeGB" );
        set.OsDiskCaching = disk.Str( "caching" );
        set.OsDiskStorageAccountType = disk.Obj( "managedDisk" ).Str( "storageAccountType" );
        set.DiskEncryptionSetId = disk.Obj( "managedDisk" ).Obj( "diskEncryptionSet" ).Str( "id" );

        var image = storage.Obj( "imageReference" );

        set.ImageReferenceId = image.Str( "id" );

        if ( image.Str( "publisher" ) != null )
        {
            set.ImageReference = string.Join( ":",
                image.Str( "publisher" ), image.Str( "offer" ), image.Str( "sku" ), image.Str( "version" ) );
        }

        foreach ( var item in profile.Obj( "networkProfile" ).Items( "networkInterfaceConfigurations" ) )
        {
            var configuration = item.Obj( "properties" );

            var nic = new AzScaleSetNetworkInterface
            {
                Name = item.Str( "name" ) ?? "",
                Primary = configuration.Bool( "primary" ),
                EnableAcceleratedNetworking = configuration.Bool( "enableAcceleratedNetworking" ),
                EnableIPForwarding = configuration.Bool( "enableIPForwarding" ),
                NetworkSecurityGroupId = configuration.Obj( "networkSecurityGroup" ).Str( "id" ),
            };

            foreach ( var address in configuration.Items( "ipConfigurations" ) )
            {
                var settings = address.Obj( "properties" );

                nic.IPConfigurations.Add( new AzScaleSetIPConfiguration
                {
                    Name = address.Str( "name" ) ?? "",
                    Primary = settings.Bool( "primary" ),
                    PrivateIPAddressVersion = settings.Str( "privateIPAddressVersion" ),
                    SubnetId = settings.Obj( "subnet" ).Str( "id" ),
                    LoadBalancerBackendPoolIds = IdList( settings, "loadBalancerBackendAddressPools" ),
                } );
            }

            set.NetworkInterfaces.Add( nic );
        }

        foreach ( var item in profile.Obj( "extensionProfile" ).Items( "extensions" ) )
        {
            var extension = item.Obj( "properties" );

            set.Extensions.Add( new AzScaleSetExtension
            {
                Name = item.Str( "name" ) ?? "",
                Publisher = extension.Str( "publisher" ),
                ExtensionType = extension.Str( "type" ),
                TypeHandlerVersion = extension.Str( "typeHandlerVersion" ),
                AutoUpgradeMinorVersion = extension.Bool( "autoUpgradeMinorVersion" ),
            } );
        }

        return set;
    }


    /// <summary />
    private static AzResource MapLoadBalancer( JsonElement row )
    {
        var properties = row.Obj( "properties" );
        var balancer = Basic<AzLoadBalancer>( row );

        balancer.Sku = row.Obj( "sku" ).Str( "name" );
        balancer.SkuTier = row.Obj( "sku" ).Str( "tier" );
        balancer.ProvisioningState = properties.Str( "provisioningState" );

        foreach ( var item in properties.Items( "frontendIPConfigurations" ) )
        {
            var configuration = item.Obj( "properties" );

            balancer.FrontendIPConfigurations.Add( new AzLoadBalancerFrontend
            {
                Id = item.Str( "id" ) ?? "",
                Name = item.Str( "name" ) ?? "",
                Type = item.Str( "type" ) ?? FrontendType,
                PrivateIPAddress = configuration.Str( "privateIPAddress" ),
                PrivateIPAllocationMethod = configuration.Str( "privateIPAllocationMethod" ),
                PrivateIPAddressVersion = configuration.Str( "privateIPAddressVersion" ),
                SubnetId = configuration.Obj( "subnet" ).Str( "id" ),
                PublicIPAddressId = configuration.Obj( "publicIPAddress" ).Str( "id" ),
                Zones = item.StrList( "zones" ),
            } );
        }

        foreach ( var item in properties.Items( "backendAddressPools" ) )
        {
            var pool = item.Obj( "properties" );

            balancer.BackendPools.Add( new AzLoadBalancerBackendPool
            {
                Id = item.Str( "id" ) ?? "",
                Name = item.Str( "name" ) ?? "",
                Type = item.Str( "type" ) ?? BackendPoolType,
                ProvisioningState = pool.Str( "provisioningState" ),
                MemberCount = pool.Items( "backendIPConfigurations" ).Count,
            } );
        }

        foreach ( var item in properties.Items( "loadBalancingRules" ) )
        {
            var rule = item.Obj( "properties" );

            balancer.LoadBalancingRules.Add( new AzLoadBalancerRule
            {
                Id = item.Str( "id" ) ?? "",
                Name = item.Str( "name" ) ?? "",
                Type = item.Str( "type" ) ?? LoadBalancingRuleType,
                Protocol = rule.Str( "protocol" ),
                FrontendPort = rule.Int( "frontendPort" ),
                BackendPort = rule.Int( "backendPort" ),
                FrontendIPConfigurationId = rule.Obj( "frontendIPConfiguration" ).Str( "id" ),
                BackendPoolId = rule.Obj( "backendAddressPool" ).Str( "id" ),
                ProbeId = rule.Obj( "probe" ).Str( "id" ),
                LoadDistribution = rule.Str( "loadDistribution" ),
                IdleTimeoutInMinutes = rule.Int( "idleTimeoutInMinutes" ),
                EnableFloatingIP = rule.Bool( "enableFloatingIP" ),
                EnableTcpReset = rule.Bool( "enableTcpReset" ),
                DisableOutboundSnat = rule.Bool( "disableOutboundSnat" ),
            } );
        }

        foreach ( var item in properties.Items( "probes" ) )
        {
            var probe = item.Obj( "properties" );

            balancer.Probes.Add( new AzLoadBalancerProbe
            {
                Id = item.Str( "id" ) ?? "",
                Name = item.Str( "name" ) ?? "",
                Type = item.Str( "type" ) ?? ProbeType,
                Protocol = probe.Str( "protocol" ),
                Port = probe.Int( "port" ),
                RequestPath = probe.Str( "requestPath" ),
                IntervalInSeconds = probe.Int( "intervalInSeconds" ),
                ProbeThreshold = probe.Int( "probeThreshold" ),
            } );
        }

        foreach ( var item in properties.Items( "inboundNatRules" ) )
        {
            var rule = item.Obj( "properties" );

            balancer.InboundNatRules.Add( new AzLoadBalancerNatRule
            {
                Id = item.Str( "id" ) ?? "",
                Name = item.Str( "name" ) ?? "",
                Type = item.Str( "type" ) ?? InboundNatRuleType,
                Protocol = rule.Str( "protocol" ),
                FrontendPort = rule.Int( "frontendPort" ),
                BackendPort = rule.Int( "backendPort" ),
                FrontendPortRangeStart = rule.Int( "frontendPortRangeStart" ),
                FrontendPortRangeEnd = rule.Int( "frontendPortRangeEnd" ),
                FrontendIPConfigurationId = rule.Obj( "frontendIPConfiguration" ).Str( "id" ),
                BackendPoolId = rule.Obj( "backendAddressPool" ).Str( "id" ),
                IdleTimeoutInMinutes = rule.Int( "idleTimeoutInMinutes" ),
                EnableTcpReset = rule.Bool( "enableTcpReset" ),
            } );
        }

        foreach ( var item in properties.Items( "outboundRules" ) )
        {
            var rule = item.Obj( "properties" );

            balancer.OutboundRules.Add( new AzLoadBalancerOutboundRule
            {
                Id = item.Str( "id" ) ?? "",
                Name = item.Str( "name" ) ?? "",
                Type = item.Str( "type" ) ?? OutboundRuleType,
                Protocol = rule.Str( "protocol" ),
                AllocatedOutboundPorts = rule.Int( "allocatedOutboundPorts" ),
                IdleTimeoutInMinutes = rule.Int( "idleTimeoutInMinutes" ),
                EnableTcpReset = rule.Bool( "enableTcpReset" ),
                FrontendIPConfigurationIds = IdList( rule, "frontendIPConfigurations" ),
                BackendPoolId = rule.Obj( "backendAddressPool" ).Str( "id" ),
            } );
        }

        return balancer;
    }


    /// <summary />
    private static AzResource MapVirtualMachine( JsonElement row )
    {
        var properties = row.Obj( "properties" );
        var machine = Basic<AzVirtualMachine>( row );

        machine.VmId = properties.Str( "vmId" );
        machine.VmSize = properties.Obj( "hardwareProfile" ).Str( "vmSize" );
        machine.ProvisioningState = properties.Str( "provisioningState" );
        machine.TimeCreated = properties.Moment( "timeCreated" );
        machine.Priority = properties.Str( "priority" );
        machine.LicenseType = properties.Str( "licenseType" );

        /*
         * The instance view is what the machine is doing rather than how it was
         * asked to be. Resource Graph folds it into the properties of a virtual
         * machine, which is the only type it does this for.
         */
        var instance = properties.Obj( "extended" ).Obj( "instanceView" );

        machine.PowerState = instance.Obj( "powerState" ).Str( "code" );
        machine.OsName = instance.Str( "osName" );
        machine.OsVersion = instance.Str( "osVersion" );
        machine.HyperVGeneration = instance.Str( "hyperVGeneration" );

        var os = properties.Obj( "osProfile" );

        machine.ComputerName = os.Str( "computerName" );
        machine.AdminUsername = os.Str( "adminUsername" );
        machine.DisablePasswordAuthentication = os.Obj( "linuxConfiguration" ).Bool( "disablePasswordAuthentication" );

        /*
         * Patching is configured per operating system, in whichever of the two
         * configurations applies.
         */
        machine.PatchMode = os.Obj( "windowsConfiguration" ).Obj( "patchSettings" ).Str( "patchMode" )
            ?? os.Obj( "linuxConfiguration" ).Obj( "patchSettings" ).Str( "patchMode" );

        var security = properties.Obj( "securityProfile" );

        machine.SecurityType = security.Str( "securityType" );
        machine.EncryptionAtHost = security.Bool( "encryptionAtHost" );
        machine.SecureBootEnabled = security.Obj( "uefiSettings" ).Bool( "secureBootEnabled" );
        machine.VTpmEnabled = security.Obj( "uefiSettings" ).Bool( "vTpmEnabled" );
        machine.BootDiagnosticsEnabled = properties.Obj( "diagnosticsProfile" ).Obj( "bootDiagnostics" ).Bool( "enabled" );

        var storage = properties.Obj( "storageProfile" );
        var image = storage.Obj( "imageReference" );

        machine.ImageReferenceId = image.Str( "id" );

        if ( image.Str( "publisher" ) != null )
        {
            machine.ImageReference = string.Join( ":",
                image.Str( "publisher" ), image.Str( "offer" ), image.Str( "sku" ), image.Str( "version" ) );
        }

        var osDisk = storage.Obj( "osDisk" );

        if ( osDisk.ValueKind == JsonValueKind.Object )
        {
            machine.OsDisk = Disk( osDisk );
            machine.OsType = osDisk.Str( "osType" );
            machine.DiskEncryptionSetId = machine.OsDisk.DiskEncryptionSetId;
        }

        foreach ( var item in storage.Items( "dataDisks" ) )
            machine.DataDisks.Add( Disk( item ) );

        machine.AvailabilitySetId = properties.Obj( "availabilitySet" ).Str( "id" );
        machine.VirtualMachineScaleSetId = properties.Obj( "virtualMachineScaleSet" ).Str( "id" );
        machine.NetworkInterfaceIds = IdList( properties.Obj( "networkProfile" ), "networkInterfaces" );

        return machine;
    }


    /// <summary />
    private static AzVirtualMachineDisk Disk( JsonElement item )
    {
        var managed = item.Obj( "managedDisk" );

        return new AzVirtualMachineDisk
        {
            Name = item.Str( "name" ) ?? "",
            ManagedDiskId = managed.Str( "id" ),
            Lun = item.Int( "lun" ),
            DiskSizeGB = item.Int( "diskSizeGB" ),
            StorageAccountType = managed.Str( "storageAccountType" ),
            Caching = item.Str( "caching" ),
            DeleteOption = item.Str( "deleteOption" ),
            DiskEncryptionSetId = managed.Obj( "diskEncryptionSet" ).Str( "id" ),
            WriteAcceleratorEnabled = item.Bool( "writeAcceleratorEnabled" ),
        };
    }


    /// <summary />
    /// <remarks>
    /// Function apps, logic apps and web apps are all
    /// <c>Microsoft.Web/sites</c>, and the kind is the only thing which tells
    /// them apart. A Standard logic app reports itself as
    /// <c>functionapp,workflowapp</c>, and so is mapped as a function app which
    /// knows it is a workflow.
    /// </remarks>
    private static AzResource MapWebSite( JsonElement row )
    {
        var kind = row.Str( "kind" ) ?? "";

        if ( kind.Contains( "functionapp", StringComparison.OrdinalIgnoreCase ) == false )
            return MapAppService( row );

        var properties = row.Obj( "properties" );
        var configuration = properties.Obj( "siteConfig" );
        var app = Basic<AzFunctionApp>( row );

        WebSite( app, row );

        app.IsWorkflowApp = kind.Contains( "workflowapp", StringComparison.OrdinalIgnoreCase );
        app.ContainerSize = properties.Int( "containerSize" );
        app.DailyMemoryTimeQuota = properties.Long( "dailyMemoryTimeQuota" );
        app.FunctionAppScaleLimit = configuration.Int( "functionAppScaleLimit" );
        app.MinimumElasticInstanceCount = configuration.Int( "minimumElasticInstanceCount" );

        return app;
    }


    /// <summary />
    private static AzResource MapAppService( JsonElement row )
    {
        var properties = row.Obj( "properties" );
        var site = Basic<AzAppService>( row );

        WebSite( site, row );

        site.ClientAffinityEnabled = properties.Bool( "clientAffinityEnabled" );
        site.NumberOfWorkers = properties.Obj( "siteConfig" ).Int( "numberOfWorkers" );
        site.RedundancyMode = properties.Str( "redundancyMode" );
        site.HostNamesDisabled = properties.Bool( "hostNamesDisabled" );

        return site;
    }


    /// <summary />
    /// <remarks>
    /// What every kind of site reports. The runtime stack is recorded in one of
    /// two fields depending on the operating system, and only ever one of them
    /// is set.
    /// </remarks>
    private static void WebSite( AzWebSite site, JsonElement row )
    {
        var properties = row.Obj( "properties" );
        var configuration = properties.Obj( "siteConfig" );

        site.Kind = row.Str( "kind" );
        site.State = properties.Str( "state" );
        site.Enabled = properties.Bool( "enabled" );
        site.IsLinux = properties.Bool( "reserved" );

        site.DefaultHostName = properties.Str( "defaultHostName" );
        site.HostNames = properties.StrList( "hostNames" );
        site.HttpsOnly = properties.Bool( "httpsOnly" );
        site.ClientCertEnabled = properties.Bool( "clientCertEnabled" );
        site.ClientCertMode = properties.Str( "clientCertMode" );
        site.PublicNetworkAccess = properties.Str( "publicNetworkAccess" );

        site.MinTlsVersion = configuration.Str( "minTlsVersion" );
        site.FtpsState = configuration.Str( "ftpsState" );
        site.Http20Enabled = configuration.Bool( "http20Enabled" );
        site.AlwaysOn = configuration.Bool( "alwaysOn" );
        site.RuntimeStack = configuration.Str( "linuxFxVersion" ) ?? configuration.Str( "netFrameworkVersion" );

        site.ServerFarmId = properties.Str( "serverFarmId" );
        site.VirtualNetworkSubnetId = properties.Str( "virtualNetworkSubnetId" );
        site.VnetRouteAllEnabled = properties.Bool( "vnetRouteAllEnabled" );
        site.OutboundIpAddresses = properties.Str( "outboundIpAddresses" );

        foreach ( var connection in properties.Items( "privateEndpointConnections" ) )
        {
            var id = connection.Obj( "properties" ).Obj( "privateEndpoint" ).Str( "id" );

            if ( id != null )
                site.PrivateEndpointIds.Add( id );
        }
    }


    /// <summary />
    private static AzResource MapCacheForRedis( JsonElement row )
    {
        var properties = row.Obj( "properties" );
        var cache = Basic<AzCacheForRedis>( row );

        /*
         * The sku is inside the properties here, and the sku column of the
         * resources table is null: the one type which does this.
         */
        var sku = properties.Obj( "sku" );

        cache.Sku = sku.Str( "name" );
        cache.SkuFamily = sku.Str( "family" );
        cache.SkuCapacity = sku.Int( "capacity" );

        cache.ProvisioningState = properties.Str( "provisioningState" );
        cache.RedisVersion = properties.Str( "redisVersion" );
        cache.UpdateChannel = properties.Str( "updateChannel" );

        cache.HostName = properties.Str( "hostName" );
        cache.Port = properties.Int( "port" );
        cache.SslPort = properties.Int( "sslPort" );
        cache.EnableNonSslPort = properties.Bool( "enableNonSslPort" );
        cache.MinimumTlsVersion = properties.Str( "minimumTlsVersion" );
        cache.PublicNetworkAccess = properties.Str( "publicNetworkAccess" );
        cache.DisableAccessKeyAuthentication = properties.Bool( "disableAccessKeyAuthentication" );

        /*
         * replicasPerMaster is the older name for the same number, and is the
         * only one an Enterprise cache reports.
         */
        cache.ReplicasPerPrimary = properties.Int( "replicasPerPrimary" ) is var replicas && replicas != 0
            ? replicas : properties.Int( "replicasPerMaster" );

        cache.ShardCount = properties.Int( "shardCount" );
        cache.ZonalAllocationPolicy = properties.Str( "zonalAllocationPolicy" );

        /*
         * Every value in the configuration is a string, whether it is a number
         * or a flag. Only the settings named here are taken: the same object
         * carries the storage connection string a backup is written with, and
         * that holds an account key.
         */
        var configuration = properties.Obj( "redisConfiguration" );

        cache.MaxMemoryPolicy = configuration.Str( "maxmemory-policy" );
        cache.MaxMemoryReservedMB = Number( configuration, "maxmemory-reserved" );
        cache.MaxFragmentationMemoryReservedMB = Number( configuration, "maxfragmentationmemory-reserved" );
        cache.MaxClients = Number( configuration, "maxclients" );
        cache.AadEnabled = Flag( configuration, "aad-enabled" );
        cache.RdbBackupEnabled = Flag( configuration, "rdb-backup-enabled" );
        cache.AofBackupEnabled = Flag( configuration, "aof-backup-enabled" );

        cache.SubnetId = properties.Str( "subnetId" );
        cache.StaticIP = properties.Str( "staticIP" );

        foreach ( var linked in properties.Items( "linkedServers" ) )
        {
            var id = linked.Str( "id" );

            if ( id != null )
                cache.LinkedServerIds.Add( id );
        }

        foreach ( var connection in properties.Items( "privateEndpointConnections" ) )
        {
            var id = connection.Obj( "properties" ).Obj( "privateEndpoint" ).Str( "id" );

            if ( id != null )
                cache.PrivateEndpointIds.Add( id );
        }

        return cache;
    }


    /// <summary />
    /// <remarks>
    /// A number which Redis reports as a string.
    /// </remarks>
    private static int Number( JsonElement element, string name )
    {
        return int.TryParse( element.Str( name ), out var number ) == true ? number : 0;
    }


    /// <summary />
    /// <remarks>
    /// A flag which Redis reports as the string true or false.
    /// </remarks>
    private static bool Flag( JsonElement element, string name )
    {
        return string.Equals( element.Str( name ), "true", StringComparison.OrdinalIgnoreCase );
    }


    /// <summary />
    private static AzResource MapEventHubNamespace( JsonElement row )
    {
        var properties = row.Obj( "properties" );
        var space = Basic<AzEventHubNamespace>( row );

        space.Sku = row.Obj( "sku" ).Str( "name" );
        space.SkuTier = row.Obj( "sku" ).Str( "tier" );
        space.SkuCapacity = row.Obj( "sku" ).Int( "capacity" );

        space.ProvisioningState = properties.Str( "provisioningState" );
        space.Status = properties.Str( "status" );
        space.CreatedAt = properties.Moment( "createdAt" );

        space.ServiceBusEndpoint = properties.Str( "serviceBusEndpoint" );
        space.KafkaEnabled = properties.Bool( "kafkaEnabled" );
        space.IsAutoInflateEnabled = properties.Bool( "isAutoInflateEnabled" );
        space.MaximumThroughputUnits = properties.Int( "maximumThroughputUnits" );
        space.ZoneRedundant = properties.Bool( "zoneRedundant" );

        space.DisableLocalAuth = properties.Bool( "disableLocalAuth" );
        space.MinimumTlsVersion = properties.Str( "minimumTlsVersion" );
        space.PublicNetworkAccess = properties.Str( "publicNetworkAccess" );

        foreach ( var connection in properties.Items( "privateEndpointConnections" ) )
        {
            var id = connection.Obj( "properties" ).Obj( "privateEndpoint" ).Str( "id" );

            if ( id != null )
                space.PrivateEndpointIds.Add( id );
        }

        return space;
    }


    /// <summary />
    private static AzResource MapNetAppAccount( JsonElement row )
    {
        var properties = row.Obj( "properties" );
        var account = Basic<AzNetAppAccount>( row );

        account.ProvisioningState = properties.Str( "provisioningState" );
        account.MultiAdStatus = properties.Str( "multiADStatus" );
        account.NfsV4IdDomain = properties.Str( "nfsV4IDDomain" );
        account.DisableShowmount = properties.Bool( "disableShowmount" );

        var encryption = properties.Obj( "encryption" );

        account.EncryptionKeySource = encryption.Str( "keySource" );
        account.EncryptionKeyVaultId = encryption.Obj( "keyVaultProperties" ).Str( "keyVaultResourceId" );
        account.EncryptionKeyName = encryption.Obj( "keyVaultProperties" ).Str( "keyName" );
        account.EncryptionKeyVaultUri = encryption.Obj( "keyVaultProperties" ).Str( "keyVaultUri" );
        account.EncryptionIdentityId = encryption.Obj( "identity" ).Str( "userAssignedIdentity" );

        foreach ( var item in properties.Items( "activeDirectories" ) )
        {
            account.ActiveDirectories.Add( new AzNetAppDirectory
            {
                ActiveDirectoryId = item.Str( "activeDirectoryId" ),
                Domain = item.Str( "domain" ),
                Username = item.Str( "username" ),
                Dns = item.Str( "dns" ),
                SmbServerName = item.Str( "smbServerName" ),
                OrganizationalUnit = item.Str( "organizationalUnit" ),
                Status = item.Str( "status" ),
                AesEncryption = item.Bool( "aesEncryption" ),
                LdapSigning = item.Bool( "ldapSigning" ),
                LdapOverTls = item.Bool( "ldapOverTLS" ),
                EncryptDCConnections = item.Bool( "encryptDCConnections" ),
                AllowLocalNfsUsersWithLdap = item.Bool( "allowLocalNfsUsersWithLdap" ),
            } );
        }

        return account;
    }


    /// <summary />
    private static AzResource MapNetAppCapacityPool( JsonElement row )
    {
        var properties = row.Obj( "properties" );
        var pool = Basic<AzNetAppCapacityPool>( row );

        pool.NetAppAccountId = ParentOf( pool.Id, "/capacityPools/" );
        pool.ProvisioningState = properties.Str( "provisioningState" );
        pool.PoolId = properties.Str( "poolId" );

        pool.ServiceLevel = properties.Str( "serviceLevel" );
        pool.Size = properties.Long( "size" );
        pool.QosType = properties.Str( "qosType" );
        pool.TotalThroughputMibps = properties.Dbl( "totalThroughputMibps" );
        pool.UtilizedThroughputMibps = properties.Dbl( "utilizedThroughputMibps" );
        pool.CoolAccess = properties.Bool( "coolAccess" );
        pool.EncryptionType = properties.Str( "encryptionType" );

        return pool;
    }


    /// <summary />
    private static AzResource MapVolume( JsonElement row )
    {
        var properties = row.Obj( "properties" );
        var volume = Basic<AzNetAppVolume>( row );

        volume.CapacityPoolId = ParentOf( volume.Id, "/volumes/" );
        volume.NetAppAccountId = ParentOf( volume.CapacityPoolId ?? "", "/capacityPools/" );
        volume.ProvisioningState = properties.Str( "provisioningState" );
        volume.CreationToken = properties.Str( "creationToken" );
        volume.FileSystemId = properties.Str( "fileSystemId" );

        volume.ServiceLevel = properties.Str( "serviceLevel" );
        volume.UsageThreshold = properties.Long( "usageThreshold" );
        volume.ThroughputMibps = properties.Dbl( "throughputMibps" );
        volume.MaximumNumberOfFiles = properties.Long( "maximumNumberOfFiles" );
        volume.CoolAccess = properties.Bool( "coolAccess" );

        volume.ProtocolTypes = properties.StrList( "protocolTypes" );
        volume.SecurityStyle = properties.Str( "securityStyle" );
        volume.UnixPermissions = properties.Str( "unixPermissions" );
        volume.KerberosEnabled = properties.Bool( "kerberosEnabled" );
        volume.LdapEnabled = properties.Bool( "ldapEnabled" );
        volume.SnapshotDirectoryVisible = properties.Bool( "snapshotDirectoryVisible" );
        volume.EncryptionKeySource = properties.Str( "encryptionKeySource" );

        volume.SubnetId = properties.Str( "subnetId" );
        volume.NetworkFeatures = properties.Str( "networkFeatures" );

        foreach ( var target in properties.Items( "mountTargets" ) )
        {
            var address = target.Str( "ipAddress" );

            if ( address != null )
                volume.MountTargetIPAddresses.Add( address );
        }

        foreach ( var rule in properties.Obj( "exportPolicy" ).Items( "rules" ) )
        {
            volume.ExportRules.Add( new AzNetAppVolumeExportRule
            {
                RuleIndex = rule.Int( "ruleIndex" ),
                AllowedClients = rule.Str( "allowedClients" ),
                Nfsv3 = rule.Bool( "nfsv3" ),
                Nfsv41 = rule.Bool( "nfsv41" ),
                Cifs = rule.Bool( "cifs" ),
                UnixReadOnly = rule.Bool( "unixReadOnly" ),
                UnixReadWrite = rule.Bool( "unixReadWrite" ),
                HasRootAccess = rule.Bool( "hasRootAccess" ),
                ChownMode = rule.Str( "chownMode" ),
            } );
        }

        return volume;
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
