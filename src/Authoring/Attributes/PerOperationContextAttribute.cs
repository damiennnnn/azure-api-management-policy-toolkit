// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
//
//

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public class PerOperationContextAttribute(string? name) : Attribute
{
    public string? Name { get; } = name;
}
