using System.Globalization;
using CS2Luau.Compiler.Configuration;
using CS2Luau.Compiler.Diagnostics;
using CS2Luau.Compiler.Ir;
using CS2Luau.Compiler.Roblox;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CS2Luau.Compiler.Lowering;

using CompilerDiagnosticSeverity = CS2Luau.Compiler.Diagnostics.DiagnosticSeverity;

internal sealed class ProgramLowerer
{
    private readonly CSharpCompilation compilation;
    private readonly RobloxSymbolRegistry roblox;
    private readonly DiagnosticBag diagnostics;
    private readonly CancellationToken cancellationToken;
    private int temporaryCounter;
    private INamedTypeSymbol? currentClass;

    public ProgramLowerer(CSharpCompilation compilation, RobloxSymbolRegistry roblox, DiagnosticBag diagnostics, CancellationToken cancellationToken)
    {
        this.compilation = compilation;
        this.roblox = roblox;
        this.diagnostics = diagnostics;
        this.cancellationToken = cancellationToken;
    }

    public IrProgram Lower(CompileInput input)
    {
        var enums = new List<IrEnumDeclaration>();
        var classes = new List<IrClassDeclaration>();
        var topLevelStatements = new List<IrStatement>();

        foreach (var tree in compilation.SyntaxTrees.Where(tree => !IsImplicitSyntaxTree(tree)).OrderBy(tree => tree.FilePath, StringComparer.OrdinalIgnoreCase))
        {
            var root = tree.GetCompilationUnitRoot(cancellationToken);
            var semanticModel = compilation.GetSemanticModel(tree);
            CollectMembers(root.Members, semanticModel, enums, classes, topLevelStatements);
        }

        return new IrProgram
        {
            Name = input.Name,
            Enums = enums,
            Classes = classes,
            TopLevelStatements = topLevelStatements,
        };
    }

    private static bool IsImplicitSyntaxTree(SyntaxTree tree)
    {
        return tree.FilePath.StartsWith("<implicit-", StringComparison.Ordinal);
    }

    private void CollectMembers(
        SyntaxList<MemberDeclarationSyntax> members,
        SemanticModel semanticModel,
        List<IrEnumDeclaration> enums,
        List<IrClassDeclaration> classes,
        List<IrStatement> topLevelStatements)
    {
        foreach (var member in members)
        {
            switch (member)
            {
                case NamespaceDeclarationSyntax namespaceDeclaration:
                    CollectMembers(namespaceDeclaration.Members, semanticModel, enums, classes, topLevelStatements);
                    break;
                case FileScopedNamespaceDeclarationSyntax fileScopedNamespaceDeclaration:
                    CollectMembers(fileScopedNamespaceDeclaration.Members, semanticModel, enums, classes, topLevelStatements);
                    break;
                case EnumDeclarationSyntax enumDeclaration:
                    enums.Add(LowerEnum(enumDeclaration, semanticModel));
                    break;
                case ClassDeclarationSyntax classDeclaration:
                    classes.Add(LowerClass(classDeclaration, semanticModel));
                    break;
                case GlobalStatementSyntax globalStatement:
                    topLevelStatements.AddRange(LowerStatement(globalStatement.Statement, semanticModel));
                    break;
                default:
                    diagnostics.ReportUnsupported($"Unsupported member declaration '{member.Kind()}'.", member);
                    break;
            }
        }
    }

    private IrEnumDeclaration LowerEnum(EnumDeclarationSyntax syntax, SemanticModel semanticModel)
    {
        long nextValue = -1;
        var members = new List<IrEnumMember>();
        foreach (var member in syntax.Members)
        {
            if (member.EqualsValue is not null)
            {
                var constant = semanticModel.GetConstantValue(member.EqualsValue.Value, cancellationToken);
                nextValue = constant.HasValue ? Convert.ToInt64(constant.Value, CultureInfo.InvariantCulture) : nextValue + 1;
            }
            else
            {
                nextValue++;
            }

            members.Add(new IrEnumMember(member.Identifier.ValueText, nextValue));
        }

        return new IrEnumDeclaration
        {
            Name = syntax.Identifier.ValueText,
            Members = members,
        };
    }

