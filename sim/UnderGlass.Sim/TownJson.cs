using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace UnderGlass.Sim;

/// <summary>
/// A town as a JSON file (town spec 4.5; design 0d's "the town as JSON"): the runner's --dump-town
/// writes one, and --town file:path reads it back, so a town can be edited by hand. Everything in
/// <see cref="TownData"/> is written: the cast's cards, the places' rows, the doors, the hubs, the act
/// kinds and every option. Tuples are written as arrays ([from, to] for a patrol hour, [a, b, value]
/// for a familiarity seed, [from, to, regard] for a starting tension); enums by name; numbers that are
/// not finite as "Infinity". Values the code works out (a place's width, a villager's home) and the
/// one setting that is code (FeelingOptions.CloseCall) are left out. A town read back runs exactly as
/// the one written. Reading is strict, since a hand edit is easy to get wrong: a field the town
/// doesn't have (a misspelling, or the wrong case) fails, so does a field left out (write the town
/// again with --dump-town when the town gains a setting), and so does a part set to null (only the
/// economy may be null, for a town with no money).
/// </summary>
public static class TownJson
{
    public static string Write(TownData town) => JsonSerializer.Serialize(town, Options);

    public static TownData Read(string json)
    {
        TownData town = JsonSerializer.Deserialize<TownData>(json, Options) ?? throw new JsonException("the file holds no town");
        var parts = new (string Name, object? Value)[]
        {
            ("Cast", town.Cast), ("Places", town.Places), ("Links", town.Links), ("Gatherings", town.Gatherings), ("Acts", town.Acts),
            ("Feelings", town.Feelings), ("Authority", town.Authority), ("Perception", town.Perception), ("Gossip", town.Gossip),
            ("Body", town.Body), ("Habits", town.Habits), ("Money", town.Money), ("Familiarity", town.Familiarity),
        };
        foreach (var (name, value) in parts)
            if (value is null)
                throw new JsonException($"the file's {name} is null; only the Economy may be");
        return town;
    }

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(), new TupleConverterFactory(), new TensionsConverter() },
        TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { LeaveOutWorkedOut } },
    };

    /// <summary>Leaves out delegates, and properties the code works out (no setter and no constructor
    /// parameter of the same name), so the file holds only what a town is made of; every property
    /// kept must be in the file.</summary>
    private static void LeaveOutWorkedOut(JsonTypeInfo info)
    {
        if (info.Kind != JsonTypeInfoKind.Object || Nullable.GetUnderlyingType(info.Type) is not null)
            return;
        var parameters = info.Type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .SelectMany(c => c.GetParameters()).Select(p => p.Name ?? "").ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (int i = info.Properties.Count - 1; i >= 0; i--)
        {
            JsonPropertyInfo p = info.Properties[i];
            bool code = typeof(Delegate).IsAssignableFrom(p.PropertyType);
            bool workedOut = p.Set is null && !parameters.Contains(p.Name);
            if (code || workedOut)
                info.Properties.RemoveAt(i);
            else if (p.Set is not null)
                p.IsRequired = true;
        }
    }

    /// <summary>Value tuples as arrays of their items.</summary>
    private sealed class TupleConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type t) => t.IsGenericType && t.FullName!.StartsWith("System.ValueTuple`", StringComparison.Ordinal);

        public override JsonConverter CreateConverter(Type t, JsonSerializerOptions options)
            => (JsonConverter)Activator.CreateInstance(typeof(TupleConverter<>).MakeGenericType(t))!;
    }

    private sealed class TupleConverter<T> : JsonConverter<T>
    {
        private static readonly Type[] Items = typeof(T).GetGenericArguments();

        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartArray)
                throw new JsonException($"expected an array for {typeof(T).Name}");
            var values = new object?[Items.Length];
            for (int i = 0; i < Items.Length; i++)
            {
                reader.Read();
                values[i] = JsonSerializer.Deserialize(ref reader, Items[i], options);
            }
            reader.Read();
            if (reader.TokenType != JsonTokenType.EndArray)
                throw new JsonException($"too many items for {typeof(T).Name}");
            return (T)Activator.CreateInstance(typeof(T), values)!;
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            for (int i = 0; i < Items.Length; i++)
                JsonSerializer.Serialize(writer, typeof(T).GetField("Item" + (i + 1))!.GetValue(value), Items[i], options);
            writer.WriteEndArray();
        }
    }

    /// <summary>Starting tensions, keyed by a pair, as [from, to, regard] rows.</summary>
    private sealed class TensionsConverter : JsonConverter<IReadOnlyDictionary<(string From, string To), double>>
    {
        public override IReadOnlyDictionary<(string From, string To), double> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var rows = JsonSerializer.Deserialize<List<(string, string, double)>>(ref reader, options) ?? new();
            var d = new Dictionary<(string, string), double>();
            foreach (var (from, to, regard) in rows)
                d[(from, to)] = regard;
            return d;
        }

        public override void Write(Utf8JsonWriter writer, IReadOnlyDictionary<(string From, string To), double> value, JsonSerializerOptions options)
            => JsonSerializer.Serialize(writer, value.Select(x => (x.Key.From, x.Key.To, x.Value)).ToList(), options);
    }
}
