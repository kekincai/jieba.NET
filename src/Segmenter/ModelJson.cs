using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JiebaNet.Segmenter
{
    internal static class ModelJson
    {
        internal static readonly JsonSerializerOptions Options = new()
        {
            MaxDepth = 32,
            Converters = { new CharacterKeyConverter() }
        };

        internal static T Load<T>(string path)
        {
            using var stream = ConfigManager.OpenRead(path);
            return JsonSerializer.Deserialize<T>(stream, Options)
                ?? throw new InvalidDataException($"The jieba.NET model '{path}' contains null.");
        }

        // The model uses single UTF-16 characters as JSON property names.
        private sealed class CharacterKeyConverter : JsonConverter<char>
        {
            public override char Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
                => Parse(reader.GetString());

            public override char ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
                => Parse(reader.GetString());

            public override void Write(Utf8JsonWriter writer, char value, JsonSerializerOptions options)
                => writer.WriteStringValue(value.ToString());

            public override void WriteAsPropertyName(Utf8JsonWriter writer, char value, JsonSerializerOptions options)
                => writer.WritePropertyName(value.ToString());

            private static char Parse(string value)
                => value is { Length: 1 } ? value[0] : throw new JsonException("Model keys must contain exactly one UTF-16 character.");
        }
    }
}
