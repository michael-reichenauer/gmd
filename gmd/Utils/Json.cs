using System.Text.Json;

namespace gmd.Utils;

class Json
{
    internal static string Serialize<T>(T value)
    {
        return JsonSerializer.Serialize(value);
    }

    internal static string SerializePretty<T>(T value)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        return JsonSerializer.Serialize(value, options);
    }

    internal static R<T> Deserialize<T>(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json)!;
        }
        catch (Exception e)
        {
            return R.Error(e);
        }
    }
}
