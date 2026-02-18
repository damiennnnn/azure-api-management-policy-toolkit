// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;

using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

/// <summary>
/// Provides a store for external values that can be accessed during policy compilation.
/// </summary>
public static class CompileProperties
{
    private static Dictionary<string, JsonElement> All { get; set; } = new Dictionary<string, JsonElement>();

    public static bool TryGetString(string key, out string? value) 
    {
        value = default;
        if (All.TryGetValue(key, out JsonElement element))
        {
            value = element.GetString();
            return true;
        }

        return false;
    }

    public static bool TryGetArray(string key, out string[]? value)
    {
        value = default;
        if (All.TryGetValue(key, out JsonElement element))
        {
            value = element.EnumerateArray()
                .Select(e => e.GetString() ?? "")
                .ToArray();

            return true;
        }

        return false;
    }

    public static string[]? GetArray(string key)
    {
        if (All.TryGetValue(key, out JsonElement element))
        {
            return element.EnumerateArray()
                .Select(e => e.GetString() ?? "")
                .ToArray();
        }
        return default;
    }

    public static string? Get(string key)
    {
        if (All.TryGetValue(key, out JsonElement element))
        {
            return element.GetString();
        }
        return default;
    }

    public static JsonElement? GetElement(string key)
    {
        if (All.TryGetValue(key, out JsonElement element))
        {
            return element;
        }
        return default;
    }

    public static void LoadFromJson(string json)
    {
        try
        {
            var values = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
            if (values is not null)
            {
                All = values;
            }
        }
        catch (JsonException e)
        {
            throw;
        }
    }
}
