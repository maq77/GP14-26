using System.Text.Json;

namespace SSSP.Api.Realtime;

public static class JsonPayload
{
    // Safe helper: converts any T into JsonElement using System.Text.Json.
    public static JsonElement From<T>(T value, JsonSerializerOptions? options = null)
    {
        options ??= new JsonSerializerOptions
        {
            PropertyNamingPolicy = null,
            WriteIndented = false
        };

        // serialize => parse => clone root
        var json = JsonSerializer.Serialize(value, options);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }
}