    private IrClassDeclaration LowerClass(ClassDeclarationSyntax syntax, SemanticModel semanticModel)
    {
        var classSymbol = semanticModel.GetDeclaredSymbol(syntax, cancellationToken);
        currentClass = classSymbol;
        var initializers = new List<IrMemberInitializer>();
        var methods = new List<IrMethodDeclaration>();
        ConstructorDeclarationSyntax? constructorSyntax = null;

        foreach (var group in syntax.Members.OfType<MethodDeclarationSyntax>().GroupBy(member => member.Identifier.ValueText))
        {
            if (group.Count() > 1)
            {
                diagnostics.Report("CS2L0042", $"Method overloads are not supported for '{group.Key}'.", CompilerDiagnosticSeverity.Error, group.First());
            }
        }

        if (syntax.Members.OfType<ConstructorDeclarationSyntax>().Count() > 1)
        {
            diagnostics.Report("CS2L0042", $"Constructor overloads are not supported for '{syntax.Identifier.ValueText}'.", CompilerDiagnosticSeverity.Error, syntax);
        }

        foreach (var member in syntax.Members)
        {
            switch (member)
            {
                case FieldDeclarationSyntax field:
                    foreach (var variable in field.Declaration.Variables)
                    {
                        initializers.Add(new IrMemberInitializer(
                            variable.Identifier.ValueText,
                            field.Modifiers.Any(SyntaxKind.StaticKeyword),
                            false,
                            variable.Initializer is null ? null : LowerExpression(variable.Initializer.Value, semanticModel)));
                    }
                    break;
                case PropertyDeclarationSyntax property:
                    if (property.AccessorList?.Accessors.Any(accessor => accessor.Body is not null || accessor.ExpressionBody is not null) == true)
                    {
                        diagnostics.Report("CS2L0031", $"Property '{property.Identifier.ValueText}' must be an auto-property in V1.", CompilerDiagnosticSeverity.Error, property);
                        break;
                    }

                    initializers.Add(new IrMemberInitializer(
                        property.Identifier.ValueText,
                        property.Modifiers.Any(SyntaxKind.StaticKeyword),
                        true,
                        property.Initializer is null ? null : LowerExpression(property.Initializer.Value, semanticModel)));
                    break;
                case ConstructorDeclarationSyntax constructor:
                    constructorSyntax = constructor;
                    break;
                case MethodDeclarationSyntax method:
                    methods.Add(LowerMethod(method, semanticModel));
                    break;
                case ClassDeclarationSyntax nestedClass:
                    diagnostics.Report("CS2L0032", $"Nested class '{nestedClass.Identifier.ValueText}' is not supported.", CompilerDiagnosticSeverity.Error, nestedClass);
                    break;
            }
        }

        currentClass = null;
        return new IrClassDeclaration
        {
            Name = syntax.Identifier.ValueText,
            BaseTypeName = classSymbol?.BaseType is { SpecialType: not SpecialType.System_Object } baseType &&
                           baseType.Locations.Any(location => location.IsInSource)
                ? baseType.Name
                : null,
            IsStatic = syntax.Modifiers.Any(SyntaxKind.StaticKeyword),
            Initializers = initializers,
            Constructor = LowerConstructor(constructorSyntax, semanticModel, initializers.Where(initializer => !initializer.IsStatic).ToArray()),
            Methods = methods,
        };
    }

    private IrConstructorDeclaration LowerConstructor(
        ConstructorDeclarationSyntax? syntax,
        SemanticModel semanticModel,
        IReadOnlyList<IrMemberInitializer> instanceInitializers)
    {
        if (syntax is null)
        {
            return new IrConstructorDeclaration
            {
                Parameters = [],
                BaseArguments = [],
                Body = LowerInitializers(instanceInitializers),
            };
        }

        if (syntax.Initializer is { ThisOrBaseKeyword.RawKind: (int)SyntaxKind.ThisKeyword })
        {
            diagnostics.Report("CS2L0033", "Constructor chaining with this(...) is not supported.", CompilerDiagnosticSeverity.Error, syntax.Initializer);
        }

        var body = LowerInitializers(instanceInitializers);
        if (syntax.Body is not null)
        {
            body.AddRange(LowerStatements(syntax.Body.Statements, semanticModel));
        }
        else if (syntax.ExpressionBody is not null)
        {
            body.Add(new IrExpressionStatement(LowerExpression(syntax.ExpressionBody.Expression, semanticModel)));
        }

        return new IrConstructorDeclaration
        {
            Parameters = syntax.ParameterList.Parameters.Select(parameter => parameter.Identifier.ValueText).ToArray(),
            BaseArguments = syntax.Initializer is { ThisOrBaseKeyword.RawKind: (int)SyntaxKind.BaseKeyword }
                ? syntax.Initializer.ArgumentList.Arguments.Select(argument => LowerExpression(argument.Expression, semanticModel)).ToArray()
                : [],
            Body = body,
        };
    }

