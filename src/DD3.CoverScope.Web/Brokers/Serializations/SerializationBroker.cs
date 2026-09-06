using System.Text.Json;
using System.Text.Json.Serialization;

namespace DD3.CoverScope.Brokers.Serializations;

public interface ISerializationBroker
{
    string Serialize<T>(T value);
    T? Deserialize<T>(string value);
}

public class SerializationBroker : ISerializationBroker
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);
    public T? Deserialize<T>(string value) => JsonSerializer.Deserialize<T>(value, Options);
}
