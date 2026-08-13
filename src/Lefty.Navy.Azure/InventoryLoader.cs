using Lefty.Navy.Model;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Lefty.Navy.Azure;

/// <summary />
/// <remarks>
/// Reads back an inventory which the build command wrote, and puts the object
/// graph together again.
/// <para>
/// Stitching does not survive a round trip through JSON. A reference resolved
/// by <see cref="ResourceLinker" /> is written inline at every path which
/// reaches it, so reading the file back produces one copy per path instead of
/// one shared object: a subnet reached through a network interface is no longer
/// the subnet held by its virtual network, and a resource reached through no
/// path at all carries nothing but the identifiers it started with. The
/// identifiers are written out alongside the objects, which is what makes the
/// graph repairable: linking the resources a second time resolves every one of
/// them back onto the single instance which the inventory holds, and the copies
/// are dropped.
/// </para>
/// <para>
/// An inventory written with <c>--no-stitch</c> is repaired by the same pass:
/// there are no copies to drop, and the identifiers are resolved for the first
/// time.
/// </para>
/// </remarks>
public class InventoryLoader
{
    private const string EnvironmentTag = "Environment";

    private readonly ResourceLinker _linker;
    private readonly ILogger<InventoryLoader> _logger;


    /// <summary />
    public InventoryLoader( ResourceLinker linker, ILogger<InventoryLoader> logger )
    {
        _linker = linker;
        _logger = logger;
    }


    /// <summary />
    /// <param name="path">
    /// File written by the build command.
    /// </param>
    /// <exception cref="AzureServiceException">
    /// The file is not an inventory, or is one which this version cannot read.
    /// </exception>
    public async Task<Inventory> LoadAsync( string path, CancellationToken cancellationToken = default )
    {
        AzSubscription? subscription;

        try
        {
            using var ins = File.OpenRead( path );

            subscription = await JsonSerializer.DeserializeAsync<AzSubscription>( ins, default( JsonSerializerOptions? ), cancellationToken );
        }
        catch ( JsonException ex )
        {
            throw new AzureServiceException( $"'{path}' is not an inventory which navy can read: {ex.Message}", ex );
        }

        if ( subscription == null )
            throw new AzureServiceException( $"'{path}' holds no inventory." );

        return Stitch( subscription );
    }


    /// <summary />
    public Inventory Stitch( AzSubscription subscription )
    {
        var resources = ( subscription.ResourceGroups ?? [] )
            .SelectMany( x => x.Resources ?? [] )
            .ToList();

        _logger.LogInformation( "loaded {Subscription} with {Groups} resource group(s) and {Resources} resource(s)",
            subscription.Name, subscription.ResourceGroups?.Count ?? 0, resources.Count );

        _linker.Link( resources );

        return new Inventory
        {
            Subscription = subscription,
            Resources = resources,
        };
    }


    /// <summary>
    /// Value of the Environment tag, or null when the resource group carries
    /// none.
    /// </summary>
    /// <remarks>
    /// The key is matched without regard to case: Azure preserves the case a
    /// tag was written in, and the same tag is spelled differently across
    /// subscriptions. Reading the inventory back loses the comparer the
    /// dictionary was built with, so the match cannot be left to the dictionary
    /// itself.
    /// </remarks>
    internal static string? EnvironmentOf( AzResourceGroup group )
    {
        foreach ( var tag in group.Tags )
        {
            if ( string.Equals( tag.Key, EnvironmentTag, StringComparison.OrdinalIgnoreCase ) == true )
                return tag.Value;
        }

        return null;
    }
}


/// <summary />
public class Inventory
{
    /// <summary />
    public required AzSubscription Subscription { get; init; }

    /// <summary>
    /// Every resource in the subscription, flattened out of the groups which
    /// hold them.
    /// </summary>
    public required List<AzResource> Resources { get; init; }


    /// <summary>
    /// Resource groups tagged as belonging to an environment, ordered by name.
    /// </summary>
    public List<AzResourceGroup> ResourceGroupsOf( string environment )
    {
        return [ .. ( this.Subscription.ResourceGroups ?? [] )
            .Where( x => string.Equals( InventoryLoader.EnvironmentOf( x ), environment, StringComparison.OrdinalIgnoreCase ) == true )
            .OrderBy( x => x.Name, StringComparer.OrdinalIgnoreCase ) ];
    }


    /// <summary>
    /// Environments which the subscription holds, for reporting that the one
    /// which was asked for is not among them.
    /// </summary>
    public List<string> Environments()
    {
        return [ .. ( this.Subscription.ResourceGroups ?? [] )
            .Select( InventoryLoader.EnvironmentOf )
            .Where( x => string.IsNullOrEmpty( x ) == false )
            .Select( x => x! )
            .Distinct( StringComparer.OrdinalIgnoreCase )
            .Order( StringComparer.OrdinalIgnoreCase ) ];
    }
}