    private static List<IrStatement> LowerInitializers(IReadOnlyList<IrMemberInitializer> initializers)
    {
        var statements = new List<IrStatement>();
        foreach (var initializer in initializers.Where(initializer => initializer.Initializer is not null))
        {
            statements.Add(new IrAssignmentStatement($"self.{initializer.Name}", initializer.Initializer!));
        }

        return statements;
    }

    private IrMethodDeclaration LowerMethod(MethodDeclarationSyntax syntax, SemanticModel semanticModel)
    {
        IReadOnlyList<IrStatement> body = syntax.Body is not null
            ? LowerStatements(syntax.Body.Statements, semanticModel)
            : syntax.ExpressionBody is not null
                ? [new IrReturnStatement(LowerExpression(syntax.ExpressionBody.Expression, semanticModel))]
                : [];

        return new IrMethodDeclaration
        {
            Name = syntax.Identifier.ValueText,
            Parameters = syntax.ParameterList.Parameters.Select(parameter => parameter.Identifier.ValueText).ToArray(),
            Body = body,
            IsStatic = syntax.Modifiers.Any(SyntaxKind.StaticKeyword),
        };
    }

    private IReadOnlyList<IrStatement> LowerStatements(SyntaxList<StatementSyntax> statements, SemanticModel semanticModel)
    {
        var result = new List<IrStatement>();
        foreach (var statement in statements)
        {
            result.AddRange(LowerStatement(statement, semanticModel));
        }

        return result;
    }

    private IReadOnlyList<IrStatement> LowerStatement(StatementSyntax statement, SemanticModel semanticModel)
    {
        switch (statement)
        {
            case BlockSyntax block:
                return [new IrBlockStatement(LowerStatements(block.Statements, semanticModel))];
            case LocalDeclarationStatementSyntax localDeclaration:
                return LowerLocalDeclaration(localDeclaration, semanticModel);
            case ExpressionStatementSyntax expressionStatement:
                return [LowerExpressionStatement(expressionStatement.Expression, semanticModel)];
            case ReturnStatementSyntax returnStatement:
                return [new IrReturnStatement(returnStatement.Expression is null ? null : LowerExpression(returnStatement.Expression, semanticModel))];
            case IfStatementSyntax ifStatement:
                return [new IrIfStatement(
                    LowerExpression(ifStatement.Condition, semanticModel),
                    LowerEmbeddedStatement(ifStatement.Statement, semanticModel),
                    ifStatement.Else is null ? null : LowerEmbeddedStatement(ifStatement.Else.Statement, semanticModel))];
            case WhileStatementSyntax whileStatement:
                return [new IrWhileStatement(LowerExpression(whileStatement.Condition, semanticModel), LowerEmbeddedStatement(whileStatement.Statement, semanticModel))];
            case DoStatementSyntax doStatement:
                return [new IrRepeatStatement(LowerExpression(doStatement.Condition, semanticModel), LowerEmbeddedStatement(doStatement.Statement, semanticModel))];
            case ForStatementSyntax forStatement:
                return LowerForStatement(forStatement, semanticModel);
            case ForEachStatementSyntax forEachStatement:
                return [new IrForEachStatement(
                    forEachStatement.Identifier.ValueText,
                    LowerExpression(forEachStatement.Expression, semanticModel),
                    LowerEmbeddedStatement(forEachStatement.Statement, semanticModel))];
            case SwitchStatementSyntax switchStatement:
                return LowerSwitchStatement(switchStatement, semanticModel);
            case BreakStatementSyntax:
                return [new IrBreakStatement()];
            case ContinueStatementSyntax:
                return [new IrContinueStatement()];
            case EmptyStatementSyntax:
                return [];
            default:
                diagnostics.ReportUnsupported($"Unsupported statement '{statement.Kind()}'.", statement);
                return [];
        }
    }

    private IReadOnlyList<IrStatement> LowerLocalDeclaration(LocalDeclarationStatementSyntax localDeclaration, SemanticModel semanticModel)
    {
        return localDeclaration.Declaration.Variables
            .Select(variable => (IrStatement)new IrLocalDeclarationStatement(
                variable.Identifier.ValueText,
                variable.Initializer is null ? null : LowerExpression(variable.Initializer.Value, semanticModel)))
            .ToArray();
    }

    private IrStatement LowerExpressionStatement(ExpressionSyntax expression, SemanticModel semanticModel)
    {
        if (TryLowerIncrementOrCompoundAssignment(expression, semanticModel, out var statement))
        {
            return statement;
        }

        return new IrExpressionStatement(LowerExpression(expression, semanticModel));
    }

