namespace CS2Luau.Compiler.Ir;

public sealed class IrProgram
{
    public required string Name { get; init; }
    public required IReadOnlyList<IrEnumDeclaration> Enums { get; init; }
    public required IReadOnlyList<IrClassDeclaration> Classes { get; init; }
    public required IReadOnlyList<IrStatement> TopLevelStatements { get; init; }
}

public sealed class IrEnumDeclaration
{
    public required string Name { get; init; }
    public required IReadOnlyList<IrEnumMember> Members { get; init; }
}

public sealed record IrEnumMember(string Name, long Value);

public sealed class IrClassDeclaration
{
    public required string Name { get; init; }
    public string? BaseTypeName { get; init; }
    public bool IsStatic { get; init; }
    public required IReadOnlyList<IrMemberInitializer> Initializers { get; init; }
    public required IrConstructorDeclaration Constructor { get; init; }
    public required IReadOnlyList<IrMethodDeclaration> Methods { get; init; }
}

public sealed record IrMemberInitializer(string Name, bool IsStatic, bool IsProperty, IrExpression? Initializer);

public sealed class IrConstructorDeclaration
{
    public required IReadOnlyList<string> Parameters { get; init; }
    public required IReadOnlyList<IrExpression> BaseArguments { get; init; }
    public required IReadOnlyList<IrStatement> Body { get; init; }
}

public sealed class IrMethodDeclaration
{
    public required string Name { get; init; }
    public required IReadOnlyList<string> Parameters { get; init; }
    public required IReadOnlyList<IrStatement> Body { get; init; }
    public bool IsStatic { get; init; }
}

public abstract record IrStatement;
public sealed record IrBlockStatement(IReadOnlyList<IrStatement> Statements) : IrStatement;
public sealed record IrLocalDeclarationStatement(string Name, IrExpression? Initializer) : IrStatement;
public sealed record IrAssignmentStatement(string Target, IrExpression Value) : IrStatement;
public sealed record IrExpressionStatement(IrExpression Expression) : IrStatement;
public sealed record IrIfStatement(IrExpression Condition, IReadOnlyList<IrStatement> ThenStatements, IReadOnlyList<IrStatement>? ElseStatements) : IrStatement;
public sealed record IrWhileStatement(IrExpression Condition, IReadOnlyList<IrStatement> Body) : IrStatement;
public sealed record IrRepeatStatement(IrExpression Condition, IReadOnlyList<IrStatement> Body) : IrStatement;
public sealed record IrForEachStatement(string Identifier, IrExpression Iterable, IReadOnlyList<IrStatement> Body) : IrStatement;
public sealed record IrReturnStatement(IrExpression? Expression) : IrStatement;
public sealed record IrBreakStatement() : IrStatement;
public sealed record IrContinueStatement() : IrStatement;

public abstract record IrExpression;
public sealed record IrRawExpression(string Code) : IrExpression;
public sealed record IrLambdaExpression(IReadOnlyList<string> Parameters, IReadOnlyList<IrStatement> Body) : IrExpression;
