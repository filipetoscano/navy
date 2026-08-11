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
    public static async Task<int> Main( string[] args )
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

        svc.AddSingleton<TokenCredential>( CredentialGet() );
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
         * An interrupt asks the command to stop rather than killing the process
         * where it stands, so that a run which is part way through a
         * subscription unwinds instead of leaving a half-written file. A second
         * interrupt is left to the runtime, which does kill it.
         */
        using var cts = new CancellationTokenSource();

        Console.CancelKeyPress += ( _, e ) =>
        {
            if ( cts.IsCancellationRequested == true )
                return;

            e.Cancel = true;
            cts.Cancel();
        };


        /*
         *
         */
        try
        {
            return await app.ExecuteAsync( args, cts.Token );
        }
        catch ( CommandParsingException ex )
        {
            Console.WriteLine( "err: " + ex.Message );

            return 2;
        }
        catch ( OperationCanceledException ) when ( cts.IsCancellationRequested == true )
        {
            /*
             * Reported through the logger rather than to the console, because
             * stdout carries the inventory and a message on it would corrupt
             * whatever the output is piped into. 130 is what a shell expects
             * from a process which stopped on an interrupt.
             */
            logger.Warning( "interrupted, the inventory was not completed" );

            return 130;
        }
        catch ( Exception ex )
        {
            Console.WriteLine( "ftl: unhandled exception during execution" );
            Console.WriteLine( ex.ToString() );

            return 2;
        }
    }


    /// <summary />
    /// <remarks>
    /// Deliberately not a plain DefaultAzureCredential. Its managed identity
    /// source probes the instance metadata service at a link-local address
    /// which, off an Azure host, is not refused but silently dropped: the probe
    /// spends over two minutes in retries and then reports failure by throwing
    /// AuthenticationFailedException rather than CredentialUnavailableException.
    /// That is fatal to the chain, so no later source is ever consulted, and a
    /// developer signed in through the Azure CLI is turned away.
    /// <para>
    /// Running the probe last rather than in the middle keeps that cost off
    /// every machine which has some other way to authenticate, while leaving
    /// managed identity available to a service which genuinely has nothing
    /// else.
    /// </para>
    /// </remarks>
    private static TokenCredential CredentialGet()
    {
        var options = new DefaultAzureCredentialOptions
        {
            ExcludeManagedIdentityCredential = true,
        };

        return new ChainedTokenCredential(
            new DefaultAzureCredential( options ),
            new ManagedIdentityCredential( new ManagedIdentityCredentialOptions() ) );
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
