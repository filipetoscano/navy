using System.Text.Json;

namespace Lefty.Navy;

/// <summary />
public class JSO
{
    /// <summary />
    public static readonly JsonSerializerOptions Default = new()
    {
        WriteIndented = true,
    };
}
