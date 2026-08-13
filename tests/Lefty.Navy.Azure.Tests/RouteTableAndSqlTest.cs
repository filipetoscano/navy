using Lefty.Navy.Azure;
using Lefty.Navy.Model;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace Lefty.Navy.Tests;

/// <summary />
public class RouteTableAndSqlTest
{
    private const string ServerId = "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Sql/servers/sql-one";
    private const string SubnetId = "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/virtualNetworks/vnet-one/subnets/snet-app";
    private const string TableId = "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/routeTables/rt-one";

    private static readonly ResourceMapper Mapper = new( NullLogger<ResourceMapper>.Instance );
    private static readonly ResourceLinker Linker = new( NullLogger<ResourceLinker>.Instance );


    /// <summary />
    private static T Map<T>( string json )
        where T : AzResource
    {
        return Assert.IsType<T>( Mapper.Map( JsonDocument.Parse( json ).RootElement.Clone() ) );
    }


    /// <summary />
    [Fact]
    public void RouteTable_IsFullyMapped()
    {
        var table = Map<AzRouteTable>( RouteTableJson );

        Assert.True( table.DisableBgpRoutePropagation );
        Assert.Equal( 2, table.Routes.Count );
        Assert.Equal( [ SubnetId ], table.SubnetIds );

        var appliance = table.Routes[ 0 ];

        Assert.Equal( "udr-rfc-02", appliance.Name );
        Assert.Equal( "172.16.0.0/12", appliance.AddressPrefix );
        Assert.Equal( "VirtualAppliance", appliance.NextHopType );
        Assert.Equal( "172.30.80.244", appliance.NextHopIpAddress );
        Assert.False( appliance.HasBgpOverride );
        Assert.Equal( "Microsoft.Network/routeTables/routes", appliance.Type );

        var internet = table.Routes[ 1 ];

        Assert.Equal( "Internet", internet.NextHopType );
        Assert.Null( internet.NextHopIpAddress );
    }


    /// <summary />
    /// <remarks>
    /// A subnet already carries the table which applies to it, so the table
    /// must not resolve its subnets or the graph would close a loop.
    /// </remarks>
    [Fact]
    public void RouteTable_SubnetsAreNotResolved()
    {
        var table = Map<AzRouteTable>( RouteTableJson );
        var network = Network();

        network.Subnets[ 0 ].RouteTableId = TableId;

        Linker.Link( [ table, network ] );

        Assert.Same( table, network.Subnets[ 0 ].RouteTable );
        Assert.Equal( [ SubnetId ], table.SubnetIds );

        var json = JsonSerializer.Serialize<List<AzResource>>( [ table, network ] );

        Assert.Contains( "rt-one", json );
    }


    /// <summary />
    [Fact]
    public void SqlServer_IsFullyMapped()
    {
        var server = Map<AzSqlServer>( ServerJson );

        Assert.Equal( "12.0", server.Version );
        Assert.Equal( "Ready", server.State );
        Assert.Equal( "sql-one.database.windows.net", server.FullyQualifiedDomainName );
        Assert.Equal( "sqladminuser", server.AdministratorLogin );

        Assert.Equal( "SQL DB Admin", server.EntraAdministratorLogin );
        Assert.Equal( "Group", server.EntraAdministratorPrincipalType );
        Assert.Equal( "3dc84871-05f0-48b3-9871-dd69b083b32f", server.EntraAdministratorObjectId );
        Assert.True( server.EntraOnlyAuthentication );

        Assert.Equal( "Disabled", server.PublicNetworkAccess );
        Assert.Equal( "1.2", server.MinimalTlsVersion );
        Assert.Single( server.PrivateEndpointIds );
    }


    /// <summary />
    [Fact]
    public void SqlDatabase_IsFullyMapped()
    {
        var database = Map<AzSqlDatabase>( DatabaseJson );

        Assert.Equal( ServerId, database.ServerId );
        Assert.Equal( "v12.0,user", database.Kind );
        Assert.Equal( "Online", database.Status );

        Assert.Equal( "Standard", database.Sku );
        Assert.Equal( "Standard", database.SkuTier );
        Assert.Equal( 10, database.SkuCapacity );
        Assert.Equal( "S0", database.CurrentServiceObjectiveName );

        Assert.Equal( 268435456000L, database.MaxSizeBytes );
        Assert.Equal( "SQL_Latin1_General_CP1_CI_AS", database.Collation );
        Assert.Equal( "Local", database.RequestedBackupStorageRedundancy );
        Assert.False( database.ZoneRedundant );
        Assert.False( database.IsLedgerOn );
        Assert.Equal( 2026, database.CreationDate!.Value.Year );
    }


    /// <summary />
    /// <remarks>
    /// maxSizeBytes exceeds the range of a 32 bit integer, which is why it is
    /// read as a 64 bit one.
    /// </remarks>
    [Fact]
    public void SqlDatabase_MaxSizeSurvivesLargeValues()
    {
        var database = Map<AzSqlDatabase>( DatabaseJson.Replace( "268435456000", "4398046511104" ) );

        Assert.Equal( 4398046511104L, database.MaxSizeBytes );
    }


