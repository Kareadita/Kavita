using System.Text.Json;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace API.Extensions;

public static class DataContextExtensions
{

    public static PropertyBuilder<TProperty> HasJsonConversion<TProperty>(this PropertyBuilder<TProperty> builder, TProperty def = default)
    {
        return builder.HasConversion(
            v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
            v => JsonSerializer.Deserialize<TProperty>(v, JsonSerializerOptions.Default) ?? def
        );
    }

}