    private bool TryLowerIncrementOrCompoundAssignment(ExpressionSyntax expression, SemanticModel semanticModel, out IrStatement statement)
    {
        switch (expression)
        {
            case PostfixUnaryExpressionSyntax postfix when postfix.IsKind(SyntaxKind.PostIncrementExpression) || postfix.IsKind(SyntaxKind.PostDecrementExpression):
                {
                    var target = IrRendering.RenderExpression(LowerExpression(postfix.Operand, semanticModel));
                    var delta = postfix.IsKind(SyntaxKind.PostIncrementExpression) ? "1" : "-1";
                    statement = new IrAssignmentStatement(target, new IrRawExpression($"{target} + {delta}"));
                    return true;
                }
            case PrefixUnaryExpressionSyntax prefix when prefix.IsKind(SyntaxKind.PreIncrementExpression) || prefix.IsKind(SyntaxKind.PreDecrementExpression):
                {
                    var target = IrRendering.RenderExpression(LowerExpression(prefix.Operand, semanticModel));
                    var delta = prefix.IsKind(SyntaxKind.PreIncrementExpression) ? "1" : "-1";
                    statement = new IrAssignmentStatement(target, new IrRawExpression($"{target} + {delta}"));
                    return true;
                }
            case AssignmentExpressionSyntax assignment when assignment.Kind() != SyntaxKind.SimpleAssignmentExpression:
                {
                    var target = IrRendering.RenderExpression(LowerExpression(assignment.Left, semanticModel));
                    var operatorText = assignment.Kind() switch
                    {
                        SyntaxKind.AddAssignmentExpression => "+",
                        SyntaxKind.SubtractAssignmentExpression => "-",
                        SyntaxKind.MultiplyAssignmentExpression => "*",
                        SyntaxKind.DivideAssignmentExpression => "/",
                        _ => string.Empty,
                    };

                    if (!string.IsNullOrEmpty(operatorText))
                    {
                        statement = new IrAssignmentStatement(target, new IrRawExpression($"{target} {operatorText} {IrRendering.RenderExpression(LowerExpression(assignment.Right, semanticModel))}"));
                        return true;
                    }

                    break;
                }
        }

        statement = new IrExpressionStatement(new IrRawExpression("nil"));
        return false;
    }

    private IReadOnlyList<IrStatement> LowerForStatement(ForStatementSyntax forStatement, SemanticModel semanticModel)
    {
        var statements = new List<IrStatement>();
        if (forStatement.Declaration is not null)
        {
            statements.AddRange(LowerLocalDeclaration(SyntaxFactory.LocalDeclarationStatement(forStatement.Declaration), semanticModel));
        }

        foreach (var initializer in forStatement.Initializers)
        {
            statements.Add(LowerExpressionStatement(initializer, semanticModel));
        }

        var body = new List<IrStatement>(LowerEmbeddedStatement(forStatement.Statement, semanticModel));
        foreach (var incrementor in forStatement.Incrementors)
        {
            body.Add(LowerExpressionStatement(incrementor, semanticModel));
        }

        statements.Add(new IrWhileStatement(
            forStatement.Condition is null ? new IrRawExpression("true") : LowerExpression(forStatement.Condition, semanticModel),
            body));
        return statements;
    }

    private IReadOnlyList<IrStatement> LowerSwitchStatement(SwitchStatementSyntax switchStatement, SemanticModel semanticModel)
    {
        var tempName = $"__switch{temporaryCounter++}";
        var statements = new List<IrStatement>
        {
            new IrLocalDeclarationStatement(tempName, LowerExpression(switchStatement.Expression, semanticModel)),
        };

        IReadOnlyList<IrStatement>? elseStatements = null;
        foreach (var section in switchStatement.Sections.Reverse())
        {
            IReadOnlyList<IrStatement> sectionStatements = LowerStatements(section.Statements, semanticModel)
                .Where(statement => statement is not IrBreakStatement)
                .ToArray();

            var caseConditions = section.Labels
                .OfType<CaseSwitchLabelSyntax>()
                .Select(label => $"{tempName} == {IrRendering.RenderExpression(LowerExpression(label.Value, semanticModel))}")
                .ToArray();

            if (section.Labels.Any(label => label.IsKind(SyntaxKind.DefaultSwitchLabel)))
            {
                elseStatements = sectionStatements;
                continue;
            }

            if (caseConditions.Length == 0)
            {
                diagnostics.Report("CS2L0034", "Only constant switch cases are supported.", CompilerDiagnosticSeverity.Error, section);
                continue;
            }

            elseStatements =
            [
                new IrIfStatement(
                    new IrRawExpression(string.Join(" or ", caseConditions)),
                    sectionStatements,
                    elseStatements),
            ];
        }

        if (elseStatements is not null)
        {
            statements.AddRange(elseStatements);
        }

        return statements;
    }

