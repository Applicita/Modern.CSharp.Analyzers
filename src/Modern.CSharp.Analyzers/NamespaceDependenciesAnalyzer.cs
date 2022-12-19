using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Modern.CSharp.Analyzers;

public static class DiagnosticDescriptors
{
    enum Id { MCS001, MCS002, MCS003 }

    enum Category { Multiservice }

    public static readonly DiagnosticDescriptor NamespaceDeclarationsMustHonorKnownProjectTypes
       = new(Id.MCS001.ToString(),
             "Either none or all of the namespace declarations in a project must fall under a single well-known namespace root: Apis, Contracts or <Name>Service",
             "Invalid namespace declaration in '{0}' project: '{1}'",     // TODO: Fix '{0}' shows as ''
             Category.Multiservice.ToString(),
             DiagnosticSeverity.Error,
             true,
             helpLinkUri: "https://github.com/Applicita/Orleans.Multiservice#pattern-rules");

    public static readonly DiagnosticDescriptor NamespaceUsingsMustHonorAllowedDependencies
       = new(Id.MCS002.ToString(),
             "Namespace usings must honor the allowed dependencies between Apis, Contracts and Services",
             "Invalid using for namespace '{0}': '{1}'",
             Category.Multiservice.ToString(),
             DiagnosticSeverity.Error,
             true,
             helpLinkUri: "https://github.com/Applicita/Orleans.Multiservice#pattern-rules");

