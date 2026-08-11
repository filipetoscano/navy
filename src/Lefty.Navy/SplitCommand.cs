using Azure.ResourceManager.Network.Models;
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
         * 
         */
        AzSubscription sub;

        using ( var ins = File.OpenRead( this.InputFile! ) )
        {
            var obj = await JsonSerializer.DeserializeAsync<AzSubscription>( ins, default( JsonSerializerOptions? ), cancellationToken );

            sub = obj!;
        }

        _logger.LogInformation( "loaded {Subscription} with {Count} resource groups", sub.Name, sub.ResourceGroups?.Count() );

        if ( sub.ResourceGroups == null )
            return 0;


        /*
         * 
         */
        var env = new ZzEnvironment()
        {
            Subscription = sub.Name,
            Environment = this.Environment!.ToLowerInvariant(),
            ResourceGroups = new List<ZzResourceGroup>(),
        };


        /*
         * 
         */
        var p1 = $"-{env.Environment}-";
        var p2 = $"{env.Environment}";

        foreach ( var rg in sub.ResourceGroups
                               .Where( x => x.Tags.ContainsKey( "Environment" ) == true )
                               .Where( x => x.Tags[ "Environment" ].ToLowerInvariant() == this.Environment )
                               .OrderBy( x => x.Name ) )
        {
            _logger.LogInformation( "{ResourceGroup} - {Count} resources", rg.Name, rg.Resources?.Count );

            var kk = new ZzResourceGroup()
            {
                Name = rg.Name.Replace( p1, "-env-" ),
                Resources = new List<ZzResource>(),
            };

            env.ResourceGroups.Add( kk );


            /*
             * 
             */
            if ( rg.Resources == null )
                continue;

            foreach ( var rr in rg.Resources.OrderBy( x => x.Type ).ThenBy( x => x.Name ) )
            {
                kk.Resources.Add( new ZzResource()
                {
                    Name = rr.Name.Replace( p1, "-env-" ).Replace( p2, "env" ),
                    Type = rr.Type,
                } );
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
        public required List<ZzResource> Resources { get; set; }
    }


    /// <summary />
    public class ZzResource
    {
        /// <summary />
        public required string Name { get; set; }

        /// <summary />
        public required string Type { get; set; }
    }
}
