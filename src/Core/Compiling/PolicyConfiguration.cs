// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

/// <summary>
/// Defines external methods that can be invoked during compilation, allowing dynamic configuration of APIM policy compilation
/// </summary>
public static partial class CompilerUtils
{

    private static readonly Dictionary<string, ExternalMethod> SupportedExternalMethods = new()
    {
        { "Environment.GetEnvironmentVariable",
            new ExternalMethod(Environment.GetEnvironmentVariable,
                CompilationErrors.EnvironmentVariableNameMustBeAConstant) },
        { "File.ReadAllText",
            new ExternalMethod(File.ReadAllText,
                CompilationErrors.InlinedFileNameMustBeAConstant) },
        { "JsonProperties.Get",
            new ExternalMethod(name =>
            {
                if (CompileProperties.TryGetString(name, out var value))
                {
                    return value;
                }
                return null;
            },
                CompilationErrors.ExternalValueKeyMustBeAConstant)  }
    };

    public static object? GetFromConfigProperty(IPropertySymbol symbol)
    {
        var expressionMethod = symbol.DeclaringSyntaxReferences
            .Select(r => r.GetSyntax())
            .OfType<PropertyDeclarationSyntax>()
            .FirstOrDefault();

        // User-defined config classes can define properties with [JsonProperty] attribute, to be looked up in the current json config
        // Allows for strong typing of config named values
        if (expressionMethod is not null)
        {
            var jsonProperty = expressionMethod?
                .AttributeLists
                .Select(attrListSyn
                => attrListSyn.Attributes
                    .First(attrSyn => attrSyn.Name.ToString().Contains("JsonProperty")))
                .FirstOrDefault();

            var argument = jsonProperty?.ArgumentList?.Arguments[0].Expression.GetFirstToken().ValueText!;

            return (symbol?.Type) switch
            {
                { Kind: SymbolKind.ArrayType } => CompileProperties.GetArray(argument),
                _ => CompileProperties.Get(argument),
            };
        }

        return null;
    }

    private class ExternalMethod
    {
        public Func<string, string?> Method { get; }
        public DiagnosticDescriptor ErrorDescriptor { get; }

        public string Invoke(InvocationExpressionSyntax syntax, IDocumentCompilationContext context)
        {
            // Currently only supporting a single argument
            // Can be expanded later
            var argument = EvaluateArgument(syntax.ArgumentList.Arguments[0].Expression, context);

            if (argument is null)
            {
                context.Report(Diagnostic.Create(
                    ErrorDescriptor,
                    syntax.GetLocation()
                ));
                return "";
            }

            return Method(argument.ToString()!) ?? "";
        }

        public ExternalMethod(Func<string, string?> method, DiagnosticDescriptor errorDescriptor)
        {
            Method = method;
            ErrorDescriptor = errorDescriptor;
        }
    }
}
