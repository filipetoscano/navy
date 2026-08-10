namespace Lefty.Navy.Model;

/// <summary />
public class AzSubscription
{
    /// <summary />
    public required Guid Id { get; set; }

    /// <summary />
    public required string Name { get; set; }


    /// <summary />
    public List<AzResourceGroup>? ResourceGroups { get; set; }
}