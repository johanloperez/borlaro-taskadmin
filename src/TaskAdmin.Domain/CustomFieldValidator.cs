using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using TaskAdmin.Domain.Entities;

namespace TaskAdmin.Domain;

public record FieldError(string Key, string Message);

/// <summary>Valida los valores de campos personalizados contra las definiciones del proyecto.
/// Sin esto, el jsonb aceptaría cualquier cosa —incluida cualquier cosa que escriba el agente—
/// y los filtros del tablero devolverían resultados incoherentes.</summary>
public static class CustomFieldValidator
{
    public static IReadOnlyList<FieldError> Validate(
        JsonObject? values,
        IReadOnlyCollection<CustomFieldDef> definitions)
    {
        var errors = new List<FieldError>();
        values ??= new JsonObject();

        var known = definitions.ToDictionary(d => d.Key, StringComparer.Ordinal);

        // Claves que no corresponden a ningún campo del proyecto: se rechazan en vez de
        // guardarse silenciosamente, porque un typo del cliente quedaría invisible para siempre.
        foreach (var pair in values)
        {
            if (!known.ContainsKey(pair.Key))
            {
                errors.Add(new FieldError(pair.Key, $"El campo «{pair.Key}» no existe en este proyecto."));
            }
        }

        foreach (var def in definitions)
        {
            var present = values.TryGetPropertyValue(def.Key, out var node) && node is not null;

            if (!present)
            {
                if (def.Required)
                {
                    errors.Add(new FieldError(def.Key, $"«{def.Label}» es obligatorio."));
                }
                continue;
            }

            var error = ValidateValue(def, node!);
            if (error is not null)
            {
                errors.Add(new FieldError(def.Key, error));
            }
        }

        return errors;
    }

    private static string? ValidateValue(CustomFieldDef def, JsonNode node)
    {
        switch (def.Type)
        {
            case CustomFieldType.Text:
            case CustomFieldType.LongText:
            case CustomFieldType.Url:
                if (node.GetValueKind() != JsonValueKind.String)
                    return $"«{def.Label}» debe ser texto.";
                if (def.Type == CustomFieldType.Url)
                {
                    var raw = node.GetValue<string>();
                    if (!string.IsNullOrWhiteSpace(raw) &&
                        !Uri.TryCreate(raw, UriKind.Absolute, out var uri))
                    {
                        return $"«{def.Label}» debe ser una URL absoluta.";
                    }
                }
                return null;

            case CustomFieldType.Number:
                return node.GetValueKind() == JsonValueKind.Number
                    ? null
                    : $"«{def.Label}» debe ser un número.";

            case CustomFieldType.Checkbox:
                return node.GetValueKind() is JsonValueKind.True or JsonValueKind.False
                    ? null
                    : $"«{def.Label}» debe ser verdadero o falso.";

            case CustomFieldType.Date:
                if (node.GetValueKind() != JsonValueKind.String)
                    return $"«{def.Label}» debe ser una fecha en formato ISO (aaaa-mm-dd).";
                return DateOnly.TryParse(node.GetValue<string>(), CultureInfo.InvariantCulture, out _)
                    ? null
                    : $"«{def.Label}» debe ser una fecha en formato ISO (aaaa-mm-dd).";

            case CustomFieldType.Select:
                if (node.GetValueKind() != JsonValueKind.String)
                    return $"«{def.Label}» debe ser una de las opciones.";
                return def.Options.Contains(node.GetValue<string>())
                    ? null
                    : $"«{def.Label}» acepta: {string.Join(", ", def.Options)}.";

            case CustomFieldType.MultiSelect:
                if (node is not JsonArray array)
                    return $"«{def.Label}» debe ser una lista de opciones.";
                foreach (var element in array)
                {
                    if (element is null || element.GetValueKind() != JsonValueKind.String ||
                        !def.Options.Contains(element.GetValue<string>()))
                    {
                        return $"«{def.Label}» acepta: {string.Join(", ", def.Options)}.";
                    }
                }
                return null;

            case CustomFieldType.User:
                if (node.GetValueKind() != JsonValueKind.String)
                    return $"«{def.Label}» debe ser el id de una persona.";
                return Guid.TryParse(node.GetValue<string>(), out _)
                    ? null
                    : $"«{def.Label}» debe ser el id de una persona.";

            default:
                return null;
        }
    }
}