    private IReadOnlyList<IrStatement> LowerEmbeddedStatement(StatementSyntax statement, SemanticModel semanticModel)
    {
        return statement is BlockSyntax block ? LowerStatements(block.Statements, semanticModel) : LowerStatement(statement, semanticModel);
    }

    private IrExpression LowerExpression(ExpressionSyntax expression, SemanticModel semanticModel)
    {
        switch (expression)
        {
            case LiteralExpressionSyntax literal:
                return new IrRawExpression(RenderLiteral(literal.Token.Value));
            case IdentifierNameSyntax identifier:
                return LowerIdentifier(identifier, semanticModel);
            case ThisExpressionSyntax:
                return new IrRawExpression("self");
            case BaseExpressionSyntax:
                return new IrRawExpression(currentClass?.BaseType?.Name ?? "self");
            case ParenthesizedExpressionSyntax parenthesized:
                return new IrRawExpression($"({IrRendering.RenderExpression(LowerExpression(parenthesized.Expression, semanticModel))})");
            case InvocationExpressionSyntax invocation:
                return LowerInvocation(invocation, semanticModel);
            case MemberAccessExpressionSyntax memberAccess:
                return LowerMemberAccess(memberAccess, semanticModel);
            case ObjectCreationExpressionSyntax objectCreation:
                return LowerObjectCreation(objectCreation, semanticModel);
            case ImplicitArrayCreationExpressionSyntax implicitArrayCreation:
                return LowerArrayCreation(implicitArrayCreation.Initializer.Expressions, semanticModel);
            case ArrayCreationExpressionSyntax arrayCreation:
                return arrayCreation.Initializer is not null
                    ? LowerArrayCreation(arrayCreation.Initializer.Expressions, semanticModel)
                    : new IrRawExpression("{}");
            case ElementAccessExpressionSyntax elementAccess:
                return LowerElementAccess(elementAccess, semanticModel);
            case AssignmentExpressionSyntax assignment:
                return new IrRawExpression($"{IrRendering.RenderExpression(LowerExpression(assignment.Left, semanticModel))} = {IrRendering.RenderExpression(LowerExpression(assignment.Right, semanticModel))}");
            case BinaryExpressionSyntax binary:
                return new IrRawExpression($"{IrRendering.RenderExpression(LowerExpression(binary.Left, semanticModel))} {MapBinaryOperator(binary.OperatorToken)} {IrRendering.RenderExpression(LowerExpression(binary.Right, semanticModel))}");
            case PrefixUnaryExpressionSyntax prefix:
                return new IrRawExpression($"{MapPrefixOperator(prefix.OperatorToken)}{IrRendering.RenderExpression(LowerExpression(prefix.Operand, semanticModel))}");
            case CastExpressionSyntax cast:
                return LowerExpression(cast.Expression, semanticModel);
            case SimpleLambdaExpressionSyntax simpleLambda:
                return LowerLambda([simpleLambda.Parameter.Identifier.ValueText], simpleLambda.Body, semanticModel);
            case ParenthesizedLambdaExpressionSyntax parenthesizedLambda:
                return LowerLambda(parenthesizedLambda.ParameterList.Parameters.Select(parameter => parameter.Identifier.ValueText).ToArray(), parenthesizedLambda.Body, semanticModel);
            case InitializerExpressionSyntax initializer when initializer.IsKind(SyntaxKind.ArrayInitializerExpression):
                return LowerArrayCreation(initializer.Expressions, semanticModel);
            default:
                diagnostics.ReportUnsupported($"Could not lower expression '{expression.Kind()}' to Luau.", expression);
                return new IrRawExpression("nil");
        }
    }

    private IrExpression LowerLambda(IReadOnlyList<string> parameters, CSharpSyntaxNode body, SemanticModel semanticModel)
    {
        IReadOnlyList<IrStatement> statements = body switch
        {
            BlockSyntax block => LowerStatements(block.Statements, semanticModel),
            ExpressionSyntax expression => [new IrExpressionStatement(LowerExpression(expression, semanticModel))],
            _ => [],
        };

        return new IrLambdaExpression(parameters, statements);
    }

