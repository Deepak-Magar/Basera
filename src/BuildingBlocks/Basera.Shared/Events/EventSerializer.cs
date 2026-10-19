using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Basera.Shared.Domain;

namespace Basera.Shared.Events;

/// <summary>How reliable events are stored in the outbox: JSON payload plus "Full.Type.Name, Assembly".</summary>
public static class EventSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new MoneyJsonConverter() },
    };

    public static bool IsReliable(IDomainEvent domainEvent) =>
        domainEvent.GetType().GetCustomAttribute<ReliableAttribute>() is not null;

    /// <summary>Type name without version, so a rebuilt assembly still reads old rows.</summary>
    public static string TypeName(Type type) => $"{type.FullName}, {type.Assembly.GetName().Name}";

    public static string Serialize(IDomainEvent domainEvent) =>
        JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), Options);

    public static IDomainEvent Deserialize(string typeName, string payload)
    {
        // Only types that are domain events: the name comes from the database, so it is not trusted blindly.
        var type = Type.GetType(typeName, throwOnError: false);
        if (type is null || !typeof(IDomainEvent).IsAssignableFrom(type))
        {
            throw new InvalidOperationException($"'{typeName}' is not a known domain event type.");
        }

        return (IDomainEvent)(JsonSerializer.Deserialize(payload, type, Options)
            ?? throw new InvalidOperationException($"The payload of '{typeName}' is empty."));
    }

    /// <summary>Money as a plain number of paisa, the same as in the database.</summary>
    private sealed class MoneyJsonConverter : JsonConverter<Money>
    {
        public override Money Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            Money.FromPaisa(reader.GetInt64());

        public override void Write(Utf8JsonWriter writer, Money value, JsonSerializerOptions options) =>
            writer.WriteNumberValue(value.Paisa);
    }
}
