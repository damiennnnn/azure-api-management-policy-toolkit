// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Xml.Linq;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring;
using Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling.Syntax;

/// <summary>
/// Repeats foreach blocks with every provided JSON config as context. Allows for multiple policy blocks to be generated from a single code block, with different values.
/// </summary>
public class OperationForEachStatementCompiler : ISyntaxCompiler
{
    private readonly Lazy<BlockCompiler> _blockCompiler;

    public OperationForEachStatementCompiler(Lazy<BlockCompiler> blockCompiler)
    {
        this._blockCompiler = blockCompiler;
    }

    public SyntaxKind Syntax => SyntaxKind.ForEachStatement;

    public void Compile(IDocumentCompilationContext context, SyntaxNode node)
    {
        var forEachStatement = node as ForEachStatementSyntax ?? throw new NullReferenceException();

        var operations = context.PerOperationContext.GetValueOrDefault();

        if (!context.PerOperationContext.HasValue)
        {
            context.Report(Diagnostic.Create(
                CompilationErrors.OperationContextRequired,
                forEachStatement.GetLocation()
            ));
            return;
        }

        if (forEachStatement.Expression.ToString() == $"context.{nameof(IInlineHelpers.Operations)}()")
        {
            var operation = operations.Value.EnumerateObject();

            // API operation has multiple JSON object configs, repeat the block with each config as context
            if (operation.All(op => op.Value.ValueKind == JsonValueKind.Object))
            {
                var currentOperationContext = context.PerOperationContext;
                foreach (var op in operation)
                {
                    context.PerOperationContext = op;
                    var innerContext = new DocumentCompilationContext(context, context.CurrentElement);
                    _blockCompiler.Value.Compile(innerContext, forEachStatement.Statement);
                }
                context.PerOperationContext = currentOperationContext;
            }
            else
            {
                context.Report(Diagnostic.Create(
                    CompilationErrors.OperationContextRequired,
                    forEachStatement.GetLocation()
                ));

                return;
            }
        }
    }
}