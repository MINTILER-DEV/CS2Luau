using CS2Luau.Compiler.Configuration;
using CS2Luau.Compiler.Diagnostics;
using CS2Luau.Compiler.Lowering;
using CS2Luau.Compiler.Roblox;
using CS2Luau.Runtime;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CS2Luau.Compiler;

using CompilerDiagnosticSeverity = CS2Luau.Compiler.Diagnostics.DiagnosticSeverity;

public sealed class Cs2LuauCompiler
{
    private const string ImplicitUsingsSource =
        """
        global using Roblox;
        global using Roblox.Services;
        global using Roblox.Instances;
        global using Roblox.Datatypes;
        global using Roblox.Signals;
        global using Roblox.Enums;
        """;

    public Task<CompileResult> CompileAsync(CompileInput input, CancellationToken cancellationToken = default)
    {
        var diagnostics = new DiagnosticBag();
        var compilation = CreateCompilation(input);
        foreach (var diagnostic in compilation.GetDiagnostics(cancellationToken))
        {
            diagnostics.AddRoslynDiagnostic(diagnostic);
        }

        foreach (var tree in compilation.SyntaxTrees.Where(tree => tree.FilePath != "<implicit-globals>"))
        {
            var walker = new UnsupportedFeatureWalker(diagnostics);
            walker.Visit(tree.GetRoot(cancellationToken));
        }

        var program = new ProgramLowerer(
            compilation,
            new RobloxSymbolRegistry(compilation),
            diagnostics,
            cancellationToken).Lower(input);

            var normalizedDiagnostics = input.WarningsAsErrors
            ? diagnostics.Diagnostics.Select(diagnostic => diagnostic.Severity == CompilerDiagnosticSeverity.Warning
                ? diagnostic with { Severity = CompilerDiagnosticSeverity.Error }
                : diagnostic).ToArray()
            : diagnostics.Diagnostics.ToArray();

        var outputs = new List<GeneratedOutput>();
        if (!normalizedDiagnostics.Any(diagnostic => diagnostic.IsError))
        {
            var luauSource = Emission.LuauEmitter.Emit(program, input.ScriptType);
            outputs.Add(new GeneratedOutput($"{input.Name}.luau", luauSource, GeneratedOutputKind.Luau));

            if (input.IncludeRuntime)
            {
                outputs.AddRange(RuntimeAssetCatalog.GetAssets()
                    .Select(asset => new GeneratedOutput(asset.RelativePath, asset.Content, GeneratedOutputKind.Runtime)));
            }

            if (input.EmitRbxmx)
            {
                var rbxmx = Emission.RbxmxSerializer.Serialize(
                    input.Name,
                    input.ScriptType,
                    luauSource,
                    input.IncludeRuntime ? RuntimeAssetCatalog.GetAssets() : []);
                outputs.Add(new GeneratedOutput($"{input.Name}.rbxmx", rbxmx, GeneratedOutputKind.Rbxmx));
            }
        }

        return Task.FromResult(new CompileResult
        {
            Outputs = outputs,
            Diagnostics = normalizedDiagnostics,
        });
    }

    private static CSharpCompilation CreateCompilation(CompileInput input)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        var syntaxTrees = input.Documents
            .Select(document => CSharpSyntaxTree.ParseText(document.Text, parseOptions, document.Path))
            .Concat([CSharpSyntaxTree.ParseText(ImplicitUsingsSource, parseOptions, "<implicit-globals>")])
            .ToArray();

