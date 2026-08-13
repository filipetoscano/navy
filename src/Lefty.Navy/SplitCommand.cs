using Lefty.Navy.Azure;
using Lefty.Navy.Model;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Lefty.Navy;

/// <summary />
[Command( Name = "split", Description = "Splits an inventory into an environment" )]
public class SplitCommand
{
    private readonly ILogger<SplitCommand> _logger;


    /// <summary />
    public SplitCommand( ILogger<SplitCommand> logger )
    {
        _logger = logger;
    }


    /// <summary />
    [Argument( 0, Description = "Input file" )]
    [Required]
    [FileExists]
    public string? InputFile { get; set; }

    /// <summary />
    [Argument( 1, Description = "Environment" )]
    [Required]
    public string? Environment { get; set; }


    /// <summary />
    [Option( "-o|--output-file", CommandOptionType.SingleValue, Description = "Output file" )]
    public string? OutputFile { get; set; }


    /// <summary />
    public async Task<int> OnExecuteAsync( CancellationToken cancellationToken )
    {
        /*
         * The graph is stitched on the way in: what a resource points at is
         * written into the file inline, so reading it back leaves a copy at
         * each reference rather than the object the inventory holds.
         */
        Inventory inventory;

        try
        {
            inventory = await new InventoryLoader( _logger ).LoadAsync( this.InputFile!, cancellationToken );
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

        var env = new ZzEnvironment()
        {
            Subscription = inventory.Subscription.Name,
            Environment = this.Environment!.ToLowerInvariant(),
            ResourceGroups = new List<ZzResourceGroup>(),
        };


        /*
         *
         */
        var p1 = $"-{env.Environment}-";
        var p2 = $"{env.Environment}";

        foreach ( var rg in groups )
        {
            _logger.LogInformation( "{ResourceGroup} - {Count} resources", rg.Name, rg.Resources?.Count );

            var kk = new ZzResourceGroup()
            {
                Name = rg.Name.Replace( p1, "-env-" ),
                Resources = new List<dynamic>(),
            };

            env.ResourceGroups.Add( kk );


            /*
             * 
             */
            if ( rg.Resources == null )
                continue;

            foreach ( var rr in rg.Resources.OrderBy( x => x.Type ).ThenBy( x => x.Name ) )
            {
                dynamic obj;

                var name1 = rr.Name.Replace( p1, "-env-" ).Replace( p2, "env" );

                if ( rr is AzKeyVault kv )
                {
                    var peIp = ToIp( kv.PrivateEndpoint );

                    obj = new
                    {
                        Name = name1,
                        Type = "KeyVault",
                        kv.Sku,
                        HasDiskEncryption = kv.EnabledForDiskEncryption,
                        PrivateEndpointIp = peIp,
                    };
                }
                else
                {
                    obj = new
                    {
                        Name = name1,
                        Type = rr.Type,
                    };
                }

                kk.Resources.Add( obj );
            }
        }


        /*
         * 
         */
        var json = JsonSerializer.Serialize( env, JSO.Default );

        if ( this.OutputFile != null )
        {
            await File.WriteAllTextAsync( this.OutputFile, json, cancellationToken );
        }
        else
        {
            Console.WriteLine( json );
        }

        return 0;
    }


    /// <summary />
    private string? ToIp( AzPrivateEndpoint? privateEndpoint )
    {
        if ( privateEndpoint == null )
            return null;

        var list = privateEndpoint.NetworkInterfaces
            .SelectMany( x => x.IPConfigurations )
            .Select( x => x.PrivateIPAddress )
            .ToList();

        if ( list.Count == 0 )
            return null;

        return string.Join( ",", list );
    }


    /// <summary />
    public class ZzEnvironment
    {
        /// <summary />
        public required string Subscription { get; set; }

        /// <summary />
        public required string Environment { get; set; }

        /// <summary />
        public required List<ZzResourceGroup> ResourceGroups { get; set; }
    }


    /// <summary />
    public class ZzResourceGroup
    {
        /// <summary />
        public required string Name { get; set; }

        /// <summary />
        public required List<dynamic> Resources { get; set; }
    }
}