    private IrExpression LowerIdentifier(IdentifierNameSyntax identifier, SemanticModel semanticModel)
    {
        var symbol = semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol;
        if (roblox.TryGetGlobalPropertyName(symbol, out var globalName))
        {
            return new IrRawExpression(globalName);
        }

        return symbol switch
        {
            IFieldSymbol field when field.ContainingType?.TypeKind == TypeKind.Enum && roblox.IsRobloxEnum(field.ContainingType) =>
                new IrRawExpression($"Enum.{field.ContainingType.Name}.{field.Name}"),
            _ when roblox.TryGetEnumWrapperMember(symbol, out var wrapperEnumName, out var wrapperMemberName) =>
                new IrRawExpression($"Enum.{wrapperEnumName}.{wrapperMemberName}"),
            IFieldSymbol field when field.IsStatic => new IrRawExpression($"{field.ContainingType?.Name}.{field.Name}"),
            IFieldSymbol => new IrRawExpression($"self.{identifier.Identifier.ValueText}"),
            IPropertySymbol property when property.IsStatic => new IrRawExpression($"{property.ContainingType?.Name}.{property.Name}"),
            IPropertySymbol => new IrRawExpression($"self.{identifier.Identifier.ValueText}"),
            _ => new IrRawExpression(identifier.Identifier.ValueText),
        };
    }

    private IrExpression LowerMemberAccess(MemberAccessExpressionSyntax memberAccess, SemanticModel semanticModel)
    {
        var symbol = semanticModel.GetSymbolInfo(memberAccess, cancellationToken).Symbol;
        if (roblox.TryGetGlobalPropertyName(symbol, out var globalName))
        {
            return new IrRawExpression(globalName);
        }

        if (symbol is IFieldSymbol field && field.ContainingType?.TypeKind == TypeKind.Enum && roblox.IsRobloxEnum(field.ContainingType))
        {
            return new IrRawExpression($"Enum.{field.ContainingType.Name}.{field.Name}");
        }

        if (roblox.TryGetEnumWrapperMember(symbol, out var wrapperEnumName, out var wrapperMemberName))
        {
            return new IrRawExpression($"Enum.{wrapperEnumName}.{wrapperMemberName}");
        }

        if (symbol is IPropertySymbol property &&
            property.Name == "Length" &&
            roblox.IsArray(semanticModel.GetTypeInfo(memberAccess.Expression, cancellationToken).Type))
        {
            return new IrRawExpression($"#{IrRendering.RenderExpression(LowerExpression(memberAccess.Expression, semanticModel))}");
        }

        return new IrRawExpression($"{IrRendering.RenderExpression(LowerExpression(memberAccess.Expression, semanticModel))}.{memberAccess.Name.Identifier.ValueText}");
    }

