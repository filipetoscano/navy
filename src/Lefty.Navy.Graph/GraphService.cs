using Azure.Core;
using Lefty.Navy.Model;
using Microsoft.Extensions.Logging;

namespace Lefty.Navy.Graph;

/// <summary />
public class GraphService
{
    private readonly ILogger<GraphService> _logger;


    /// <summary />
    public GraphService( TokenCredential credential, ILogger<GraphService> logger )
    {
        _logger = logger;
    }


    /// <summary />
    public Task<AzSubscription> SubscriptionGet( string subscriptionName )
    {
        throw new NotImplementedException();
    }
}