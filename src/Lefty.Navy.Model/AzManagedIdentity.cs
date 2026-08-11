namespace Lefty.Navy.Model;

/// <summary />
/// <remarks>
/// A user-assigned managed identity. The system-assigned identity of a resource
/// is not a resource of its own, and so is never returned here.
/// </remarks>
public class AzManagedIdentity : AzResource
{
    /// <summary>
    /// Object identifier of the service principal which backs the identity.
    /// </summary>
    /// <remarks>
    /// The value which role assignments and key vault access policies name, and
    /// the only one of the three which identifies the identity to Azure RBAC.
    /// </remarks>
    public string? PrincipalId { get; set; }

    /// <summary>
    /// Application identifier of the identity.
    /// </summary>
    /// <remarks>
    /// What a workload presents when it asks for a token as this identity.
    /// </remarks>
    public string? ClientId { get; set; }

    /// <summary />
    public string? TenantId { get; set; }
}
