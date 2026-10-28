using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace Meridian.Platform.Http;

/// <summary>
/// Validates request bodies with their DataAnnotations and <see cref="IValidatableObject"/>
/// rules, nested objects and list items included, before the handler runs. Error keys
/// are camelCase JSON paths (<c>selection.to</c>, <c>sessions[1]</c>), so the UI puts
/// each message on the right field.
/// </summary>
public static class RequestValidation
{
    public static EndpointFilterDelegate Factory(EndpointFilterFactoryContext context, EndpointFilterDelegate next)
    {
        var validated = context.MethodInfo.GetParameters()
            .Select((p, i) => (p.ParameterType, Index: i))
            .Where(p => IsValidated(p.ParameterType))
            .Select(p => p.Index)
            .ToArray();
        if (validated.Length == 0) return next;

        return invocation =>
        {
            var errors = new Dictionary<string, List<string>>();
            foreach (var index in validated)
                if (invocation.Arguments[index] is { } argument)
                    Validate(argument, "", errors);
            return errors.Count == 0
                ? next(invocation)
                : ValueTask.FromResult<object?>(TypedResults.ValidationProblem(
                    errors.ToDictionary(e => e.Key, e => e.Value.ToArray())));
        };
    }

    /// <summary>Types of ours that declare any rule.</summary>
    private static bool IsValidated(Type type) =>
        type.Namespace?.StartsWith("Meridian.", StringComparison.Ordinal) == true
        && (typeof(IValidatableObject).IsAssignableFrom(type)
            || type.GetProperties().Any(p => p.GetCustomAttributes<ValidationAttribute>().Any() || IsValidated(p.PropertyType)));

    private static void Validate(object instance, string prefix, Dictionary<string, List<string>> errors)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(instance, new ValidationContext(instance), results, validateAllProperties: true);
        foreach (var result in results)
        foreach (var member in result.MemberNames.DefaultIfEmpty(""))
            Add(errors, Join(prefix, JsonPath(member)), result.ErrorMessage ?? "Invalid value.");

        foreach (var property in instance.GetType().GetProperties())
        {
            var value = property.GetValue(instance);
            var path = Join(prefix, JsonPath(property.Name));
            if (value is null || value is string) continue;
            if (IsValidated(property.PropertyType)) Validate(value, path, errors);
            else if (value is IEnumerable items && property.PropertyType.IsGenericType
                     && IsValidated(property.PropertyType.GetGenericArguments()[0]))
            {
                var i = 0;
                foreach (var item in items)
                    if (item is not null) Validate(item, $"{path}[{i++}]", errors);
            }
        }
    }

    /// <summary><c>Sessions[1]</c> → <c>sessions[1]</c>; <c>Selection.To</c> → <c>selection.to</c>.</summary>
    private static string JsonPath(string member) =>
        string.Join('.', member.Split('.').Select(part => JsonNamingPolicy.CamelCase.ConvertName(part)));

    private static string Join(string prefix, string path) =>
        prefix.Length == 0 ? path : path.Length == 0 ? prefix : $"{prefix}.{path}";

    private static void Add(Dictionary<string, List<string>> errors, string key, string message)
    {
        if (!errors.TryGetValue(key, out var messages)) errors[key] = messages = [];
        messages.Add(message);
    }
}
