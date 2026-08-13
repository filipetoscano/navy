using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.Storage;
using Lefty.Navy.Model;
using Microsoft.Extensions.Logging;

namespace Lefty.Navy.Azure;

/// <summary />
/// <remarks>
/// Resource Graph indexes a storage account but none of its containers, shares,
/// queues or tables, so those are read from the management plane, one set of
/// calls per account. Being management plane calls they are unaffected by the
/// account firewall or by private endpoints, and need no data plane role and no
/// account key.
/// <para>
/// A caller without permission to list a given account is the expected case
/// rather than an error: the collection is left empty, a warning is logged, and
/// the rest of the inventory is unaffected.
/// </para>
/// </remarks>
public class StorageEnricher
{
    /// <summary>
    /// Accounts read at once. Enough to hide the per-call latency without
    /// provoking throttling on a subscription holding many accounts.
    /// </summary>
    private const int Parallelism = 8;

    private readonly ArmClient _client;
    private readonly ILogger<StorageEnricher> _logger;


    /// <summary />
    public StorageEnricher( ArmClient client, ILogger<StorageEnricher> logger )
    {
        _client = client;
        _logger = logger;
    }


    /// <summary />
    public async Task Enrich( List<AzStorageAccount> accounts, CancellationToken cancellationToken = default )
    {
        if ( accounts.Count == 0 )
            return;

        _logger.LogDebug( "reading the contents of {Count} storage account(s)", accounts.Count );

        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = Parallelism,
            CancellationToken = cancellationToken,
        };

        await Parallel.ForEachAsync( accounts, options, async ( account, ct ) =>
        {
            var resource = _client.GetStorageAccountResource( new ResourceIdentifier( account.Id ) );

            await BlobContainers( account, resource, ct );
            await FileShares( account, resource, ct );
            await Queues( account, resource, ct );
            await Tables( account, resource, ct );
        } );

        _logger.LogDebug( "read {Containers} container(s), {Shares} share(s), {Queues} queue(s), {Tables} table(s)",
            accounts.Sum( x => x.BlobContainers.Count ),
            accounts.Sum( x => x.FileShares.Count ),
            accounts.Sum( x => x.Queues.Count ),
            accounts.Sum( x => x.Tables.Count ) );
    }


    /// <summary />
    private async Task BlobContainers( AzStorageAccount account, StorageAccountResource resource, CancellationToken cancellationToken )
    {
        await Read( account, "blob containers", async () =>
        {
            await foreach ( var item in resource.GetBlobService().GetBlobContainers().GetAllAsync( cancellationToken: cancellationToken ) )
            {
                var data = item.Data;

                account.BlobContainers.Add( new AzBlobContainer
                {
                    Id = data.Id!,
                    Name = data.Name,
                    Type = data.ResourceType,
                    PublicAccess = data.PublicAccess?.ToString(),
                    HasImmutabilityPolicy = data.HasImmutabilityPolicy == true,
                    HasLegalHold = data.HasLegalHold == true,
                    IsVersioningEnabled = data.ImmutableStorageWithVersioning?.IsEnabled == true,
                    DefaultEncryptionScope = data.DefaultEncryptionScope,
                    PreventEncryptionScopeOverride = data.PreventEncryptionScopeOverride == true,
                    LeaseState = data.LeaseState?.ToString(),
                    LeaseStatus = data.LeaseStatus?.ToString(),
                    LastModifiedOn = data.LastModifiedOn,
                } );
            }
        } );
    }


    /// <summary />
    private async Task FileShares( AzStorageAccount account, StorageAccountResource resource, CancellationToken cancellationToken )
    {
        await Read( account, "file shares", async () =>
        {
            await foreach ( var item in resource.GetFileService().GetFileShares().GetAllAsync( cancellationToken: cancellationToken ) )
            {
                var data = item.Data;

                account.FileShares.Add( new AzFileShare
                {
                    Id = data.Id!,
                    Name = data.Name,
                    Type = data.ResourceType,
                    QuotaInGB = data.ShareQuota,
                    EnabledProtocol = data.EnabledProtocol?.ToString(),
                    AccessTier = data.AccessTier?.ToString(),
                    LeaseState = data.LeaseState?.ToString(),
                    LastModifiedOn = data.LastModifiedOn,
                } );
            }
        } );
    }


    /// <summary />
    private async Task Queues( AzStorageAccount account, StorageAccountResource resource, CancellationToken cancellationToken )
    {
        await Read( account, "queues", async () =>
        {
            await foreach ( var item in resource.GetQueueService().GetStorageQueues().GetAllAsync( cancellationToken: cancellationToken ) )
            {
                var data = item.Data;
                var queue = new AzStorageQueue
                {
                    Id = data.Id!,
                    Name = data.Name,
                    Type = data.ResourceType,
                };

                if ( data.Metadata != null )
                {
                    foreach ( var entry in data.Metadata )
                        queue.Metadata[ entry.Key ] = entry.Value;
                }

                account.Queues.Add( queue );
            }
        } );
    }


    /// <summary />
    private async Task Tables( AzStorageAccount account, StorageAccountResource resource, CancellationToken cancellationToken )
    {
        await Read( account, "tables", async () =>
        {
            await foreach ( var item in resource.GetTableService().GetTables().GetAllAsync( cancellationToken ) )
            {
                var data = item.Data;

                account.Tables.Add( new AzStorageTable
                {
                    Id = data.Id!,
                    Name = data.Name,
                    Type = data.ResourceType,
                } );
            }
        } );
    }


    /// <summary />
    /// <remarks>
    /// A refusal or a service which the account does not offer leaves the
    /// collection empty rather than failing the inventory.
    /// </remarks>
    private async Task Read( AzStorageAccount account, string what, Func<Task> read )
    {
        try
        {
            await read();
        }
        catch ( global::Azure.RequestFailedException ex )
        {
            _logger.LogWarning( "could not read the {What} of {Account}: {Status} {Error}", what, account.Name, ex.Status, ex.ErrorCode ?? ex.Message );
        }
    }
}
