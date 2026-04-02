using Microsoft.CodeAnalysis;

namespace CS2Luau.Compiler.Diagnostics;

public enum DiagnosticSeverity
{
    Info,
    Warning,
    Error,
}

public sealed record SourceLocation(string FilePath, int Line, int Column);

public sealed record CompilerDiagnostic(
    string Code,
    string Message,
    DiagnosticSeverity Severity,
    SourceLocation? Location = null)
{
    public bool IsError => Severity == DiagnosticSeverity.Error;
}

public sealed class DiagnosticBag
{
    private readonly List<CompilerDiagnostic> diagnostics = [];

    public IReadOnlyList<CompilerDiagnostic> Diagnostics => diagnostics;

    public void Add(CompilerDiagnostic diagnostic)
    {
        diagnostics.Add(diagnostic);
    }

    public void Report(string code, string message, DiagnosticSeverity severity, SyntaxNode? node = null)
    {
        Add(new CompilerDiagnostic(code, message, severity, CreateLocation(node?.GetLocation())));
    }

    public void ReportUnsupported(string message, SyntaxNode node, string code = "CS2L0041")
    {
        Report(code, message, DiagnosticSeverity.Error, node);
    }

    public void AddRoslynDiagnostic(Microsoft.CodeAnalysis.Diagnostic diagnostic)
    {
        if (diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Hidden)
        {
            return;
        }

        var severity = diagnostic.Severity switch
        {
            Microsoft.CodeAnalysis.DiagnosticSeverity.Info => DiagnosticSeverity.Info,
            Microsoft.CodeAnalysis.DiagnosticSeverity.Warning => DiagnosticSeverity.Warning,
            _ => DiagnosticSeverity.Error,
        };

        Add(new CompilerDiagnostic(
            diagnostic.Id,
            diagnostic.GetMessage(),
            severity,
            CreateLocation(diagnostic.Location)));
    }

    private static SourceLocation? CreateLocation(Location? location)
    {
        if (location is null || !location.IsInSource)
        {
            return null;
        }

        var span = location.GetLineSpan();
        return new SourceLocation(
            span.Path,
            span.StartLinePosition.Line + 1,
            span.StartLinePosition.Character + 1);
    }
}
