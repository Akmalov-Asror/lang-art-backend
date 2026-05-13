using System.Text.Json;
using System.Text.Json.Serialization;

namespace LangArt.Api.Common.Serialization;

public static class JsonConfig
{
    public static void Configure(JsonSerializerOptions o)
    {
        o.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        o.DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower;
        o.PropertyNameCaseInsensitive = true;
        o.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        o.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
        o.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
    }
}