    public static readonly DiagnosticDescriptor TypeReferencesMustHonorAllowedDependencies
       = new(Id.MCS003.ToString(),
             "Type reference must honor the allowed dependencies between Apis, Contracts and Services",
             "Invalid type reference in namespace '{0}': '{1}'",
             Category.Multiservice.ToString(),
             DiagnosticSeverity.Error,
             true,
             helpLinkUri: "https://github.com/Applicita/Orleans.Multiservice#pattern-rules");
}

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class NamespaceDependenciesAnalyzer : DiagnosticAnalyzer
{
    sealed record MultiserviceProjectInfo(
        ProjectKind Kind,
        string ProjectRootNamespace
    );

    enum ProjectKind { Unknown, Apis, Contracts, Service, Other }

    readonly SyntaxTreePathDepthComparer syntaxTreePathDepthComparer = new();

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
        DiagnosticDescriptors.NamespaceDeclarationsMustHonorKnownProjectTypes,
        DiagnosticDescriptors.NamespaceUsingsMustHonorAllowedDependencies,
        DiagnosticDescriptors.TypeReferencesMustHonorAllowedDependencies
    );

    public override void Initialize(AnalysisContext context)
    {
        #if DEBUG
        if (!Debugger.IsAttached) _ = Debugger.Launch();
        #endif

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(InitializeCompilation);
    }

    void InitializeCompilation(CompilationStartAnalysisContext context) // A compilation represents a project
    {
        var compilation = context.Compilation;
        if (compilation is null || !compilation.SyntaxTrees.Any()) return;

        var projectInfo = GetProjectInfo(context.Options, compilation);

        // TODO: ***HERE code complete of Try to determine the project kind and root namespace from the first namespace

        context.RegisterSyntaxNodeAction(
            ctx => AnalyzeNamespaceDeclaration(projectInfo, ctx),
            SyntaxKind.NamespaceDeclaration,
            SyntaxKind.FileScopedNamespaceDeclaration
            // TODO: SyntaxKind.QualifiedName, ?SyntaxKind.AliasQualifiedName?
        );
    }

    MultiserviceProjectInfo GetProjectInfo(AnalyzerOptions options, Compilation compilation)
    {
        // Get options from .editorconfig for the source file in this project that is the topmost on the filesystem hierarchy;
        // it does not make sense to have different options within the same project, so we ignore any other .editorconfig files
        var sortedTrees = compilation.SyntaxTrees.ToImmutableSortedSet(syntaxTreePathDepthComparer);
        var config = options.AnalyzerConfigOptionsProvider.GetOptions(sortedTrees.First());
        _ = config.TryGetValue("mcs_multiservice_apis_namespaces", out string? apisNamespaces);
        _ = config.TryGetValue("mcs_multiservice_contracts_namespaces", out string? contractsNamespaces);
        _ = config.TryGetValue("mcs_multiservice_services_namespaces", out string? servicesNamespaces);
        _ = config.TryGetValue("mcs_multiservice_other_namespaces", out string? otherNamespaces);

        // Try to determine the project kind and root namespace from the first namespace
        string firstNamespace = string.Empty;
        foreach (var t in sortedTrees)
        {
            var fsnsd = t.GetRoot().DescendantNodesAndSelf().OfType<FileScopedNamespaceDeclarationSyntax>().FirstOrDefault();
            if (fsnsd is not null) { firstNamespace = fsnsd.Name.ToString(); break; }

            var nsd = t.GetRoot().DescendantNodesAndSelf().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
            if (nsd is not null) { firstNamespace = nsd.Name.ToString(); break; }
        }
        string[] firstNamespaceParts = firstNamespace.Split('.');

        ProjectKind kind;
        string projectNamespace = string.Empty;

        if (MatchToNamespaces(apisNamespaces, "Apis")) kind = ProjectKind.Apis;
        else if (MatchToNamespaces(contractsNamespaces, "Contracts")) kind = ProjectKind.Contracts;
        else if (MatchToNamespaces(servicesNamespaces, "Service", true)) kind = ProjectKind.Service;
        else if (MatchToNamespaces(otherNamespaces, "")) kind = ProjectKind.Other;
        else kind = ProjectKind.Unknown;

        return new(kind, projectNamespace);

        bool MatchToNamespaces(string? namespaces, string defaultNamespaceName, bool defaultIsSuffix = false)
        {
            if (namespaces is null)
            {
                for (int i = 0, projectNamespaceNameLength = 0; i < firstNamespaceParts.Length; i++)
                {
                    string name = firstNamespaceParts[i];
                    projectNamespaceNameLength += name.Length;
                    if ((defaultIsSuffix && name.EndsWith(defaultNamespaceName, StringComparison.Ordinal)) ||
                        (!defaultIsSuffix && name.Equals(defaultNamespaceName, StringComparison.Ordinal)))
                    {
                        projectNamespace = firstNamespace.Substring(0, projectNamespaceNameLength);
                        return true;
                    }
                    projectNamespaceNameLength++;
                }
                return false;
            }

            string[] namespacesArray = namespaces.Split(',');
            for (int i = 0; i < namespacesArray.Length; i++)
            {
                string matchNamespace = namespacesArray[i].Trim();
                if (firstNamespace.Equals(matchNamespace, StringComparison.Ordinal) ||
                    firstNamespace.StartsWith(matchNamespace + ".", StringComparison.Ordinal))
                {
                    projectNamespace = matchNamespace;
                    return true;
                }
            }
            return false;
        }
    }

    sealed class SyntaxTreePathDepthComparer : Comparer<SyntaxTree>
    {
        public override int Compare(SyntaxTree x, SyntaxTree y) => x.FilePath.LastIndexOf(Path.DirectorySeparatorChar).CompareTo(y.FilePath.LastIndexOf(Path.DirectorySeparatorChar));
    }

    static void AnalyzeNamespaceDeclaration(MultiserviceProjectInfo project, SyntaxNodeAnalysisContext context)
    {
        var node = context.Node;

        string imports = "";
        var imported = context.SemanticModel.GetImportScopes(node.SpanStart); // Get usings before namespace declaration
        foreach (var import in imported)
            imports += string.Join(", ", import.Imports.Select(i => i.NamespaceOrType.ToString())) + "\n";

        var (name, usings) = node switch // Get usings within namespace declaration
        {
            NamespaceDeclarationSyntax nsd => (nsd.Name, nsd.Usings), // Can get namespace name with ...
            FileScopedNamespaceDeclarationSyntax fsnsd => (fsnsd.Name, fsnsd.Usings), // Can get namespace name with ...
            _ => new()
        };
        string usingsText = string.Join(", ", usings.Select(u => u.Name.ToString()));

        var error = Diagnostic.Create(DiagnosticDescriptors.NamespaceDeclarationsMustHonorKnownProjectTypes,
                                      name.GetLocation(),
                                      project.ProjectRootNamespace,
                                      name.ToString());
        context.ReportDiagnostic(error);

        Debug.WriteLine($"Project {project}: namespace declaration {name} of type {node.GetType().Name} with usings:\n{usingsText}\nand import scopes:\n{imports}");

        // TODO: how to check a file without namespace declaration e.g. top level statements? They can have using statements. Check generated code? or check that namespace explicitly?

        // Defaults if no settings: assume guidelines are followed: https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/names-of-namespaces 
        // <Company>.(<Product>|<Technology>)[.<Feature>][.<Subnamespace>]
        // So first 2 are solution, 3rd level is well-known project.

        // usings within a namespace are stored in the NamespaceDeclarationSyntax and the FileScopedNamespaceDeclarationSyntax
        // importscopes of the namespace contain the usings outside of a namespace
        // -> so only need to check the namespace declarations (plus qualified names)
        // We can get the using original source text location from imports and usings so can report on the usings as the error, given that the namespace declaration is checked first

        // TODO: consider proposal for configurable allowed dependencies (see onenote)
    }
}
