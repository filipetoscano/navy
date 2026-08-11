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
    /// <remarks>
    /// Without stitching the inventory is a strict tree: each resource appears
    /// exactly once, and references to other resources are reported as
    /// identifiers rather than being followed. Stitching resolves them, at the
    /// cost of writing a resource out once per path which reaches it.
    /// </remarks>
    [Option( "--no-stitch", CommandOptionType.NoValue, Description = "Report references between resources as identifiers, without resolving them" )]
    public bool NoStitch { get; set; }


    /// <summary />
    public async Task<int> OnExecuteAsync()
    {
        try
        {
            var sub = await _svc.SubscriptionGet( this.Subscription!, this.NoStitch == false );

            var json = JsonSerializer.Serialize( sub, Options );
            Console.WriteLine( json );

            return 0;
        }
        catch ( AzureServiceException ex )
        {
            _logger.LogError( ex, "{Message}", ex.Message );

            return 1;
        }
    }
}
