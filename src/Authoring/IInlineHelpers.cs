// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
//
//

using System.Collections;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;

/// <summary>
/// Methods that don't represent APIM policy elements, but are helpers for inlining expressions during compilation.
/// </summary>
public interface IInlineHelpers
{
    /// <summary>
    /// Retrieves the value of a variable or expression by its name.
    /// Compiled to inline policy expression. Can be used in an if condition.
    /// </summary>
    /// <param name="name">The name of the variable or expression to retrieve. Expressions may be allowed depending on the context.</param>
    /// <param name="defaultValue">An optional default value to return if the specified variable does not exist. Defaults to an empty string.</param>
    /// <returns>The value of the specified variable or expression, or <see langword="null"/> if the variable does not exist.</returns>
    string GetVariable([ExpressionAllowed] string name, string? defaultValue = "");
    int GetVariable([ExpressionAllowed] string name, int defaultValue);
    /// <summary>
    /// Allows iteration over all JSON objects in the current configuration context.
    /// 
    /// <br/>
    /// <c>config.json</c> must contain multiple JSON objects with defined properties.
    /// <br/>
    /// 
    /// Code blocks will be replicated for each JSON object in the collection, allowing for dynamic policy generation based on the configuration.
    /// The values within the current data set will be used for these blocks.
    /// </summary>
    /// <returns></returns>
    ConfigDataSetEnumerable DataSets();
}

public class ConfigDataSetEnumerable : IEnumerable
{
    public IEnumerator GetEnumerator()
    {
        yield return "";
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}