    /// <summary />
    [Fact]
    public void SqlDatabase_IsAttachedToItsServer()
    {
        var server = Map<AzSqlServer>( ServerJson );
        var database = Map<AzSqlDatabase>( DatabaseJson );

        Linker.Link( [ server, database ] );

        Assert.Same( database, Assert.Single( server.Databases ) );
        Assert.Equal( ServerId, database.ServerId );

        // The database names its server but must not resolve it.
        var json = JsonSerializer.Serialize<List<AzResource>>( [ server, database ] );

        Assert.Contains( "sql-one", json );
    }


    /// <summary />
    /// <remarks>
    /// A database whose server sits in a subscription which was not read is
    /// kept, rather than being discarded for having nowhere to attach.
    /// </remarks>
    [Fact]
    public void SqlDatabase_WithoutItsServer_IsStillMapped()
    {
        var database = Map<AzSqlDatabase>( DatabaseJson );

        Linker.Link( [ database ] );

        Assert.Equal( ServerId, database.ServerId );
        Assert.Equal( "Lynx_Ctc_Taxpayer", database.Name );
    }


    /// <summary />
    private static AzVirtualNetwork Network()
    {
        var network = Activator.CreateInstance<AzVirtualNetwork>();

        network.Id = "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/virtualNetworks/vnet-one";
        network.Name = "vnet-one";
        network.Type = "Microsoft.Network/virtualNetworks";
        network.Location = "westeurope";
        network.AddressPrefixes = [ "10.0.0.0/16" ];
        network.DnsServers = [];
        network.Subnets =
        [
            new AzSubnet
            {
                Id = SubnetId,
                Name = "snet-app",
                Type = "Microsoft.Network/virtualNetworks/subnets",
                AddressPrefix = "10.0.1.0/24",
            },
        ];

        return network;
    }


    private const string RouteTableJson = """
        {
          "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/routeTables/rt-one",
          "name": "rt-one",
          "type": "Microsoft.Network/routeTables",
          "location": "westeurope",
          "properties": {
            "disableBgpRoutePropagation": true,
            "provisioningState": "Succeeded",
            "routes": [
              {
                "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/routeTables/rt-one/routes/udr-rfc-02",
                "name": "udr-rfc-02",
                "type": "Microsoft.Network/routeTables/routes",
                "properties": {
                  "addressPrefix": "172.16.0.0/12",
                  "hasBgpOverride": false,
                  "nextHopIpAddress": "172.30.80.244",
                  "nextHopType": "VirtualAppliance"
                }
              },
              {
                "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/routeTables/rt-one/routes/udr-internet",
                "name": "udr-internet",
                "type": "Microsoft.Network/routeTables/routes",
                "properties": { "addressPrefix": "0.0.0.0/0", "hasBgpOverride": false, "nextHopType": "Internet" }
              }
            ],
            "subnets": [ { "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/virtualNetworks/vnet-one/subnets/snet-app" } ]
          }
        }
        """;

    private const string ServerJson = """
        {
          "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Sql/servers/sql-one",
          "name": "sql-one",
          "type": "Microsoft.Sql/servers",
          "location": "westeurope",
          "kind": "v12.0",
          "properties": {
            "version": "12.0",
            "state": "Ready",
            "fullyQualifiedDomainName": "sql-one.database.windows.net",
            "administratorLogin": "sqladminuser",
            "administrators": {
              "administratorType": "ActiveDirectory",
              "azureADOnlyAuthentication": true,
              "login": "SQL DB Admin",
              "principalType": "Group",
              "sid": "3dc84871-05f0-48b3-9871-dd69b083b32f"
            },
            "publicNetworkAccess": "Disabled",
            "minimalTlsVersion": "1.2",
            "restrictOutboundNetworkAccess": "Disabled",
            "privateEndpointConnections": [
              {
                "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Sql/servers/sql-one/privateEndpointConnections/one",
                "properties": { "privateEndpoint": { "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/privateEndpoints/pe-sql" } }
              }
            ]
          }
        }
        """;

    private const string DatabaseJson = """
        {
          "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Sql/servers/sql-one/databases/Lynx_Ctc_Taxpayer",
          "name": "Lynx_Ctc_Taxpayer",
          "type": "Microsoft.Sql/servers/databases",
          "location": "westeurope",
          "kind": "v12.0,user",
          "sku": { "name": "Standard", "tier": "Standard", "capacity": 10 },
          "properties": {
            "status": "Online",
            "databaseId": "0d1b2c3d-4e5f-6789-abcd-ef0123456789",
            "collation": "SQL_Latin1_General_CP1_CI_AS",
            "catalogCollation": "SQL_Latin1_General_CP1_CI_AS",
            "maxSizeBytes": 268435456000,
            "zoneRedundant": false,
            "readScale": "Disabled",
            "requestedBackupStorageRedundancy": "Local",
            "currentBackupStorageRedundancy": "Local",
            "currentServiceObjectiveName": "S0",
            "requestedServiceObjectiveName": "S0",
            "isLedgerOn": false,
            "creationDate": "2026-02-09T16:38:50.0000000Z"
          }
        }
        """;
}
