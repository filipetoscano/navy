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
    [Option( "-o|--output-file", CommandOptionType.SingleValue, Description = "Output file" )]
    public string? OutputFile { get; set; }


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
    /// <param name="cancellationToken">
    /// Cancelled when the process is interrupted. Nothing is written on the way
    /// out: the output file is only opened once the whole inventory has been
    /// read, so an interrupted run leaves no half-written file behind.
    /// </param>
    public async Task<int> OnExecuteAsync( CancellationToken cancellationToken )
    {
        try
        {
            var sub = await _svc.SubscriptionGetAsync( this.Subscription!, this.NoStitch == false, cancellationToken );

            var json = JsonSerializer.Serialize( sub, JSO.Default );

            if ( this.OutputFile != null )
            {
                File.WriteAllText( this.OutputFile, json );
            }
            else
            {
                Console.WriteLine( json );
            }

            return 0;
        }
        catch ( AzureServiceException ex )
        {
            _logger.LogError( ex, "{Message}", ex.Message );

            return 1;
        }
    }
}
