using Azure.Core;
using Azure.Identity;
using Lefty.Navy.Azure;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;
using System.Reflection;

namespace Lefty.Navy;

/// <summary />
[Command( "navy", Description = "Azure Inventory" )]
[Subcommand( typeof( BuildCommand ) )]
[VersionOptionFromMember( MemberName = nameof( GetVersion ) )]
public class Program
{
    /// <summary />
    public static int Main( string[] args )
    {
        /*
         * 
         */
        var isVerbose = args.Where( x => x == "-v" || x == "--verbose" ).Any();

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override( "Lefty.Navy", isVerbose == true ? LogEventLevel.Debug : LogEventLevel.Information )
            .WriteTo.Console( standardErrorFromLevel: LogEventLevel.Verbose )
            .CreateLogger();

        var logger = Log.ForContext<Program>();


        /*
         * 
         */
        var svc = new ServiceCollection();

        svc.AddLogging( loggingBuilder =>
            loggingBuilder.AddSerilog( dispose: true ) );

        svc.AddOptions();

        svc.AddSingleton<TokenCredential>( new DefaultAzureCredential() );
        svc.AddTransient<AzureService>();

        var sp = svc.BuildServiceProvider();


        /*
         * 
         */
        var app = new CommandLineApplication<Program>();

        try
        {
            app.Conventions
                .UseDefaultConventions()
                .UseConstructorInjection( sp );
        }
        catch ( TargetInvocationException ex ) when ( ex.InnerException is OptionsValidationException iex )
        {
            foreach ( var f in iex.Failures )
                logger.Fatal( f );

            return 2;
        }
        catch ( Exception ex )
        {
            logger.Fatal( ex, "Unhandled exception during setup" );

            return 2;
        }


        /*
         * 
         */
        try
        {
            return app.Execute( args );
        }
        catch ( CommandParsingException ex )
        {
            Console.WriteLine( "err: " + ex.Message );

            return 2;
        }
        catch ( Exception ex )
        {
            Console.WriteLine( "ftl: unhandled exception during execution" );
            Console.WriteLine( ex.ToString() );

            return 2;
        }
    }


    /// <summary />
    private static string GetVersion()
    {
        return typeof( Program ).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
    }


    /// <summary />
    [Option( "-v|--verbose", CommandOptionType.NoValue, Description = "Enabled verbose output" )]
    public bool IsVerbose { get; set; }


    /// <summary />
    public int OnExecute( CommandLineApplication app )
    {
        app.ShowHelp();
        return 1;
    }
}
