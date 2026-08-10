namespace Lefty.Navy.Model;

/// <summary />
public class AzResourceGroup
{
    /// <summary />
    public required string Id { get; set; }

    /// <summary />
    public required string Name { get; set; }

    /// <summary />
    public required Dictionary<string, string> Tags { get; set; }


    /// <summary />
    public List<AzResource>? Resources { get; set; }
}