        var references = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))?
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => MetadataReference.CreateFromFile(path))
            .ToList() ?? [];

        var robloxAssemblyPath = typeof(global::Roblox.Globals).Assembly.Location;
        if (!string.IsNullOrWhiteSpace(robloxAssemblyPath))
        {
            references.Add(MetadataReference.CreateFromFile(robloxAssemblyPath));
        }

        return CSharpCompilation.Create(
            input.Name,
            syntaxTrees,
            references,
            new CSharpCompilationOptions(OutputKind.ConsoleApplication));
    }

    private sealed class UnsupportedFeatureWalker : CSharpSyntaxWalker
    {
        private readonly DiagnosticBag diagnostics;

        public UnsupportedFeatureWalker(DiagnosticBag diagnostics)
        {
            this.diagnostics = diagnostics;
        }

        public override void VisitAwaitExpression(AwaitExpressionSyntax node)
        {
            diagnostics.Report("CS2L0012", "async/await is not supported yet.", CompilerDiagnosticSeverity.Error, node);
            base.VisitAwaitExpression(node);
        }

        public override void VisitQueryExpression(QueryExpressionSyntax node)
        {
            diagnostics.Report("CS2L0013", "LINQ query syntax is not supported yet.", CompilerDiagnosticSeverity.Error, node);
            base.VisitQueryExpression(node);
        }

        public override void VisitYieldStatement(YieldStatementSyntax node)
        {
            diagnostics.Report("CS2L0014", "yield iterators are not supported yet.", CompilerDiagnosticSeverity.Error, node);
            base.VisitYieldStatement(node);
        }

        public override void VisitTryStatement(TryStatementSyntax node)
        {
            diagnostics.Report("CS2L0015", "The exception model is not supported yet.", CompilerDiagnosticSeverity.Error, node);
            base.VisitTryStatement(node);
        }

        public override void VisitThrowStatement(ThrowStatementSyntax node)
        {
            diagnostics.Report("CS2L0015", "The exception model is not supported yet.", CompilerDiagnosticSeverity.Error, node);
            base.VisitThrowStatement(node);
        }

        public override void VisitUnsafeStatement(UnsafeStatementSyntax node)
        {
            diagnostics.Report("CS2L0016", "unsafe code is not supported.", CompilerDiagnosticSeverity.Error, node);
            base.VisitUnsafeStatement(node);
        }

        public override void VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            if (node.Modifiers.Any(SyntaxKind.PartialKeyword))
            {
                diagnostics.Report("CS2L0017", "partial classes are not supported yet.", CompilerDiagnosticSeverity.Error, node);
            }

            if (node.TypeParameterList is not null)
            {
                diagnostics.Report("CS2L0043", "Generic user-defined classes are not supported yet.", CompilerDiagnosticSeverity.Error, node);
            }

            base.VisitClassDeclaration(node);
        }

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            if (node.TypeParameterList is not null)
            {
                diagnostics.Report("CS2L0044", "Generic user-defined methods are not supported yet.", CompilerDiagnosticSeverity.Error, node);
            }

            if (node.ParameterList.Parameters.FirstOrDefault()?.Modifiers.Any(SyntaxKind.ThisKeyword) == true)
            {
                diagnostics.Report("CS2L0045", "Extension methods are not supported.", CompilerDiagnosticSeverity.Error, node);
            }

            base.VisitMethodDeclaration(node);
        }

        public override void VisitOperatorDeclaration(OperatorDeclarationSyntax node)
        {
            diagnostics.Report("CS2L0046", "Operator overloading is not supported.", CompilerDiagnosticSeverity.Error, node);
            base.VisitOperatorDeclaration(node);
        }

        public override void VisitConversionOperatorDeclaration(ConversionOperatorDeclarationSyntax node)
        {
            diagnostics.Report("CS2L0046", "Operator overloading is not supported.", CompilerDiagnosticSeverity.Error, node);
            base.VisitConversionOperatorDeclaration(node);
        }

        public override void VisitAttributeList(AttributeListSyntax node)
        {
            diagnostics.Report("CS2L0047", "Attributes are not supported yet.", CompilerDiagnosticSeverity.Error, node);
            base.VisitAttributeList(node);
        }

        public override void VisitAnonymousMethodExpression(AnonymousMethodExpressionSyntax node)
        {
            diagnostics.Report("CS2L0048", "Anonymous methods are not supported. Use lambdas instead.", CompilerDiagnosticSeverity.Error, node);
            base.VisitAnonymousMethodExpression(node);
        }

        public override void VisitConditionalExpression(ConditionalExpressionSyntax node)
        {
            diagnostics.Report("CS2L0049", "The conditional operator is not supported yet.", CompilerDiagnosticSeverity.Error, node);
            base.VisitConditionalExpression(node);
        }
    }
}
