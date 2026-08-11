using Azure.Core;
using Azure.ResourceManager;
using Lefty.Navy.Model;
using Microsoft.Extensions.Logging;

namespace Lefty.Navy.Azure;

/// <summary />
/// <remarks>
/// Reads the inventory of a subscription through Azure Resource Graph, which
/// returns every resource type without needing a resource provider SDK for each
/// one. The cost of that is that resource properties arrive as untyped JSON:
/// see <see cref="ResourceMapper" />.
/// </remarks>
public class AzureService
{
    /// <summary>
    /// Subscriptions visible to the credential. Deliberately unfiltered: the
    /// name is matched in memory, so that it never reaches the query text.
    /// </summary>
    private const string SubscriptionQuery = """
        resourcecontainers
        | where type =~ 'microsoft.resources/subscriptions'
        | project subscriptionId, name
        """;

    /// <summary />
    private const string ResourceGroupQuery = """
        resourcecontainers
        | where type =~ 'microsoft.resources/subscriptions/resourcegroups'
        | project id, name, tags
        | order by id asc
        """;

    /// <summary>
    /// The ordering is not cosmetic: Resource Graph requires a stable sort for
    /// continuation tokens to page consistently.
    /// </summary>
    private const string ResourceQuery = """
        resources
        | project id, name, type, location, tags, resourceGroup, sku, kind, properties
        | order by id asc
        """;

    private readonly ArmClient _client;
    private readonly ILogger<AzureService> _logger;


    /// <summary />
    public AzureService( TokenCredential credential, ILogger<AzureService> logger )
    {
        _client = new ArmClient( credential );
        _logger = logger;
    }


    /// <summary />
    /// <param name="subscriptionName">
    /// Name of the subscription, or its identifier. Matched case-insensitively.
    /// </param>
    /// <param name="stitch">
    /// Whether to resolve the identifiers which resources hold to one another
    /// into references to the objects themselves. The identifiers are populated
    /// either way; leaving them unresolved keeps the result a strict tree, in
    /// which nothing is reachable by more than one path.
    /// </param>
    public async Task<AzSubscription> SubscriptionGet( string subscriptionName, bool stitch )
    {
        using var query = new ResourceGraphQuery( _client, _logger );

        var subscription = await SubscriptionResolve( query, subscriptionName );
        var subscriptionId = subscription.Str( "subscriptionId" )!;

        _logger.LogInformation( "reading subscription {Name} ({Id})", subscription.Str( "name" ), subscriptionId );


        /*
         * Resource groups first, so that resources have somewhere to be placed.
         */
        var groupRows = await query.Execute( "resourcegroups", ResourceGroupQuery, subscriptionId );

        var groups = new List<AzResourceGroup>();
        var groupsByName = new Dictionary<string, AzResourceGroup>( StringComparer.OrdinalIgnoreCase );

        foreach ( var row in groupRows )
        {
            var group = new AzResourceGroup
            {
                Id = row.Str( "id" ) ?? "",
                Name = row.Str( "name" ) ?? "",
                Tags = row.TagMap( "tags" ),
                Resources = [],
            };

            groups.Add( group );
            groupsByName[ group.Name ] = group;
        }


        /*
         * Resources, placed into their group by name: the resources table
         * reports the group name rather than its identifier.
         */
        var resourceRows = await query.Execute( "resources", ResourceQuery, subscriptionId );

        var mapper = new ResourceMapper( _logger );
        var resources = new List<AzResource>();

        foreach ( var row in resourceRows )
        {
            var resource = mapper.Map( row );
            var groupName = row.Str( "resourceGroup" ) ?? "";

            resources.Add( resource );

            if ( groupsByName.TryGetValue( groupName, out var group ) == true )
            {
                group.Resources!.Add( resource );
            }
            else
            {
                _logger.LogWarning( "resource {Id} belongs to resource group {Group}, which was not returned", resource.Id, groupName );
            }
        }

        _logger.LogInformation( "read {Resources} resource(s) across {Groups} resource group(s)", resources.Count, groups.Count );


        /*
         * Storage account contents are not indexed by Resource Graph, and have
         * to be read from the management plane one account at a time.
         */
        var accounts = resources.OfType<AzStorageAccount>().ToList();

        await new StorageEnricher( _client, _logger ).Enrich( accounts );


        /*
         * Resolve references only once every resource is known.
         */
        if ( stitch == true )
            new ResourceLinker( _logger ).Link( resources );
        else
            _logger.LogDebug( "references between resources left unresolved" );

        return new AzSubscription
        {
            Id = Guid.Parse( subscriptionId ),
            Name = subscription.Str( "name" ) ?? "",
            ResourceGroups = groups,
        };
    }


    /// <summary />
    private async Task<System.Text.Json.JsonElement> SubscriptionResolve( ResourceGraphQuery query, string subscriptionName )
    {
        var rows = await query.Execute( "subscriptions", SubscriptionQuery, null );

        var matches = rows
            .Where( x => string.Equals( x.Str( "name" ), subscriptionName, StringComparison.OrdinalIgnoreCase ) == true
                || string.Equals( x.Str( "subscriptionId" ), subscriptionName, StringComparison.OrdinalIgnoreCase ) == true )
            .ToList();

        if ( matches.Count == 0 )
        {
            var visible = rows.Count == 0 ? "none are visible to the current credential"
                : string.Join( ", ", rows.Select( x => x.Str( "name" ) ).Order() );

            throw new AzureServiceException( $"No subscription matches '{subscriptionName}': {visible}." );
        }

        if ( matches.Count > 1 )
        {
            var candidates = string.Join( ", ", matches.Select( x => x.Str( "subscriptionId" ) ).Order() );

            throw new AzureServiceException( $"More than one subscription matches '{subscriptionName}': {candidates}. Use the subscription identifier instead." );
        }

        return matches[ 0 ];
    }
}
