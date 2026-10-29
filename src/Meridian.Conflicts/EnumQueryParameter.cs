using System.Text.Json.Nodes;
using Meridian.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi;

namespace Meridian.Conflicts;

internal static class EnumQueryParameter
{
    /// <summary>
    /// Documents a query parameter bound as text as the enum it is parsed into, so the
    /// contract (and the generated client) still names the enum and its glossary words.
    /// </summary>
    public static RouteHandlerBuilder WithEnumQuery<TEnum>(this RouteHandlerBuilder builder, string name, string @default)
        where TEnum : struct, Enum =>
        builder.AddOpenApiOperationTransformer((operation, context, _) =>
        {
            var schemaName = typeof(TEnum).Name;
            context.Document!.AddComponent(schemaName, new OpenApiSchema
            {
                Enum = Enum.GetValues<TEnum>().Select(v => (JsonNode)SnakeCaseEnumConverter<TEnum>.ToName(v)).ToList(),
            });
            var parameter = (OpenApiParameter)operation.Parameters!.Single(p => p.Name == name);
            parameter.Schema = new OpenApiSchemaReference(schemaName, context.Document) { Default = @default };
            return Task.CompletedTask;
        });
}
