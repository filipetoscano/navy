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
    private readonly InventoryLoader _loader;
    private readonly ILogger<SplitCommand> _logger;


    /// <summary />
    public SplitCommand( InventoryLoader loader, ILogger<SplitCommand> logger )
    {
        _loader = loader;
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

        foreach ( var rg in groups.OrderBy( x => x.Name ) )
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

                var name1 = rr.Name.Replace( p1, "-env-" );
                var name2 = rr.Name.Replace( p2, "env" );

                if ( rr is AzNetworkInterface )
                    continue;

                if ( rr is AzPrivateEndpoint )
                    continue;

                if ( rr is AzSqlDatabase )
                    continue;

                if ( rr is AzRouteTable )
                    continue;

                if ( rr is AzNetworkSecurityGroup )
                    continue;


                /*
                 * Key vault
                 */
                if ( rr is AzKeyVault kv )
                {
                    var peIp = ToIp( kv.PrivateEndpoint );

                    obj = new
                    {
                        Name = name1,
                        Type = "KeyVault",
                        kv.Sku,
                        kv.EnabledForDiskEncryption,
                        kv.EnableSoftDelete,
                        kv.SoftDeleteRetentionInDays,
                        kv.EnableRbacAuthorization,
                        kv.EnablePurgeProtection,
                        PrivateEndpointIp = peIp,
                    };
                }


                /*
                 * 
                 */
                else if ( rr is AzKubernetesService aks )
                {
                    obj = new
                    {
                        Name = name1,
                        Type = "Aks",
                        aks.SkuTier,
                        aks.KubernetesVersion,
                        SupportPlan = aks.SupportPlan == "AKSLongTermSupport" ? "LongTerm" : aks.SupportPlan,
                        aks.EnableRbac,
                        aks.DisableLocalAccounts,
                        aks.EnableAzureRbac,
                        NodePools = aks.NodePools.OrderBy( x => x.Name ).Select( x => new
                        {
                            x.Name,
                            x.Mode,
                            x.Count,
                            x.VmSize,
                            x.EnableAutoScaling,
                            x.MinCount,
                            x.MaxCount,
                            x.MaxPods,
                            x.EnableEncryptionAtHost,
                            Subnet = x.Subnet != null ? new
                            {
                                Name = x.Subnet.Name.Replace( p1, "-env-" ),
                                x.Subnet.AddressPrefix,
                            } : null,
                        } ),
                    };
                }


                /*
                 * Storage account, blob container
                 */
                else if ( rr is AzStorageAccount sa )
                {
                    var pe = sa.PrivateEndpoints.Select( x => ToIp( x ) );
                    var blobs = sa.BlobContainers.Select( x => new
                    {
                        x.Name,
                        x.PublicAccess,
                    } );

                    obj = new
                    {
                        Name = name2,
                        Type = "StorageAccount",
                        sa.Kind,
                        sa.Sku,
                        sa.SkuTier,
                        sa.AccessTier,
                        sa.AllowSharedKeyAccess,
                        sa.AllowBlobPublicAccess,
                        Cmk = ToCmk( sa.EncryptionKeyVaultUri?.Replace( p1, "-env-" ), sa.EncryptionKeyName ),
                        BlobContainers = blobs,
                        HasPublicNetworkAccess = sa.PublicNetworkAccess != null,
                        PrivateEndpointsIp = pe,
                    };
                }


                /*
                 * MSSQL server, and databases
                 */
                else if ( rr is AzSqlServer mssql )
                {
                    var pe = mssql.PrivateEndpoints.Select( x => ToIp( x ) );
                    var db = mssql.Databases.Select( x => new
                    {
                        x.Name,
                        x.Sku,
                        x.SkuTier,
                        x.SkuCapacity,
                        x.Collation,
                        x.IsInfraEncryptionEnabled,
                    } );

                    obj = new
                    {
                        Name = name1,
                        Type = "MssqlServer",
                        mssql.Version,
                        mssql.EntraOnlyAuthentication,
                        mssql.EntraAdministratorLogin,
                        PrivateEndpointsIp = pe,
                    };
                }

                /*
                 * Vnet, Snet, Route tables
                 */
                else if ( rr is AzVirtualNetwork vnet )
                {
                    obj = new
                    {
                        Name = name1,
                        Type = "VirtualNetwork",
                        vnet.AddressPrefixes,
                        vnet.DnsServers,
                        Subnets = vnet.Subnets.OrderBy( x => x.Name ).Select( x => new
                        {
                            Name = x.Name.Replace( p1, "-env-" ),
                            x.AddressPrefix,
                            RouteTable = x.RouteTable?.Name.Replace( p1, "-env-" ),
                            RouteTableRoutes = x.RouteTable?.Routes.OrderBy( y => y.AddressPrefix ).Select( y => new
                            {
                                y.AddressPrefix,
                                y.NextHopType,
                                y.NextHopIpAddress,
                            } ),
                        } ),
                    };
                }
                else
                {
                    obj = new
                    {
                        Name = name1,
                        rr.Type,
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
    private string? ToCmk( string? encryptionKeySource, string? encryptionKeyName )
    {
        if ( encryptionKeySource == null )
            return null;

        if ( encryptionKeyName == null )
            return null;

        return encryptionKeySource + "/" + encryptionKeyName;
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
