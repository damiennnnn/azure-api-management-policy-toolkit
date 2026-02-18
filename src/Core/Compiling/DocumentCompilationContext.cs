// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Immutable;
using System.Text.Json;
using System.Xml.Linq;

using Microsoft.CodeAnalysis;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

public class DocumentCompilationContext(Compilation compilation, SyntaxNode syntaxRoot, XElement currentElement, JsonProperty? operationContext = default)
    : IDocumentCompilationContext, IDocumentCompilationResult
{
    public DocumentCompilationContext(IDocumentCompilationContext parent, XElement currentElement, JsonProperty? operationContext = default)
        : this(parent.Compilation, parent.SyntaxRoot, currentElement, parent.PerOperationContext)
    {
        RootElement = parent.RootElement;
        Diagnostics = parent.Diagnostics;
    }

    public void AddPolicy(XNode element) => CurrentElement.Add(element);
    public void Report(Diagnostic diagnostic) => Diagnostics.Add(diagnostic);

    public Compilation Compilation { get; } = compilation;
    public SyntaxNode SyntaxRoot { get; } = syntaxRoot;
    public XElement RootElement { get; } = currentElement;
    public XElement CurrentElement { get; } = currentElement;
    public IList<Diagnostic> Diagnostics { get; } = new List<Diagnostic>();

    public XElement Document => CurrentElement;
    public ImmutableArray<Diagnostic> Errors => [..Diagnostics];

    public JsonProperty? PerOperationContext { get; set; } = operationContext;
}