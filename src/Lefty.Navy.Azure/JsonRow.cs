using System.Text.Json;

namespace Lefty.Navy.Azure;

/// <summary />
/// <remarks>
/// Resource Graph omits absent fields altogether rather than returning them as
/// null, so every accessor must tolerate a missing property, an explicit null
/// and a value of an unexpected kind. Navigation returns an undefined element
/// rather than null, so that paths such as
/// <c>row.Obj( "properties" ).Obj( "sku" ).Str( "name" )</c> chain without
/// null checks at every step.
/// </remarks>
internal static class JsonRow
{
    /// <summary />
    public static JsonElement Obj( this JsonElement element, string name )
    {
        if ( element.ValueKind != JsonValueKind.Object )
            return default;

        if ( element.TryGetProperty( name, out var value ) == false )
            return default;

        if ( value.ValueKind == JsonValueKind.Null )
            return default;

        return value;
    }


    /// <summary />
    public static string? Str( this JsonElement element, string name )
    {
        var value = element.Obj( name );

        if ( value.ValueKind != JsonValueKind.String )
            return null;

        return value.GetString();
    }


    /// <summary />
    public static bool Bool( this JsonElement element, string name )
    {
        return element.Obj( name ).ValueKind == JsonValueKind.True;
    }


    /// <summary />
    public static int Int( this JsonElement element, string name )
    {
        var value = element.Obj( name );

        if ( value.ValueKind != JsonValueKind.Number )
            return 0;

        return value.TryGetInt32( out var number ) == true ? number : 0;
    }


    /// <summary />
    public static long Long( this JsonElement element, string name )
    {
        var value = element.Obj( name );

        if ( value.ValueKind != JsonValueKind.Number )
            return 0;

        return value.TryGetInt64( out var number ) == true ? number : 0;
    }


    /// <summary />
    public static DateTimeOffset? Moment( this JsonElement element, string name )
    {
        var value = element.Obj( name );

        if ( value.ValueKind != JsonValueKind.String )
            return null;

        return value.TryGetDateTimeOffset( out var moment ) == true ? moment : null;
    }


    /// <summary />
    public static List<string> StrList( this JsonElement element, string name )
    {
        var list = new List<string>();
        var value = element.Obj( name );

        if ( value.ValueKind != JsonValueKind.Array )
            return list;

        foreach ( var item in value.EnumerateArray() )
        {
            if ( item.ValueKind == JsonValueKind.String )
                list.Add( item.GetString()! );
        }

        return list;
    }


    /// <summary />
    public static List<JsonElement> Items( this JsonElement element, string name )
    {
        var value = element.Obj( name );

        if ( value.ValueKind != JsonValueKind.Array )
            return [];

        return [ .. value.EnumerateArray() ];
    }


    /// <summary />
    /// <remarks>
    /// The string members of an object whose keys are not known ahead of time.
    /// </remarks>
    public static IEnumerable<KeyValuePair<string, string>> Pairs( this JsonElement element )
    {
        if ( element.ValueKind != JsonValueKind.Object )
            yield break;

        foreach ( var property in element.EnumerateObject() )
        {
            if ( property.Value.ValueKind == JsonValueKind.String )
                yield return new KeyValuePair<string, string>( property.Name, property.Value.GetString()! );
        }
    }


    /// <summary />
    public static Dictionary<string, string> TagMap( this JsonElement element, string name )
    {
        var tags = new Dictionary<string, string>();
        var value = element.Obj( name );

        if ( value.ValueKind != JsonValueKind.Object )
            return tags;

        foreach ( var property in value.EnumerateObject() )
        {
            if ( property.Value.ValueKind == JsonValueKind.String )
                tags[ property.Name ] = property.Value.GetString()!;
        }

        return tags;
    }
}
