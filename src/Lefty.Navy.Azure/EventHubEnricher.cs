using Azure.ResourceManager;
using Azure.ResourceManager.EventHubs;
using Lefty.Navy.Model;
using Microsoft.Extensions.Logging;

namespace Lefty.Navy.Azure;

/// <summary />
/// <remarks>
/// Resource Graph indexes an Event Hubs namespace but none of the hubs inside
/// it, so those are read from the management plane, one set of calls per
/// namespace. This is the same arrangement as <see cref="StorageEnricher" />,
/// and for the same reason.
/// <para>
/// A caller without permission to list a given namespace is the expected case
/// rather than an error: the collection is left empty, a warning is logged, and
/// the rest of the inventory is unaffected.
/// </para>
/// </remarks>
public class EventHubEnricher
{
    /// <summary>
    /// Namespaces read at once. Matches <see cref="StorageEnricher" />: enough
    /// to hide the per-call latency without provoking throttling.
    /// </summary>
    private const int Parallelism = 8;

    private readonly ArmClient _client;
    private readonly ILogger _logger;


    /// <summary />
    public EventHubEnricher( ArmClient client, ILogger logger )
    {
        _client = client;
        _logger = logger;
    }


    /// <summary />
    public async Task Enrich( List<AzEventHubNamespace> namespaces, CancellationToken cancellationToken = default )
    {
        if ( namespaces.Count == 0 )
            return;

        _logger.LogDebug( "reading the hubs of {Count} Event Hubs namespace(s)", namespaces.Count );

        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = Parallelism,
            CancellationToken = cancellationToken,
        };

        await Parallel.ForEachAsync( namespaces, options, async ( space, ct ) =>
        {
            var resource = _client.GetEventHubsNamespaceResource( new global::Azure.Core.ResourceIdentifier( space.Id ) );

            await Hubs( space, resource, ct );
        } );

        _logger.LogDebug( "read {Hubs} hub(s) across {Count} namespace(s)",
            namespaces.Sum( x => x.Hubs.Count ), namespaces.Count );
    }


    /// <summary />
    private async Task Hubs( AzEventHubNamespace space, EventHubsNamespaceResource resource, CancellationToken cancellationToken )
    {
        var hubs = new List<EventHubResource>();

        await Read( space, "hubs", async () =>
        {
            await foreach ( var item in resource.GetEventHubs().GetAllAsync( cancellationToken: cancellationToken ) )
            {
                var data = item.Data;
                var capture = data.CaptureDescription;

                /*
                 * Retention is reported twice: as whole days everywhere, and as
                 * hours where the namespace is able to be finer than a day. The
                 * SDK marks the days obsolete, but Azure still returns them and
                 * they are all a Basic or Standard namespace gives.
                 */
#pragma warning disable CS0618
                var hours = data.RetentionDescription?.RetentionTimeInHours ?? ( data.MessageRetentionInDays ?? 0 ) * 24;
#pragma warning restore CS0618

                space.Hubs.Add( new AzEventHub
                {
                    Id = data.Id!,
                    Name = data.Name,
                    Type = data.ResourceType,
                    Status = data.Status?.ToString(),
                    PartitionCount = (int) ( data.PartitionCount ?? 0 ),
                    RetentionInHours = (int) hours,
                    CleanupPolicy = data.RetentionDescription?.CleanupPolicy?.ToString(),
                    CreatedAt = data.CreatedOn,
                    CaptureEnabled = capture?.Enabled == true,
                    CaptureDestinationId = capture?.Destination?.StorageAccountResourceId?.ToString(),
                } );

                hubs.Add( item );
            }
        } );

        /*
         * Consumer groups are a call of their own per hub, so they are read
         * only once the hubs are known, and a refusal on one hub leaves the
         * others alone.
         */
        foreach ( var item in hubs )
        {
            var hub = space.Hubs.FirstOrDefault( x => x.Name == item.Data.Name );

            if ( hub == null )
                continue;

            await Read( space, "consumer groups of " + item.Data.Name, async () =>
            {
                await foreach ( var group in item.GetEventHubsConsumerGroups().GetAllAsync( cancellationToken: cancellationToken ) )
                    hub.ConsumerGroups.Add( group.Data.Name );
            } );
        }
    }


    /// <summary />
    /// <remarks>
    /// A refusal leaves the collection empty rather than failing the inventory.
    /// </remarks>
    private async Task Read( AzEventHubNamespace space, string what, Func<Task> read )
    {
        try
        {
            await read();
        }
        catch ( global::Azure.RequestFailedException ex )
        {
            _logger.LogWarning( "could not read the {What} of {Namespace}: {Status} {Error}", what, space.Name, ex.Status, ex.ErrorCode ?? ex.Message );
        }
    }
}
