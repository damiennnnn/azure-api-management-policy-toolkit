// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
//
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Attributes;

[AttributeUsage(AttributeTargets.Method)]
public class PerOperationKeyAttribute(string? key) : Attribute
{
    public string? Key { get; } = key;
}
