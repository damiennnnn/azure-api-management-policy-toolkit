// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
    };

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
