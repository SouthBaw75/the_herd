using System;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace CattleRanch.Sim.Persistence
{
    /// <summary>
    /// THE save/load entry point (ARCHITECTURE.md non-negotiable #5: save/load =
    /// serialize/deserialize <see cref="RanchState"/>). All serialization policy
    /// lives here — domain types stay attribute-free and keep their
    /// <c>internal set</c> encapsulation. The contract resolver below makes
    /// non-public setters writable so Json.NET restores them on load.
    /// </summary>
    public static class RanchSave
    {
        /// <summary>
        /// The single settings instance used for every save and load. Static and
        /// immutable-after-init so both directions of the round-trip always agree.
        /// </summary>
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ContractResolver = new NonPublicSetterContractResolver(),
            Converters = { new GameDateConverter() },
            Formatting = Formatting.Indented,
            Culture = CultureInfo.InvariantCulture,
        };

        /// <summary>Serializes the full ranch state to a JSON save string.</summary>
        public static string ToJson(RanchState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            return JsonConvert.SerializeObject(state, Settings);
        }

        /// <summary>
        /// Deserializes a save produced by <see cref="ToJson"/>. Never returns
        /// null: throws <see cref="ArgumentException"/> for null/empty input and
        /// <see cref="FormatException"/> for input that is not a valid save.
        /// </summary>
        public static RanchState FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException(
                    "Save data is null or empty; expected a RanchState JSON document.", nameof(json));
            }

            RanchState? state;
            try
            {
                state = JsonConvert.DeserializeObject<RanchState>(json, Settings);
            }
            catch (JsonException ex)
            {
                throw new FormatException(
                    "Save data is not valid RanchState JSON: " + ex.Message, ex);
            }

            if (state == null)
            {
                throw new FormatException(
                    "Save data deserialized to null; expected a RanchState JSON object.");
            }

            return state;
        }

        /// <summary>
        /// Makes properties with non-public (e.g. <c>internal</c>) setters
        /// writable during deserialization. Without this, Json.NET's default
        /// contract silently skips them and a load restores default values.
        /// </summary>
        private sealed class NonPublicSetterContractResolver : DefaultContractResolver
        {
            protected override JsonProperty CreateProperty(
                System.Reflection.MemberInfo member, MemberSerialization memberSerialization)
            {
                JsonProperty property = base.CreateProperty(member, memberSerialization);

                if (!property.Writable && member is System.Reflection.PropertyInfo propertyInfo)
                {
                    property.Writable = propertyInfo.GetSetMethod(nonPublic: true) != null;
                }

                return property;
            }
        }

        /// <summary>
        /// Serializes <see cref="GameDate"/> as its single source of truth — the
        /// bare <see cref="GameDate.TotalDays"/> integer — and reconstructs it via
        /// the <c>GameDate(int)</c> constructor. Chosen over relying on Json.NET's
        /// constructor-parameter-to-property name matching, which (a) would also
        /// emit the derived Year/Season/DayOfSeason properties into every save as
        /// redundant, silently-ignored data, and (b) silently breaks if the ctor
        /// parameter is renamed or an overload is added.
        /// </summary>
        private sealed class GameDateConverter : JsonConverter<GameDate>
        {
            public override void WriteJson(JsonWriter writer, GameDate? value, JsonSerializer serializer)
            {
                if (value == null)
                {
                    writer.WriteNull();
                    return;
                }

                writer.WriteValue(value.TotalDays);
            }

            public override GameDate? ReadJson(
                JsonReader reader,
                Type objectType,
                GameDate? existingValue,
                bool hasExistingValue,
                JsonSerializer serializer)
            {
                switch (reader.TokenType)
                {
                    case JsonToken.Null:
                        return null;

                    case JsonToken.Integer:
                        return new GameDate(Convert.ToInt32(reader.Value, CultureInfo.InvariantCulture));

                    case JsonToken.StartObject:
                        // Tolerate the object form {"TotalDays": n} (e.g. a save
                        // written before this converter existed).
                        JObject obj = JObject.Load(reader);
                        JToken? totalDays = obj["TotalDays"] ?? obj["totalDays"];
                        if (totalDays != null && totalDays.Type == JTokenType.Integer)
                        {
                            return new GameDate(totalDays.Value<int>());
                        }

                        throw new JsonSerializationException(
                            "GameDate object form must contain an integer 'TotalDays' member.");

                    default:
                        throw new JsonSerializationException(
                            $"Unexpected token {reader.TokenType} for GameDate; expected an integer TotalDays.");
                }
            }
        }
    }
}
