using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;
using DD3.CoverScope.Models.Reviews;
using DD3.CoverScope.Services.Reviews;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.MSBuild;

namespace DD3.CoverScope.Analysis;

// Runs in a separate process so SDK/MSBuild loading and cancellation do not belong to the web host.
internal static class SourceIndexWorker
{
    public static async Task<int> RunAsync(string[] arguments)
    {
        if (arguments.Length != 5) { return 2; }
        try
        {
            MSBuildLocator.RegisterDefaults();
            var result = await IndexAsync(arguments[1], arguments[2], arguments[4]);
            await File.WriteAllTextAsync(arguments[3], JsonSerializer.Serialize(result, ReviewStore.Json));
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<CodeEvidence> IndexAsync(string root, string target, string configuration)
    {
        using var workspace = MSBuildWorkspace.Create(new Dictionary<string, string> { ["Configuration"] = configuration });
        workspace.LoadMetadataForReferencedProjects = false;
        Solution solution;
        if (target.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        { solution = (await workspace.OpenProjectAsync(target)).Solution; }
        else { solution = await workspace.OpenSolutionAsync(target); }
        var result = new CodeEvidence { Complete = true };
        var projects = solution.Projects.Where(x => x.Language == LanguageNames.CSharp).ToArray();
        var generatorFailures = new ConcurrentQueue<string>();
        // Subscribe before compiling any project: compiling a dependent project can load its references' generators too.
        foreach (var reference in projects.SelectMany(x => x.AnalyzerReferences).OfType<AnalyzerFileReference>().Distinct<AnalyzerFileReference>(ReferenceEqualityComparer.Instance))
        {
            reference.AnalyzerLoadFailed += (_, failure) =>
            {
                // SDK analyzer lists also contain dependency DLLs with no analyzers or generators.
                if (failure.ErrorCode == AnalyzerLoadFailureEventArgs.FailureErrorCode.NoAnalyzers) { return; }
                generatorFailures.Enqueue($"Analyzer/source generator load failed: {reference.FullPath}. {failure.ErrorCode}: {failure.Message} " +
                    $"Required compiler: {failure.ReferencedCompilerVersion}; CoverScope compiler: {typeof(Compilation).Assembly.GetName().Version}.");
            };
        }
        var projectKeys = projects.ToDictionary(x => x.Id, x => Path.GetRelativePath(root, x.FilePath!).Replace('\\', '/'));
        var fileProjects = projects.SelectMany(p => p.Documents.Where(d => d.FilePath is not null).Select(d => (Path: d.FilePath!, Key: projectKeys[p.Id])))
            .GroupBy(x => x.Path, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Select(x => x.Key).Distinct().ToArray(), StringComparer.Ordinal);
        var types = new Dictionary<string, TypeEvidence>(StringComparer.Ordinal);
        foreach (var project in projects)
        {
            var compilation = await project.GetCompilationAsync();
            if (compilation is null)
            { result.Complete = false; result.Limitations.Add("Compilation unavailable: " + projectKeys[project.Id]); continue; }
            var errors = compilation.GetDiagnostics().Where(x => x.Severity == DiagnosticSeverity.Error).Take(10).ToArray();
            if (errors.Length > 0) { result.Complete = false; result.Limitations.AddRange(errors.Select(x => projectKeys[project.Id] + ": " + x)); }
            foreach (var document in project.Documents)
            {
                if (document.FilePath is null) { continue; }
                var source = (await document.GetTextAsync()).ToString();
                if (IsGeneratedTestEntryPoint(document.FilePath, source)) { continue; }
                var path = Path.GetRelativePath(root, document.FilePath).Replace('\\', '/');
                if (path.StartsWith("../", StringComparison.Ordinal))
                { result.Complete = false; result.Limitations.Add("External linked source excluded: " + path); continue; }
                if (path.Split('/').Contains("obj") || path.Split('/').Contains("bin")) { continue; }
                if (new FileInfo(document.FilePath).LinkTarget is not null)
                { result.Complete = false; result.Limitations.Add("Symlinked source excluded: " + path); continue; }
                result.Documents.Add(new(projectKeys[project.Id], path, source));
                var syntax = await document.GetSyntaxRootAsync();
                var semantic = await document.GetSemanticModelAsync();
                if (syntax is null || semantic is null) { result.Complete = false; continue; }
                foreach (var declaration in syntax.DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
                {
                    if (semantic.GetDeclaredSymbol(declaration) is not INamedTypeSymbol symbol) { continue; }
                    var id = TypeId(projectKeys[project.Id], symbol);
                    if (!types.TryGetValue(id, out var type))
                    { types[id] = type = new(id, projectKeys[project.Id], symbol.Name, symbol.ToDisplayString(), symbol.TypeKind.ToString())
                        { Namespace = symbol.ContainingNamespace.IsGlobalNamespace ? "Global namespace" : symbol.ContainingNamespace.ToDisplayString() }; }
                    if (!type.Files.Contains(path, StringComparer.Ordinal)) { type.Files.Add(path); }
                    var directChildren = declaration is TypeDeclarationSyntax td ? td.Members.Cast<SyntaxNode>().ToArray() : [];
                    var headerTokens = declaration.DescendantTokens().Where(t => !directChildren.Any(m => m.Span.Contains(t.Span)));
                    type.DeclarationFingerprint = string.Join("\u001e", type.DeclarationFingerprint.Split('\u001e', StringSplitOptions.RemoveEmptyEntries)
                        .Append(Fingerprint(headerTokens)).Distinct().Order(StringComparer.Ordinal));
                    if (declaration is TypeDeclarationSyntax { ParameterList: { } parameters })
                    {
                        var primaryId = id + "|primary-constructor";
                        if (type.Members.All(x => x.Id != primaryId))
                        {
                            var lines = parameters.GetLocation().GetLineSpan();
                            type.Members.Add(new(primaryId, symbol.Name, "Constructor", path, lines.StartLinePosition.Line + 1,
                                lines.EndLinePosition.Line + 1, symbol.Name + parameters.ToString(), Fingerprint(parameters.DescendantTokens())));
                        }
                    }
                    foreach (var node in declaration.DescendantNodes().Where(n => n.Ancestors().OfType<BaseTypeDeclarationSyntax>().FirstOrDefault() == declaration))
                    {
                        if (node is not (BaseMethodDeclarationSyntax or PropertyDeclarationSyntax or IndexerDeclarationSyntax or EventDeclarationSyntax or VariableDeclaratorSyntax)) { continue; }
                        var member = semantic.GetDeclaredSymbol(node);
                        if (member is null || member.IsImplicitlyDeclared || member.ContainingType is null || member is not (IMethodSymbol or IPropertySymbol or IFieldSymbol or IEventSymbol)) { continue; }
                        var canonical = member is IMethodSymbol method ? method.PartialDefinitionPart ?? method : member;
                        var memberId = id + "|" + (canonical.GetDocumentationCommentId() ?? canonical.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                        var span = node.GetLocation().GetLineSpan();
                        var attributes = member.GetAttributes();
                        var isTest = attributes.Any(a => a.AttributeClass?.ToDisplayString() is "Xunit.FactAttribute" or "Xunit.TheoryAttribute");
                        var value = new MemberEvidence(memberId, member.Name, member.Kind.ToString(), path, span.StartLinePosition.Line + 1,
                            span.EndLinePosition.Line + 1, node.ToString(), Fingerprint((node is VariableDeclaratorSyntax ? node.Parent?.Parent ?? node : node).DescendantTokens())) { IsTest = isTest };
                        var existing = type.Members.FindIndex(x => x.Id == memberId);
                        if (existing < 0) { type.Members.Add(value); }
                        else
                        {
                            var prior = type.Members[existing];
                            var fingerprint = string.Join("\u001e", prior.Fingerprint.Split('\u001e').Append(value.Fingerprint).Distinct().Order(StringComparer.Ordinal));
                            type.Members[existing] = (node is MethodDeclarationSyntax { Body: not null } or MethodDeclarationSyntax { ExpressionBody: not null } ? value : prior) with { Fingerprint = fingerprint };
                        }
                    }
                }
                foreach (var node in syntax.DescendantNodes().Where(n => n is InvocationExpressionSyntax or ObjectCreationExpressionSyntax or BaseTypeSyntax or IdentifierNameSyntax))
                {
                    var ownerDeclaration = node.Ancestors().OfType<BaseTypeDeclarationSyntax>().FirstOrDefault();
                    var owner = ownerDeclaration is null ? null : semantic.GetDeclaredSymbol(ownerDeclaration) as INamedTypeSymbol;
                    if (owner is null) { continue; }
                    var referenced = node is BaseTypeSyntax baseType ? semantic.GetTypeInfo(baseType.Type).Type : semantic.GetSymbolInfo(node).Symbol;
                    var destination = referenced is IMethodSymbol m ? m.ContainingType : referenced as INamedTypeSymbol;
                    if (destination is null || destination.IsTupleType || destination.IsImplicitlyDeclared || SymbolEqualityComparer.Default.Equals(owner, destination)) { continue; }
                    var location = destination.DeclaringSyntaxReferences.FirstOrDefault()?.SyntaxTree.FilePath;
                    if (location is null || !fileProjects.TryGetValue(location, out var keys) || keys.Length != 1) { continue; }
                    var kind = node is InvocationExpressionSyntax or ObjectCreationExpressionSyntax ? "Calls" : node is BaseTypeSyntax ? (destination.TypeKind == TypeKind.Interface ? "Implements" : "Inherits") : "Uses type";
                    result.Relationships.Add(new(TypeId(projectKeys[project.Id], owner), TypeId(keys[0], destination), kind, path,
                        node.GetLocation().GetLineSpan().StartLinePosition.Line + 1, "Head"));
                }
            }
        }
        result.Types = types.Values.OrderBy(x => x.FullName, StringComparer.Ordinal).ToList();
        result.Relationships = result.Relationships.Where(x => types.ContainsKey(x.From) && types.ContainsKey(x.To)).Distinct().ToList();
        foreach (var diagnostic in workspace.Diagnostics)
        {
            var classified = ClassifyDiagnostic(diagnostic);
            result.Diagnostics.Add(classified);
            if (classified.AffectsCompleteness) { result.Limitations.Add(diagnostic.Message); result.Complete = false; }
        }
        if (!generatorFailures.IsEmpty) { result.Complete = false; result.Limitations.AddRange(generatorFailures.Distinct()); }
        if (projects.Length == 0) { result.Complete = false; result.Limitations.Add("No C# projects could be loaded."); }
        result.Limitations.Add("Relationships connect indexed C# types through statically resolved references, not runtime dispatch or per-test coverage. Razor markup, source-generator output and SDK test entry points are excluded from declaration counts.");
        return result;
    }

    internal static bool IsGeneratedTestEntryPoint(string path, string source)
    {
        var parts = path.Replace('\\', '/').Split('/');
        return parts.Length >= 5 && parts[^5].Equals("microsoft.net.test.sdk", StringComparison.OrdinalIgnoreCase)
            && parts[^3].Equals("build", StringComparison.OrdinalIgnoreCase)
            && parts[^1].Equals("Microsoft.NET.Test.Sdk.Program.cs", StringComparison.OrdinalIgnoreCase)
            && source.TrimStart('\uFEFF', ' ', '\t', '\r', '\n').StartsWith("// <auto-generated>", StringComparison.Ordinal);
    }

    internal static CodeDiagnostic ClassifyDiagnostic(WorkspaceDiagnostic diagnostic)
    {
        // Roslyn can label NuGet audit warnings as Failure (dotnet/roslyn#75182).
        // Recognize only the audit payload; preserve the original kind and message.
        const string prefix = " with message: ";
        var marker = diagnostic.Message.IndexOf(prefix, StringComparison.Ordinal);
        var payload = marker < 0 ? diagnostic.Message : diagnostic.Message[(marker + prefix.Length)..];
        var audit = System.Text.RegularExpressions.Regex.IsMatch(payload,
            @"\APackage '[^'\r\n]+' \S+ has a known (?:low|moderate|high|critical) severity vulnerability, https://[^\s]+\s*\z",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        return new(diagnostic.Kind.ToString(), audit ? "Package audit" : "Workspace", diagnostic.Message,
            !audit && diagnostic.Kind == WorkspaceDiagnosticKind.Failure);
    }

    private static string TypeId(string project, INamedTypeSymbol symbol) => project + "|" + symbol.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    internal static string Fingerprint(IEnumerable<SyntaxToken> tokens) => string.Join("\u001f", tokens.Select(x => x.RawKind + ":" + x.Text + ":" +
        string.Join("", x.LeadingTrivia.Concat(x.TrailingTrivia).Where(t => t.IsDirective).Select(t => t.ToFullString()))));
}
