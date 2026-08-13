using Lefty.Navy.Azure;
using Lefty.Navy.Model;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace Lefty.Navy.Tests;

/// <summary />
public class InventoryLoaderTest
{
    private const string Providers = "/subscriptions/s/resourceGroups/rg-one/providers";
    private const string NetworkId = Providers + "/Microsoft.Network/virtualNetworks/vnet-one";
    private const string SubnetId = NetworkId + "/subnets/snet-one";
    private const string NicId = Providers + "/Microsoft.Network/networkInterfaces/nic-one";
    private const string EndpointId = Providers + "/Microsoft.Network/privateEndpoints/pe-one";
    private const string VaultId = Providers + "/Microsoft.KeyVault/vaults/kv-one";

    private static readonly ResourceLinker Linker = new( NullLogger<ResourceLinker>.Instance );
    private static readonly InventoryLoader Loader = new( Linker, NullLogger<InventoryLoader>.Instance );


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


    /// <summary>
    /// A vault fronted by a private endpoint, whose interface sits in the one
    /// subnet: every kind of reference the linker resolves, in miniature.
    /// </summary>
    private static AzSubscription Subscription()
    {
        var network = Resource<AzVirtualNetwork>( NetworkId, "Microsoft.Network/virtualNetworks" );

        network.AddressPrefixes = [ "10.0.0.0/16" ];
        network.DnsServers = [];
        network.Subnets =
        [
            new AzSubnet
            {
                Id = SubnetId,
                Name = "snet-one",
                Type = "Microsoft.Network/virtualNetworks/subnets",
                AddressPrefix = "10.0.1.0/24",
            },
        ];

        var nic = Resource<AzNetworkInterface>( NicId, "Microsoft.Network/networkInterfaces" );

        nic.PrivateEndpointId = EndpointId;
        nic.IPConfigurations =
        [
            new AzNetworkInterfaceIPConfiguration
            {
                Id = NicId + "/ipConfigurations/one",
                Name = "one",
                Type = "Microsoft.Network/networkInterfaces/ipConfigurations",
                PrivateIPAddress = "10.0.1.4",
                SubnetId = SubnetId,
            },
        ];

        var endpoint = Resource<AzPrivateEndpoint>( EndpointId, "Microsoft.Network/privateEndpoints" );

        endpoint.SubnetId = SubnetId;
        endpoint.NetworkInterfaceIds = [ NicId ];

        var vault = Resource<AzKeyVault>( VaultId, "Microsoft.KeyVault/vaults" );

        vault.Sku = "standard";
        vault.PrivateEndpointId = EndpointId;

        return new AzSubscription
        {
            Id = Guid.Empty,
            Name = "sub-one",
            ResourceGroups =
            [
                new AzResourceGroup
                {
                    Id = "/subscriptions/s/resourceGroups/rg-one",
                    Name = "rg-one",
                    Tags = new Dictionary<string, string> { [ "environment" ] = "Prod" },
                    Resources = [ network, nic, endpoint, vault ],
                },
                new AzResourceGroup
                {
                    Id = "/subscriptions/s/resourceGroups/rg-two",
                    Name = "rg-two",
                    Tags = new Dictionary<string, string> { [ "Environment" ] = "dev" },
                    Resources = [],
                },
                new AzResourceGroup
                {
                    Id = "/subscriptions/s/resourceGroups/rg-three",
                    Name = "rg-three",
                    Tags = [],
                    Resources = [],
                },
            ],
        };
    }


    /// <summary>
    /// An inventory as the build command writes it: stitched, and therefore
    /// holding a copy of each resolved reference at every path which reaches
    /// it.
    /// </summary>
    private static string Written()
    {
        var subscription = Subscription();

        Linker.Link( [ .. ( subscription.ResourceGroups ?? [] ).SelectMany( x => x.Resources ?? [] ) ] );

        return JsonSerializer.Serialize( subscription, new JsonSerializerOptions { WriteIndented = true } );
    }


