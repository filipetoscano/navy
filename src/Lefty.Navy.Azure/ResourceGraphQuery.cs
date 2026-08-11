using Azure.ResourceManager;
using Azure.ResourceManager.ResourceGraph;
using Azure.ResourceManager.ResourceGraph.Models;
using Azure.ResourceManager.Resources;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Lefty.Navy.Azure;

/// <summary />
/// <remarks>
/// The only type which talks to the Azure SDK. Runs a KQL query against Azure
/// Resource Graph, following the continuation token until every page has been
/// read.
/// <para>
/// Rows are returned as <see cref="JsonElement" />, which are views over the
/// buffer owned by the underlying <see cref="JsonDocument" />: the documents
/// are retained until this instance is disposed, so callers must keep it alive
/// for as long as they intend to read the rows.
/// </para>
/// </remarks>
internal class ResourceGraphQuery : IDisposable
{
    /// <summary>
    /// Maximum number of rows which Resource Graph will return in a single page.
    /// </summary>
    private const int PageSize = 1000;

    private readonly ArmClient _client;
    private readonly ILogger _logger;
    private readonly List<JsonDocument> _documents = [];
    private TenantResource? _tenant;


    /// <summary />
    public ResourceGraphQuery( ArmClient client, ILogger logger )
    {
        _client = client;
        _logger = logger;
    }


    /// <summary />
    /// <param name="name">Short name of the query, for diagnostics only.</param>
    /// <param name="kql">Query to run.</param>
    /// <param name="subscriptionId">
    /// Subscription to scope the query to, or null to query every subscription
    /// which the credential can see. Scope is always applied through the request
    /// rather than by interpolating into <paramref name="kql" />.
    /// </param>
    public async Task<List<JsonElement>> Execute( string name, string kql, string? subscriptionId, CancellationToken cancellationToken = default )
    {
        var tenant = await TenantGet( cancellationToken );

        var content = new ResourceQueryContent( kql );

        if ( subscriptionId != null )
            content.Subscriptions.Add( subscriptionId );

        content.Options = new ResourceQueryRequestOptions
        {
            ResultFormat = ResultFormat.ObjectArray,
            Top = PageSize,
        };

        var rows = new List<JsonElement>();
        var page = 0;

        while ( true )
        {
            var response = await tenant.GetResourcesAsync( content, cancellationToken );
            var result = response.Value;

            page++;

            var document = JsonDocument.Parse( result.Data );
            _documents.Add( document );

            if ( document.RootElement.ValueKind == JsonValueKind.Array )
                rows.AddRange( document.RootElement.EnumerateArray() );

            _logger.LogDebug( "query {Query}: page {Page} returned {Count} row(s) of {Total}", name, page, result.Count, result.TotalRecords );

            if ( result.ResultTruncated == ResultTruncated.True )
                _logger.LogWarning( "query {Query}: Resource Graph truncated the results, the inventory is incomplete", name );

            if ( string.IsNullOrEmpty( result.SkipToken ) == true )
                break;

            content.Options.SkipToken = result.SkipToken;
        }

        _logger.LogDebug( "query {Query}: {Count} row(s) over {Pages} page(s)", name, rows.Count, page );

        return rows;
    }


    /// <summary />
    /// <remarks>
    /// Resource Graph is a tenant-level operation. The tenant is resolved once
    /// and reused across every query issued by this instance.
    /// </remarks>
    private async Task<TenantResource> TenantGet( CancellationToken cancellationToken )
    {
        if ( _tenant != null )
            return _tenant;

        await foreach ( var tenant in _client.GetTenants().GetAllAsync( cancellationToken ) )
        {
            _tenant = tenant;
            break;
        }

        if ( _tenant == null )
            throw new InvalidOperationException( "The credential does not have access to any Azure tenant." );

        return _tenant;
    }


    /// <summary />
    public void Dispose()
    {
        foreach ( var document in _documents )
            document.Dispose();

        _documents.Clear();
    }
}
