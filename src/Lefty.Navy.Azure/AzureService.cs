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

    private readonly ResourceGraphQuery _query;
    private readonly ResourceMapper _mapper;
    private readonly StorageEnricher _storage;
    private readonly EventHubEnricher _hubs;
    private readonly ResourceLinker _linker;
    private readonly ILogger<AzureService> _logger;


    /// <summary />
    public AzureService( ResourceGraphQuery query, ResourceMapper mapper, StorageEnricher storage, EventHubEnricher hubs, ResourceLinker linker, ILogger<AzureService> logger )
    {
        _query = query;
        _mapper = mapper;
        _storage = storage;
        _hubs = hubs;
        _linker = linker;
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
    /// <param name="cancellationToken">
    /// Cancelling abandons the inventory rather than returning a partial one:
    /// the exception propagates, and whatever had been read is discarded.
    /// </param>
    public async Task<AzSubscription> SubscriptionGetAsync( string subscriptionName, bool stitch, CancellationToken cancellationToken = default )
    {
        /*
         * Released here rather than by the container: every row read through it
         * is a view over a buffer which it owns, and nothing which outlives this
         * call still points into them.
         */
        using var query = _query;

        var subscription = await SubscriptionResolve( query, subscriptionName, cancellationToken );
        var subscriptionId = subscription.Str( "subscriptionId" )!;

        _logger.LogInformation( "reading subscription {Name} ({Id})", subscription.Str( "name" ), subscriptionId );


        /*
         * Resource groups first, so that resources have somewhere to be placed.
         */
        var groupRows = await query.Execute( "resourcegroups", ResourceGroupQuery, subscriptionId, cancellationToken );

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
        var resourceRows = await query.Execute( "resources", ResourceQuery, subscriptionId, cancellationToken );

        var resources = new List<AzResource>();

        foreach ( var row in resourceRows )
        {
            var resource = _mapper.Map( row );
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
         * Storage account contents and the hubs of an Event Hubs namespace are
         * not indexed by Resource Graph, and have to be read from the
         * management plane one resource at a time.
         */
        var accounts = resources.OfType<AzStorageAccount>().ToList();

        await _storage.Enrich( accounts, cancellationToken );

        var namespaces = resources.OfType<AzEventHubNamespace>().ToList();

        await _hubs.Enrich( namespaces, cancellationToken );


        /*
         * Resolve references only once every resource is known.
         */
        if ( stitch == true )
            _linker.Link( resources );
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
    private async Task<System.Text.Json.JsonElement> SubscriptionResolve( ResourceGraphQuery query, string subscriptionName, CancellationToken cancellationToken )
    {
        var rows = await query.Execute( "subscriptions", SubscriptionQuery, null, cancellationToken );

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
