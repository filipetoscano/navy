using Azure.Core;
using Lefty.Navy.Model;
using Microsoft.Extensions.Logging;

namespace Lefty.Navy.Azure;

/// <summary />
public class AzureService
{
    private readonly ILogger<AzureService> _logger;


    /// <summary />
    public AzureService( TokenCredential credential, ILogger<AzureService> logger )
    {
        _logger = logger;
    }


    /// <summary />
    public Task<AzSubscription> SubscriptionGet( string subscriptionName )
    {
        throw new NotImplementedException();
    }
}