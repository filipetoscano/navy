using Lefty.Navy.Azure;
using Lefty.Navy.Model;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace Lefty.Navy.Tests;

/// <summary />
public class EventHubTest
{
    private const string EndpointId = "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/privateEndpoints/pe-one";
    private const string NamespaceId = "/subscriptions/s/resourceGroups/rg/providers/Microsoft.EventHub/namespaces/evhns-one";

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
    public void EventHubNamespace_IsFullyMapped()
    {
        var space = Map<AzEventHubNamespace>( NamespaceJson );

        Assert.Equal( "Standard", space.Sku );
        Assert.Equal( "Standard", space.SkuTier );
        Assert.Equal( 1, space.SkuCapacity );

        Assert.Equal( "Succeeded", space.ProvisioningState );
        Assert.Equal( "Active", space.Status );
        Assert.Equal( 2026, space.CreatedAt!.Value.Year );

        Assert.Equal( "https://evhns-one.servicebus.windows.net:443/", space.ServiceBusEndpoint );
        Assert.True( space.KafkaEnabled );
        Assert.False( space.IsAutoInflateEnabled );
        Assert.Equal( 0, space.MaximumThroughputUnits );
        Assert.True( space.ZoneRedundant );

        Assert.False( space.DisableLocalAuth );
        Assert.Equal( "1.2", space.MinimumTlsVersion );
        Assert.Equal( "Disabled", space.PublicNetworkAccess );
    }


    /// <summary />
    [Fact]
    public void EventHubNamespace_PrivateEndpointIsResolved()
    {
        var space = Map<AzEventHubNamespace>( NamespaceJson );
        var endpoint = Map<AzPrivateEndpoint>( EndpointJson );

        Linker.Link( [ space, endpoint ] );

        Assert.Same( endpoint, Assert.Single( space.PrivateEndpoints ) );
    }


    /// <summary />
    /// <remarks>
    /// Resource Graph does not index the hubs inside a namespace, so a mapped
    /// namespace starts out with none: they arrive later, from the management
    /// plane, by way of the enricher.
    /// </remarks>
    [Fact]
    public void EventHubNamespace_HubsStartEmpty()
    {
        var space = Map<AzEventHubNamespace>( NamespaceJson );

        Assert.Empty( space.Hubs );
    }


    /// <summary />
    /// <remarks>
    /// A namespace the caller may not list is the expected case rather than an
    /// error, and leaves the hubs empty without failing the inventory.
    /// </remarks>
    [Fact]
    public void EventHubNamespace_WithoutHubs_StillSerializes()
    {
        var space = Map<AzEventHubNamespace>( NamespaceJson );

        Linker.Link( [ space ] );

        var json = JsonSerializer.Serialize<AzResource>( space );

        Assert.Contains( "AzEventHubNamespace", json );
        Assert.Contains( "evhns-one", json );
    }


    /// <summary />
    /// <remarks>
    /// The hub is not mapped from a Resource Graph row, so what is guarded here
    /// is that it hangs off its namespace and survives serialization along with
    /// it.
    /// </remarks>
    [Fact]
    public void EventHub_IsHeldByItsNamespace()
    {
        var space = Map<AzEventHubNamespace>( NamespaceJson );

        space.Hubs.Add( new AzEventHub
        {
            Id = NamespaceId + "/eventhubs/evh-one",
            Name = "evh-one",
            Type = "Microsoft.EventHub/namespaces/eventhubs",
            Status = "Active",
            PartitionCount = 4,
            RetentionInHours = 24,
            CleanupPolicy = "Delete",
            CaptureEnabled = true,
            CaptureDestinationId = "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Storage/storageAccounts/stone",
            ConsumerGroups = [ "$Default", "billing" ],
        } );

        var hub = Assert.Single( space.Hubs );

        Assert.Equal( "evh-one", hub.Name );
        Assert.Equal( 4, hub.PartitionCount );
        Assert.Equal( [ "$Default", "billing" ], hub.ConsumerGroups );

        var json = JsonSerializer.Serialize<AzResource>( space );

        Assert.Contains( "evh-one", json );
        Assert.Contains( "$Default", json );
    }


    private const string NamespaceJson = """
        {
          "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.EventHub/namespaces/evhns-one",
          "name": "evhns-one",
          "type": "microsoft.eventhub/namespaces",
          "location": "westeurope",
          "sku": { "capacity": 1, "name": "Standard", "tier": "Standard" },
          "properties": {
            "createdAt": "2026-04-26T11:03:23.9873612Z",
            "disableLocalAuth": false,
            "errors": [],
            "isAutoInflateEnabled": false,
            "kafkaEnabled": true,
            "maximumThroughputUnits": 0,
            "metricId": "s:evhns-one",
            "minimumTlsVersion": "1.2",
            "privateEndpointConnections": [
              {
                "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.EventHub/namespaces/evhns-one/privateEndpointConnections/one",
                "properties": { "privateEndpoint": { "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/privateEndpoints/pe-one" } }
              }
            ],
            "provisioningState": "Succeeded",
            "publicNetworkAccess": "Disabled",
            "serviceBusEndpoint": "https://evhns-one.servicebus.windows.net:443/",
            "status": "Active",
            "updatedAt": "2026-06-23T06:55:50Z",
            "zoneRedundant": true
          }
        }
        """;

    private const string EndpointJson = """
        {
          "id": "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/privateEndpoints/pe-one",
          "name": "pe-one",
          "type": "microsoft.network/privateendpoints",
          "location": "westeurope",
          "properties": { "provisioningState": "Succeeded" }
        }
        """;
}