    private IrExpression LowerInvocation(InvocationExpressionSyntax invocation, SemanticModel semanticModel)
    {
        var symbol = semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol as IMethodSymbol;
        var arguments = invocation.ArgumentList.Arguments
            .Select(argument => IrRendering.RenderExpression(LowerExpression(argument.Expression, semanticModel)))
            .ToList();

        if (symbol is null)
        {
            diagnostics.ReportUnsupported("Could not resolve invocation target.", invocation);
            return new IrRawExpression("nil");
        }

        if (roblox.IsGlobalMethod(symbol, "print") || roblox.IsGlobalMethod(symbol, "warn"))
        {
            return new IrRawExpression($"{symbol.Name}({string.Join(", ", arguments)})");
        }

        if (roblox.IsGetServiceGeneric(symbol))
        {
            var serviceType = symbol.TypeArguments[0] as INamedTypeSymbol;
            if (!roblox.IsRobloxServiceType(serviceType))
            {
                diagnostics.Report("CS2L0105", "Roblox GetService<T>() requires a Roblox service type.", CompilerDiagnosticSeverity.Error, invocation);
            }

            var receiver = invocation.Expression is MemberAccessExpressionSyntax memberAccess
                ? IrRendering.RenderExpression(LowerExpression(memberAccess.Expression, semanticModel))
                : "game";
            return new IrRawExpression($"{receiver}:GetService(\"{serviceType?.Name}\")");
        }

        if (roblox.IsGetServiceString(symbol))
        {
            var receiver = invocation.Expression is MemberAccessExpressionSyntax memberAccess
                ? IrRendering.RenderExpression(LowerExpression(memberAccess.Expression, semanticModel))
                : "game";
            if (invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression is not LiteralExpressionSyntax)
            {
                diagnostics.Report("CS2L0106", "Roblox GetService(string) should preferably use a string literal for better static typing.", CompilerDiagnosticSeverity.Warning, invocation);
            }

            return new IrRawExpression($"{receiver}:GetService({arguments[0]})");
        }

        if (roblox.IsInstanceNewGeneric(symbol))
        {
            var instanceType = symbol.TypeArguments[0] as INamedTypeSymbol;
            if (!roblox.IsRobloxInstance(instanceType))
            {
                diagnostics.Report("CS2L0107", "Roblox Instance.New<T>() requires a Roblox instance type.", CompilerDiagnosticSeverity.Error, invocation);
            }

            return new IrRawExpression($"Instance.new(\"{instanceType?.Name}\")");
        }

        if (roblox.IsInstanceNewString(symbol))
        {
            return new IrRawExpression($"Instance.new({arguments[0]})");
        }

        if (symbol.MethodKind == MethodKind.DelegateInvoke)
        {
            var target = IrRendering.RenderExpression(LowerExpression(invocation.Expression, semanticModel));
            return new IrRawExpression($"{target}({string.Join(", ", arguments)})");
        }

        if (invocation.Expression is MemberAccessExpressionSyntax member &&
            member.Expression is BaseExpressionSyntax &&
            !symbol.IsStatic &&
            currentClass?.BaseType is not null)
        {
            var argumentList = arguments.Count == 0 ? "self" : $"self, {string.Join(", ", arguments)}";
            return new IrRawExpression($"{currentClass.BaseType.Name}.{symbol.Name}({argumentList})");
        }

        if (symbol.IsStatic)
        {
            var container = symbol.ContainingType?.Name ?? symbol.Name;
            return new IrRawExpression($"{container}.{symbol.Name}({string.Join(", ", arguments)})");
        }

        if (invocation.Expression is IdentifierNameSyntax)
        {
            return new IrRawExpression($"self:{symbol.Name}({string.Join(", ", arguments)})");
        }

        if (invocation.Expression is MemberAccessExpressionSyntax instanceMember && roblox.ShouldUseColonCall(symbol))
        {
            var receiver = IrRendering.RenderExpression(LowerExpression(instanceMember.Expression, semanticModel));
            return new IrRawExpression($"{receiver}:{symbol.Name}({string.Join(", ", arguments)})");
        }

        if (invocation.Expression is MemberAccessExpressionSyntax functionMember)
        {
            var receiver = IrRendering.RenderExpression(LowerExpression(functionMember.Expression, semanticModel));
            return new IrRawExpression($"{receiver}.{symbol.Name}({string.Join(", ", arguments)})");
        }

        return new IrRawExpression($"{symbol.Name}({string.Join(", ", arguments)})");
    }

    private IrExpression LowerObjectCreation(ObjectCreationExpressionSyntax objectCreation, SemanticModel semanticModel)
    {
        var type = semanticModel.GetTypeInfo(objectCreation, cancellationToken).Type as INamedTypeSymbol;
        var arguments = objectCreation.ArgumentList?.Arguments
            .Select(argument => IrRendering.RenderExpression(LowerExpression(argument.Expression, semanticModel)))
            .ToArray() ?? [];
        if (roblox.IsRobloxDatatype(type))
        {
            return new IrRawExpression($"{type!.Name}.new({string.Join(", ", arguments)})");
        }

        if (roblox.IsUserSourceType(type))
        {
            return new IrRawExpression($"{type!.Name}.new({string.Join(", ", arguments)})");
        }

        diagnostics.Report("CS2L0041", $"Could not lower object creation for type '{type?.ToDisplayString() ?? objectCreation.Type.ToString()}'.", CompilerDiagnosticSeverity.Error, objectCreation);
        return new IrRawExpression("nil");
    }

    private IrExpression LowerArrayCreation(SeparatedSyntaxList<ExpressionSyntax> expressions, SemanticModel semanticModel)
    {
        var items = expressions.Select(expression => IrRendering.RenderExpression(LowerExpression(expression, semanticModel)));
        return new IrRawExpression($"{{ {string.Join(", ", items)} }}");
    }

    private IrExpression LowerElementAccess(ElementAccessExpressionSyntax elementAccess, SemanticModel semanticModel)
    {
        var target = LowerExpression(elementAccess.Expression, semanticModel);
        var index = elementAccess.ArgumentList.Arguments.Count == 1
            ? IrRendering.RenderExpression(LowerExpression(elementAccess.ArgumentList.Arguments[0].Expression, semanticModel))
            : "0";
        var type = semanticModel.GetTypeInfo(elementAccess.Expression, cancellationToken).Type;
        if (roblox.IsArray(type))
        {
            return new IrRawExpression($"{IrRendering.RenderExpression(target)}[({index}) + 1]");
        }

        return new IrRawExpression($"{IrRendering.RenderExpression(target)}[{index}]");
    }

