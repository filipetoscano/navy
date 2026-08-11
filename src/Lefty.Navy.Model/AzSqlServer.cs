namespace Lefty.Navy.Model;

/// <summary />
public class AzSqlServer : AzResource
{
    /// <summary />
    public string? Version { get; set; }

    /// <summary />
    public string? State { get; set; }

    /// <summary />
    public string? FullyQualifiedDomainName { get; set; }


    /// <summary />
    /// <remarks>
    /// The SQL authentication administrator. Null once the server is set to
    /// Entra-only authentication.
    /// </remarks>
    public string? AdministratorLogin { get; set; }

    /// <summary />
    public string? EntraAdministratorLogin { get; set; }

    /// <summary />
    /// <remarks>
    /// User, Group or Application.
    /// </remarks>
    public string? EntraAdministratorPrincipalType { get; set; }

    /// <summary />
    public string? EntraAdministratorObjectId { get; set; }

    /// <summary />
    /// <remarks>
    /// True when SQL authentication is refused outright and only Entra
    /// identities may connect.
    /// </remarks>
    public bool EntraOnlyAuthentication { get; set; }


    /// <summary />
    public string? PublicNetworkAccess { get; set; }

    /// <summary />
    public string? MinimalTlsVersion { get; set; }

    /// <summary />
    /// <remarks>
    /// Enabled when the server may only reach outbound destinations which have
    /// been explicitly allowed.
    /// </remarks>
    public string? RestrictOutboundNetworkAccess { get; set; }

    /// <summary />
    public string? ExternalGovernanceStatus { get; set; }

    /// <summary />
    public string? PrimaryUserAssignedIdentityId { get; set; }

    /// <summary />
    public List<string> PrivateEndpointIds { get; set; } = [];


    /// <summary />
    /// <remarks>
    /// Databases are returned as resources in their own right rather than as
    /// part of the server, and are attached here once everything has been read.
    /// </remarks>
    public List<AzSqlDatabase> Databases { get; set; } = [];

    /// <summary />
    public List<AzPrivateEndpoint> PrivateEndpoints { get; set; } = [];

    /// <summary />
    public AzManagedIdentity? PrimaryUserAssignedIdentity { get; set; }
}
