// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;

using Microsoft.Azure.ApiManagement.PolicyToolkit.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

public class ProjectCompiler(DocumentCompiler documentCompiler)
{
    public async Task<ProjectCompilerResult> Compile(ProjectCompilerOptions options,
        CancellationToken cancellationToken = default)
    {
        if (options.ConfigJsonPath is string configPath)
        {
            CompileProperties.LoadFromJson(File.ReadAllText(configPath));
        }

        var result = new ProjectCompilerResult();
        var workspace = MSBuildWorkspace.Create();
        await Console.Out.WriteLineAsync($"Opening project '{options.ProjectPath}'");
        var project = await workspace.OpenProjectAsync(options.ProjectPath, cancellationToken: cancellationToken);
        if (!project.SupportsCompilation)
        {
            throw new Exception("Cannot compile project which does not support compilation");
        }

        await Console.Out.WriteLineAsync($"Compiling project '{options.ProjectPath}'");
        var compilation = await project.GetCompilationAsync(cancellationToken);
        if (compilation is null)
        {
            throw new NullReferenceException("Compilation is null");
        }

        var emitResult = compilation.Emit(Stream.Null, cancellationToken: cancellationToken);
        if (!emitResult.Success)
        {
            result.CompilerDiagnostics =
            [
                ..emitResult.Diagnostics.Where(d =>
                    !d.IsSuppressed &&
                    d is { Severity: DiagnosticSeverity.Error } or
                        { Severity: DiagnosticSeverity.Warning, IsWarningAsError: true })
            ];
            foreach (var diag in result.CompilerDiagnostics)
            {
                await Console.Error.WriteLineAsync(diag.ToString());
            }

            return result;
        }

        var onlyUserSyntaxTrees =
            compilation.SyntaxTrees.Where(t => PathUtils.IsNotInObjOrBinFolder(Path.GetFullPath(t.FilePath)));

        foreach (var syntaxTree in onlyUserSyntaxTrees)
        {
            await Console.Out.WriteLineAsync($"File '{syntaxTree.FilePath}' processing");
            var root = await syntaxTree.GetRootAsync(cancellationToken);
            var semantics = compilation.GetSemanticModel(syntaxTree);
            var documents = root.GetDocumentAttributedClasses(semantics);

            foreach (var document in documents)
            {
                if (document.ExtractCompileContextName(semantics) is string perOperationConfigName 
                    && CompileProperties.GetElement(perOperationConfigName) is JsonElement element)
                {
                    await Console.Out.WriteLineAsync(
                            $"Document '{document.Identifier}' is marked as per-operation with config name '{perOperationConfigName}'");

                    // For a file marked with the CompileContext attribute, look for the object with the key specified in that attribute.
                    // Every keyed object underneath the root object will be a `context`, where the properties within that object are used for a compilation.
                    // Every keyed object thus represents an individual operation.

                    await Parallel.ForEachAsync(element.EnumerateObject(), async (operation, ct) =>
                    {
                        await Console.Out.WriteLineAsync($"Processing operation '{operation.Name}' for document '{document.Identifier}'");

                        await CompileAndSave(options,
                            document,
                            compilation,
                            result,
                            semantics,
                            syntaxTree,
                            $"{document.ExtractDocumentFileName(semantics)}-{operation.Name}",
                            operation);
                    });

                    continue;
                }

                await CompileAndSave(options, 
                    document, 
                    compilation, 
                    result, 
                    semantics, 
                    syntaxTree, document.ExtractDocumentFileName(semantics));

            }

            await Console.Out.WriteLineAsync($"File '{syntaxTree.FilePath}' processed");
        }

        return result;
    }

    private async Task CompileAndSave(ProjectCompilerOptions options,
        ClassDeclarationSyntax document,
        Compilation compilation,
        ProjectCompilerResult result,
        SemanticModel semantics,
        SyntaxTree syntaxTree,
        string fileName,
        JsonProperty? context = default)
    {
        var documentResult = documentCompiler.Compile(compilation, document, context);
        result.DocumentResults.Add(documentResult);

        foreach (var error in documentResult.Errors)
        {
            await Console.Error.WriteLineAsync(error.ToString());
        }

        var targetFile = await FileUtils.WriteToFileRawAsync(new FileUtils.Data()
        {
            Element = documentResult.Document,
            SourceFolder = Path.GetDirectoryName(options.ProjectPath)!,
            SourceFilePath = syntaxTree.FilePath,
            OutputFolder = options.OutputFolder,
            OutputFilePath = PathUtils.PrepareOutputPath(fileName, options.FileExtension),
            FormatCode = options.FormatCode,
            XmlWriterSettings = options.XmlWriterSettings,
        });

        await Console.Out.WriteLineAsync($"File '{targetFile}' created");
    }
}