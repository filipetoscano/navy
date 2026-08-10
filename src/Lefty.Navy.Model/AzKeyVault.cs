namespace Lefty.Navy.Model;

/// <summary />
public class AzKeyVault : AzResource
{
    /// <summary />
    public required string Sku { get; set; }

    /// <summary />
    public string? PrivateEndpointId { get; set; }

    /// <summary />
    public bool EnabledForDiskEncryption { get; set; }

    /// <summary />
    public bool EnableSoftDelete { get; set; }

    /// <summary />
    public int SoftDeleteRetentionInDays { get; set; }

    /// <summary />
    public bool EnableRbacAuthorization { get; set; }

    /// <summary />
    public bool EnablePurgeProtection { get; set; }


    /// <summary />
    public AzPrivateEndpoint? PrivateEndpoint { get; set; }
}