    private static string RenderLiteral(object? value)
    {
        return value switch
        {
            null => "nil",
            string text => $"\"{text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal).Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal)}\"",
            char character => $"\"{character}\"",
            bool boolean => boolean ? "true" : "false",
            float number => number.ToString(CultureInfo.InvariantCulture),
            double number => number.ToString(CultureInfo.InvariantCulture),
            decimal number => number.ToString(CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? "nil",
        };
    }

    private static string MapBinaryOperator(SyntaxToken token)
    {
        return token.Kind() switch
        {
            SyntaxKind.EqualsEqualsToken => "==",
            SyntaxKind.ExclamationEqualsToken => "~=",
            SyntaxKind.AmpersandAmpersandToken => "and",
            SyntaxKind.BarBarToken => "or",
            _ => token.Text,
        };
    }

    private static string MapPrefixOperator(SyntaxToken token)
    {
        return token.Kind() switch
        {
            SyntaxKind.ExclamationToken => "not ",
            _ => token.Text,
        };
    }
}

internal static class IrRendering
{
    public static string RenderExpression(IrExpression expression)
    {
        return expression switch
        {
            IrRawExpression raw => raw.Code,
            IrLambdaExpression lambda => RenderLambda(lambda),
            _ => string.Empty,
        };
    }

    private static string RenderLambda(IrLambdaExpression lambda)
    {
        var writer = new StringWriter();
        writer.WriteLine($"function({string.Join(", ", lambda.Parameters)})");
        RenderStatements(writer, lambda.Body, 1);
        writer.Write("end");
        return writer.ToString().TrimEnd();
    }

    private static void RenderStatements(StringWriter writer, IReadOnlyList<IrStatement> statements, int indentLevel)
    {
        foreach (var statement in statements)
        {
            RenderStatement(writer, statement, indentLevel);
        }
    }

    private static void RenderStatement(StringWriter writer, IrStatement statement, int indentLevel)
    {
        var indent = new string(' ', indentLevel * 4);
        switch (statement)
        {
            case IrBlockStatement block:
                RenderStatements(writer, block.Statements, indentLevel);
                break;
            case IrLocalDeclarationStatement localDeclaration:
                writer.WriteLine(localDeclaration.Initializer is null
                    ? $"{indent}local {localDeclaration.Name}"
                    : $"{indent}local {localDeclaration.Name} = {RenderExpression(localDeclaration.Initializer)}");
                break;
            case IrAssignmentStatement assignment:
                writer.WriteLine($"{indent}{assignment.Target} = {RenderExpression(assignment.Value)}");
                break;
            case IrExpressionStatement expressionStatement:
                writer.WriteLine($"{indent}{RenderExpression(expressionStatement.Expression)}");
                break;
            case IrIfStatement ifStatement:
                writer.WriteLine($"{indent}if {RenderExpression(ifStatement.Condition)} then");
                RenderStatements(writer, ifStatement.ThenStatements, indentLevel + 1);
                if (ifStatement.ElseStatements is not null && ifStatement.ElseStatements.Count > 0)
                {
                    writer.WriteLine($"{indent}else");
                    RenderStatements(writer, ifStatement.ElseStatements, indentLevel + 1);
                }
                writer.WriteLine($"{indent}end");
                break;
            case IrWhileStatement whileStatement:
                writer.WriteLine($"{indent}while {RenderExpression(whileStatement.Condition)} do");
                RenderStatements(writer, whileStatement.Body, indentLevel + 1);
                writer.WriteLine($"{indent}end");
                break;
            case IrRepeatStatement repeatStatement:
                writer.WriteLine($"{indent}repeat");
                RenderStatements(writer, repeatStatement.Body, indentLevel + 1);
                writer.WriteLine($"{indent}until not ({RenderExpression(repeatStatement.Condition)})");
                break;
            case IrForEachStatement forEachStatement:
                writer.WriteLine($"{indent}for _, {forEachStatement.Identifier} in ipairs({RenderExpression(forEachStatement.Iterable)}) do");
                RenderStatements(writer, forEachStatement.Body, indentLevel + 1);
                writer.WriteLine($"{indent}end");
                break;
            case IrReturnStatement returnStatement:
                writer.WriteLine(returnStatement.Expression is null
                    ? $"{indent}return"
                    : $"{indent}return {RenderExpression(returnStatement.Expression)}");
                break;
            case IrBreakStatement:
                writer.WriteLine($"{indent}break");
                break;
            case IrContinueStatement:
                writer.WriteLine($"{indent}continue");
                break;
        }
    }
}
