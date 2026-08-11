using Lefty.Navy.Azure;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Lefty.Navy;

/// <summary />
[Command( Name = "build", Description = "Builds an inventory" )]
public class BuildCommand
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
    };

    private readonly AzureService _svc;
    private readonly ILogger<BuildCommand> _logger;


    /// <summary />
    public BuildCommand( AzureService svc, ILogger<BuildCommand> logger )
    {
        _svc = svc;
        _logger = logger;
    }


    /// <summary />
    [Argument( 0 )]
    [Required]
    public string? Subscription { get; set; }


    /// <summary />
    public async Task<int> OnExecuteAsync()
    {
        try
        {
            var sub = await _svc.SubscriptionGet( this.Subscription! );

            var json = JsonSerializer.Serialize( sub, Options );
            Console.WriteLine( json );

            return 0;
        }
        catch ( AzureServiceException ex )
        {
            _logger.LogError( "{Message}", ex.Message );

            return 1;
        }
    }
}
