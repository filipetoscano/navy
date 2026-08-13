using Lefty.Navy.Azure;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Json;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Lefty.Navy;

/// <summary />
[Command( Name = "network", Description = "Emits IP/resource network layout for an enviroment" )]
public class NetworkCommand
{
    private readonly InventoryLoader _loader;
    private readonly NetworkLayout _layout;
    private readonly ILogger<NetworkCommand> _logger;


    /// <summary />
    public NetworkCommand( InventoryLoader loader, NetworkLayout layout, ILogger<NetworkCommand> logger )
    {
        _loader = loader;
        _layout = layout;
        _logger = logger;
    }


    /// <summary />
    [Argument( 0, Name = "input", Description = "Input file" )]
    [Required]
    [FileExists]
    public string? InputFile { get; set; }

    /// <summary />
    [Argument( 1, Name = "env", Description = "Environment" )]
    [Required]
    public string? Environment { get; set; }

    /// <summary />
    [Option( "-o|--output-file", CommandOptionType.SingleValue, Description = "Output file" )]
    public string? OutputFile { get; set; }

    /// <summary />
    [Option( "-j|--json", CommandOptionType.NoValue, Description = "Emit as JSON, otherwise table" )]
    public bool AsJson { get; set; }


    /// <summary />
    public async Task<int> OnExecuteAsync( CancellationToken cancellationToken )
    {
        /*
         * The inventory is stitched on the way in: an address means nothing
         * without the subnet which hands it out, and a subnet is only reached
         * by resolving the identifier which the resource holds.
         */
        Inventory inventory;

        try
        {
            inventory = await _loader.LoadAsync( this.InputFile!, cancellationToken );
        }
        catch ( AzureServiceException ex )
        {
            _logger.LogError( ex, "{Message}", ex.Message );

            return 1;
        }


        /*
         *
         */
        var groups = inventory.ResourceGroupsOf( this.Environment! );

        if ( groups.Count == 0 )
        {
            var known = inventory.Environments();

            _logger.LogError( "No resource group is tagged as environment {Environment}: {Known}",
                this.Environment, known.Count == 0 ? "none are tagged at all" : string.Join( ", ", known ) );

            return 1;
        }

        _logger.LogInformation( "environment {Environment} spans {Count} resource group(s)", this.Environment, groups.Count );

        var rows = _layout.Build( groups );


        /*
         *
         */
        if ( this.OutputFile != null || this.AsJson == true )
        {
            var json = JsonSerializer.Serialize( rows, JSO.Default );

            if ( this.OutputFile != null )
            {
                await File.WriteAllTextAsync( this.OutputFile, json, cancellationToken );
            }
            else
            {
                AnsiConsole.Write( new JsonText( json ) );
                AnsiConsole.WriteLine();
            }
        }
        else
        {
            var table = new Table();
            table.Border = TableBorder.SimpleHeavy;
            table.AddColumn( "Vnet" );
            table.AddColumn( "Snet" );
            table.AddColumn( "Ip" );
            table.AddColumn( "Resource" );
            //table.AddColumn( "Endpoint" );
            //table.AddColumn( "Nic" );
            table.AddColumn( "Resource Group" );

            foreach ( var r in rows )
            {
                table.AddRow(
                    Cell( r.Vnet ),
                    Cell( r.Snet ),
                    Cell( r.Ip ),
                    Cell( r.Resource ),
                    Cell( r.ResourceGroup )
                    //Cell( r.PrivateEndpoint ),
                    //Cell( r.NetworkInterface ),
                    );
            }

            AnsiConsole.Write( table );
        }

        return 0;
    }


    /// <summary />
    /// <remarks>
    /// Resource names are escaped rather than written straight into the cell:
    /// a name is free text, and a bracket in one would otherwise be read as
    /// console markup.
    /// </remarks>
    private static Markup Cell( string? value )
    {
        if ( string.IsNullOrEmpty( value ) == true )
            return new Markup( "[grey]-[/]" );

        return new Markup( Markup.Escape( value ) );
    }
}
