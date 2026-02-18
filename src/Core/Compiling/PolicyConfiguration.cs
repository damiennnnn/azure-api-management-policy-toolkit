// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

/// <summary>
/// Defines external methods that can be invoked during compilation, allowing dynamic configuration of APIM policy compilation
/// </summary>
public static partial class CompilerUtils
{

    private static readonly Dictionary<string, InlineMethod> InlineMethods = new()
    {
        { "Environment.GetEnvironmentVariable",
            new InlineMethod(Environment.GetEnvironmentVariable,
                CompilationErrors.EnvironmentVariableNameMustBeAConstant) },
        { "File.ReadAllText",
            new InlineMethod(File.ReadAllText,
                CompilationErrors.InlinedFileNameMustBeAConstant) },
        { "context.GetVariable", 
            new InlineMethod(param =>
            {
                return @$"context.Variables.GetValueOrDefault<string>(""{param}"", """")";
            },
                CompilationErrors.ExternalValueKeyMustBeAConstant)  },
        { "context.Placeholder",
            new InlineMethod(param =>
            {
                return @$"%%{param}%%";
            },
                CompilationErrors.ExternalValueKeyMustBeAConstant)  },
        { "JsonProperties.Get",
            new InlineMethod(name =>
            {
                if (CompileProperties.TryGetString(name, out var value))
                {
                    return value;
                }
                return null;
            },
                CompilationErrors.ExternalValueKeyMustBeAConstant)  }
    };

    public static object? GetFromConfigProperty(IPropertySymbol symbol, IDocumentCompilationContext context)
    {
        var expressionMethod = symbol.DeclaringSyntaxReferences
            .Select(r => r.GetSyntax())
            .OfType<PropertyDeclarationSyntax>()
            .FirstOrDefault();

        // User-defined config classes can define properties with [JsonProperty] attribute, to be looked up in the current json config
        // Allows for strong typing of config named values
        if (expressionMethod is not null)
        {
            var jsonPropertyAttribute = expressionMethod?
                .AttributeLists
                .Select(attrListSyn
                => attrListSyn.Attributes
                    .First(attrSyn => attrSyn.Name.ToString().Contains("JsonProperty")))
                .FirstOrDefault();

            var argument = jsonPropertyAttribute?.ArgumentList?.Arguments[0].Expression.GetFirstToken().ValueText!;

            // Support per-operation context properties, allowing one policy document to be used for multiple operations with different config values
            if (context.PerOperationContext is JsonProperty jsonProperty)
            {
                var propertyValue = jsonProperty.Value.EnumerateObject().FirstOrDefault(p => p.NameEquals(argument)).Value;

                return (symbol?.Type) switch
                {
                    { Kind: SymbolKind.ArrayType } => propertyValue.EnumerateArray()
                        .Select(e => e.ToString() ?? "")
                        .ToArray(),
                    _ => propertyValue.ToString(),
                };
            }

            return (symbol?.Type) switch
            {
                { Kind: SymbolKind.ArrayType } => CompileProperties.GetArray(argument),
                _ => CompileProperties.Get(argument),
            };
        }

        return null;
    }

    public static string ProcessInterpolation(this InterpolatedStringContentSyntax interpolationContent, IDocumentCompilationContext context)
    {
        if (interpolationContent is InterpolatedStringTextSyntax text)
        {
            return text.TextToken.ValueText;
        }

        if (interpolationContent is InterpolationSyntax interpolation)
        {
            var result = interpolation.Expression.ProcessParameter(context);

            if (interpolation.Expression is InvocationExpressionSyntax invocation
                && invocation.Expression.ToString() == $"context.{nameof(IInlineHelpers.GetVariable)}")
            {
                return $"{{{result}}}";
            }

            return result;
        }

        context.Report(Diagnostic.Create(
                    CompilationErrors.NotSupportedParameter,
                    interpolationContent.GetLocation()
                ));

        return string.Empty;
    }

    private class InlineMethod
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

        public InlineMethod(Func<string, string?> method, DiagnosticDescriptor errorDescriptor)
        {
            Method = method;
            ErrorDescriptor = errorDescriptor;
        }
    }
}
