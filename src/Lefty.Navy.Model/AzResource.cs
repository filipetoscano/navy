using System.Text.Json.Serialization;

namespace Lefty.Navy.Model;

/// <summary />
/// <remarks>
/// If a specialized class is not available, the placeholder base class
/// will be used to represent the resource.
/// </remarks>
[JsonDerivedType( typeof( AzActionGroup ), nameof( AzActionGroup ) )]
[JsonDerivedType( typeof( AzActivityLogAlertRule ), nameof( AzActivityLogAlertRule ) )]
[JsonDerivedType( typeof( AzApiManagement ), nameof( AzApiManagement ) )]
[JsonDerivedType( typeof( AzAppService ), nameof( AzAppService ) )]
[JsonDerivedType( typeof( AzApplicationInsights ), nameof( AzApplicationInsights ) )]
[JsonDerivedType( typeof( AzCacheForRedis ), nameof( AzCacheForRedis ) )]
[JsonDerivedType( typeof( AzDatabricksConnector ), nameof( AzDatabricksConnector ) )]
[JsonDerivedType( typeof( AzDatabricksWorkspace ), nameof( AzDatabricksWorkspace ) )]
[JsonDerivedType( typeof( AzDiskEncryptionSet ), nameof( AzDiskEncryptionSet ) )]
[JsonDerivedType( typeof( AzEventHubNamespace ), nameof( AzEventHubNamespace ) )]
[JsonDerivedType( typeof( AzFunctionApp ), nameof( AzFunctionApp ) )]
[JsonDerivedType( typeof( AzKeyVault ), nameof( AzKeyVault ) )]
[JsonDerivedType( typeof( AzKubernetesService ), nameof( AzKubernetesService ) )]
[JsonDerivedType( typeof( AzLoadBalancer ), nameof( AzLoadBalancer ) )]
[JsonDerivedType( typeof( AzManagedIdentity ), nameof( AzManagedIdentity ) )]
[JsonDerivedType( typeof( AzMetricAlertRule ), nameof( AzMetricAlertRule ) )]
[JsonDerivedType( typeof( AzNetAppAccount ), nameof( AzNetAppAccount ) )]
[JsonDerivedType( typeof( AzNetAppCapacityPool ), nameof( AzNetAppCapacityPool ) )]
[JsonDerivedType( typeof( AzNetAppVolume ), nameof( AzNetAppVolume ) )]
[JsonDerivedType( typeof( AzNetworkInterface ), nameof( AzNetworkInterface ) )]
[JsonDerivedType( typeof( AzNetworkSecurityGroup ), nameof( AzNetworkSecurityGroup ) )]
[JsonDerivedType( typeof( AzPrivateEndpoint ), nameof( AzPrivateEndpoint ) )]
[JsonDerivedType( typeof( AzRouteTable ), nameof( AzRouteTable ) )]
[JsonDerivedType( typeof( AzSmartDetectorAlertRule ), nameof( AzSmartDetectorAlertRule ) )]
[JsonDerivedType( typeof( AzSqlDatabase ), nameof( AzSqlDatabase ) )]
[JsonDerivedType( typeof( AzSqlServer ), nameof( AzSqlServer ) )]
[JsonDerivedType( typeof( AzStorageAccount ), nameof( AzStorageAccount ) )]
[JsonDerivedType( typeof( AzVirtualMachine ), nameof( AzVirtualMachine ) )]
[JsonDerivedType( typeof( AzVirtualMachineScaleSet ), nameof( AzVirtualMachineScaleSet ) )]
[JsonDerivedType( typeof( AzVirtualNetwork ), nameof( AzVirtualNetwork ) )]
public class AzResource
{
    /// <summary />
    public required string Id { get; set; }

    /// <summary />
    public required string Name { get; set; }

    /// <summary />
    public required string Type { get; set; }

    /// <summary />
    public required string Location { get; set; }

    /// <summary />
    public Dictionary<string, string> Tags { get; set; } = [];
}


/// <summary />
public abstract class AzChildResource
{
    /// <summary />
    public required string Id { get; set; }

    /// <summary />
    public required string Name { get; set; }

    /// <summary />
    public required string Type { get; set; }
}