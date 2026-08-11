namespace Lefty.Navy.Model;

/// <summary />
public class AzApiManagement : AzResource
{
    /// <summary />
    public required string Sku { get; set; }

    /// <summary />
    public int SkuCapacity { get; set; }

    /// <summary />
    public string? PublisherName { get; set; }

    /// <summary />
    public string? PublisherEmail { get; set; }

    /// <summary />
    public string? PlatformVersion { get; set; }


    /// <summary />
    public string? GatewayUrl { get; set; }

    /// <summary />
    public string? DeveloperPortalUrl { get; set; }

    /// <summary />
    public string? ManagementApiUrl { get; set; }

    /// <summary />
    public string? ScmUrl { get; set; }


    /// <summary />
    /// <remarks>
    /// None, External or Internal.
    /// </remarks>
    public string? VirtualNetworkType { get; set; }

    /// <summary />
    public string? PublicNetworkAccess { get; set; }

    /// <summary />
    /// <remarks>
    /// Null unless the service is injected into a virtual network.
    /// </remarks>
    public string? SubnetId { get; set; }

    /// <summary />
    public List<string> PublicIPAddresses { get; set; } = [];

    /// <summary />
    /// <remarks>
    /// Only populated for a service injected into a virtual network.
    /// </remarks>
    public List<string> PrivateIPAddresses { get; set; } = [];


    /// <summary />
    public List<AzApiManagementHostname> HostnameConfigurations { get; set; } = [];


    /// <summary />
    public AzSubnet? Subnet { get; set; }
}


/// <summary />
/// <remarks>
/// Presented in the portal as a custom domain. Every service also carries one
/// configuration for its built-in azure-api.net hostname, which is told apart
/// from a custom domain by its <see cref="CertificateSource" /> of BuiltIn.
/// <para>
/// Hostname configurations have no identifier of their own, so
/// <see cref="AzChildResource.Id" /> is synthesized from the service and the
/// hostname.
/// </para>
/// </remarks>
public class AzApiManagementHostname : AzChildResource
{
    /// <summary />
    /// <remarks>
    /// Which endpoint the hostname is bound to: Proxy, Management, Portal,
    /// DeveloperPortal or Scm. Distinct from <see cref="AzChildResource.Type" />,
    /// which carries the resource type.
    /// </remarks>
    public string? HostnameType { get; set; }

    /// <summary />
    /// <remarks>
    /// BuiltIn, Custom, KeyVault or Managed.
    /// </remarks>
    public string? CertificateSource { get; set; }

    /// <summary />
    public string? CertificateStatus { get; set; }

    /// <summary />
    public bool DefaultSslBinding { get; set; }

    /// <summary />
    public bool NegotiateClientCertificate { get; set; }

    /// <summary />
    /// <remarks>
    /// Secret identifier of the certificate within a key vault, rather than the
    /// resource identifier of the vault itself: it cannot be resolved to an
    /// <see cref="AzKeyVault" />.
    /// </remarks>
    public string? KeyVaultId { get; set; }
}
