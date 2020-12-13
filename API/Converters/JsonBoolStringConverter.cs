using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace API.Converters
{
    public class JsonBoolStringConverter : JsonConverter<bool>
    {
        /// <inheritdoc />
        public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                return reader.GetString().ToLower() == "true";
            }

            return reader.GetBoolean();
        }

        /// <inheritdoc />
        public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
        {
            writer.WriteBooleanValue(value);
        }
    }
}