    /// <summary />
    private static Inventory Read( string json )
    {
        return Loader.Stitch( JsonSerializer.Deserialize<AzSubscription>( json )! );
    }


    /// <summary>
    /// What the whole exercise is for: the copies which the file holds are
    /// dropped, and every path leads to the one resource the inventory holds.
    /// </summary>
    [Fact]
    public void Stitch_AfterRoundTrip_ResolvesOntoTheSameInstance()
    {
        var inventory = Read( Written() );

        var network = inventory.Resources.OfType<AzVirtualNetwork>().Single();
        var nic = inventory.Resources.OfType<AzNetworkInterface>().Single();
        var endpoint = inventory.Resources.OfType<AzPrivateEndpoint>().Single();
        var vault = inventory.Resources.OfType<AzKeyVault>().Single();

        Assert.Same( network.Subnets[ 0 ], nic.IPConfigurations[ 0 ].Subnet );
        Assert.Same( network.Subnets[ 0 ], endpoint.Subnet );
        Assert.Same( nic, endpoint.NetworkInterfaces.Single() );
        Assert.Same( endpoint, vault.PrivateEndpoint );
    }


    /// <summary>
    /// The file already lists the endpoint's interface, so stitching it a
    /// second time must replace that list rather than append to it.
    /// </summary>
    [Fact]
    public void Stitch_AfterRoundTrip_DoesNotDuplicate()
    {
        var inventory = Read( Written() );

        var endpoint = inventory.Resources.OfType<AzPrivateEndpoint>().Single();

        Assert.Single( endpoint.NetworkInterfaces );
    }


    /// <summary>
    /// An inventory written with --no-stitch holds identifiers and nothing
    /// else, and is stitched by the same pass.
    /// </summary>
    [Fact]
    public void Stitch_Unstitched_ResolvesReferences()
    {
        var json = JsonSerializer.Serialize( Subscription(), new JsonSerializerOptions { WriteIndented = true } );

        var inventory = Read( json );

        var endpoint = inventory.Resources.OfType<AzPrivateEndpoint>().Single();
        var vault = inventory.Resources.OfType<AzKeyVault>().Single();

        Assert.Same( endpoint, vault.PrivateEndpoint );
        Assert.Single( endpoint.NetworkInterfaces );
    }


    /// <summary />
    [Fact]
    public void ResourceGroupsOf_MatchesTagIgnoringCase()
    {
        var inventory = Read( Written() );

        var groups = inventory.ResourceGroupsOf( "prod" );

        Assert.Equal( "rg-one", Assert.Single( groups ).Name );
    }


    /// <summary />
    [Fact]
    public void ResourceGroupsOf_UnknownEnvironment_IsEmpty()
    {
        var inventory = Read( Written() );

        Assert.Empty( inventory.ResourceGroupsOf( "qa" ) );
    }


    /// <summary />
    [Fact]
    public void Environments_AreListedOnce()
    {
        var inventory = Read( Written() );

        Assert.Equal( new[] { "dev", "Prod" }, inventory.Environments() );
    }


    /// <summary />
    [Fact]
    public async Task LoadAsync_ReadsFile()
    {
        var path = Path.GetTempFileName();

        try
        {
            await File.WriteAllTextAsync( path, Written() );

            var inventory = await Loader.LoadAsync( path );

            Assert.Equal( "sub-one", inventory.Subscription.Name );
            Assert.Equal( 4, inventory.Resources.Count );
        }
        finally
        {
            File.Delete( path );
        }
    }


    /// <summary>
    /// A file which is not an inventory is reported as a message rather than as
    /// a stack trace.
    /// </summary>
    [Fact]
    public async Task LoadAsync_NotAnInventory_Throws()
    {
        var path = Path.GetTempFileName();

        try
        {
            await File.WriteAllTextAsync( path, "{ nonsense" );

            await Assert.ThrowsAsync<AzureServiceException>( () => Loader.LoadAsync( path ) );
        }
        finally
        {
            File.Delete( path );
        }
    }
}
