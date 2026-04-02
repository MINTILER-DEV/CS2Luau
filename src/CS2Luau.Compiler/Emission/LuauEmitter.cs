using System.Xml.Linq;
using CS2Luau.Compiler.Configuration;
using CS2Luau.Compiler.Ir;
using CS2Luau.Compiler.Lowering;
using CS2Luau.Runtime;

namespace CS2Luau.Compiler.Emission;

internal static class LuauEmitter
{
    public static string Emit(IrProgram program, ScriptKind scriptKind)
    {
        var writer = new LuauWriter();
        var returnsExports = scriptKind == ScriptKind.ModuleScript;

        if (returnsExports)
        {
            writer.WriteLine("local exports = {}");
            writer.WriteLine();
        }

        foreach (var @enum in program.Enums)
        {
            writer.WriteLine($"local {@enum.Name} = {{");
            writer.Indent();
            foreach (var member in @enum.Members)
            {
                writer.WriteLine($"{member.Name} = {member.Value},");
            }
            writer.Unindent();
            writer.WriteLine("}");
            if (returnsExports)
            {
                writer.WriteLine($"exports.{@enum.Name} = {@enum.Name}");
            }
            writer.WriteLine();
        }

        foreach (var @class in program.Classes)
        {
            EmitClass(writer, @class, returnsExports);
        }

        foreach (var statement in program.TopLevelStatements)
        {
            EmitStatement(writer, statement);
        }

        if (returnsExports)
        {
            if (program.TopLevelStatements.Count > 0)
            {
                writer.WriteLine();
            }

            writer.WriteLine("return exports");
        }

        return writer.ToString().TrimEnd() + Environment.NewLine;
    }

    private static void EmitClass(LuauWriter writer, IrClassDeclaration @class, bool returnsExports)
    {
        writer.WriteLine($"local {@class.Name} = {{}}");
        writer.WriteLine($"{@class.Name}.__index = {@class.Name}");
        if (!string.IsNullOrWhiteSpace(@class.BaseTypeName))
        {
            writer.WriteLine($"setmetatable({@class.Name}, {{ __index = {@class.BaseTypeName} }})");
        }
        writer.WriteLine();

        foreach (var initializer in @class.Initializers.Where(initializer => initializer.IsStatic && initializer.Initializer is not null))
        {
            writer.WriteLine($"{@class.Name}.{initializer.Name} = {EmitExpression(initializer.Initializer!)}");
        }

        if (@class.Initializers.Any(initializer => initializer.IsStatic && initializer.Initializer is not null))
        {
            writer.WriteLine();
        }

        writer.WriteLine($"function {@class.Name}.new({string.Join(", ", @class.Constructor.Parameters)})");
        writer.Indent();
        writer.WriteLine($"local self = setmetatable({{}}, {@class.Name})");
        writer.WriteLine($"{@class.Name}.__init(self{FormatArguments(@class.Constructor.Parameters)})");
        writer.WriteLine("return self");
        writer.Unindent();
        writer.WriteLine("end");
        writer.WriteLine();

        writer.WriteLine($"function {@class.Name}.__init(self{FormatArguments(@class.Constructor.Parameters)})");
        writer.Indent();
        if (!string.IsNullOrWhiteSpace(@class.BaseTypeName))
        {
            var baseArgs = @class.Constructor.BaseArguments.Select(EmitExpression).ToArray();
            writer.WriteLine($"{@class.BaseTypeName}.__init(self{FormatArguments(baseArgs)})");
        }

        foreach (var statement in @class.Constructor.Body)
        {
            EmitStatement(writer, statement);
        }

        writer.Unindent();
        writer.WriteLine("end");
        writer.WriteLine();

        foreach (var method in @class.Methods)
        {
            var prefix = method.IsStatic ? $"{@class.Name}.{method.Name}" : $"{@class.Name}:{method.Name}";
            writer.WriteLine($"function {prefix}({string.Join(", ", method.Parameters)})");
            writer.Indent();
            foreach (var statement in method.Body)
            {
                EmitStatement(writer, statement);
            }

            writer.Unindent();
            writer.WriteLine("end");
            writer.WriteLine();
        }

        if (returnsExports)
        {
            writer.WriteLine($"exports.{@class.Name} = {@class.Name}");
            writer.WriteLine();
        }
    }

    private static void EmitStatement(LuauWriter writer, IrStatement statement)
    {
        switch (statement)
        {
            case IrBlockStatement block:
                foreach (var child in block.Statements)
                {
                    EmitStatement(writer, child);
                }
                break;
            case IrLocalDeclarationStatement localDeclaration:
                writer.WriteLine(localDeclaration.Initializer is null
                    ? $"local {localDeclaration.Name}"
                    : $"local {localDeclaration.Name} = {EmitExpression(localDeclaration.Initializer)}");
                break;
            case IrAssignmentStatement assignment:
                writer.WriteLine($"{assignment.Target} = {EmitExpression(assignment.Value)}");
                break;
            case IrExpressionStatement expressionStatement:
                writer.WriteLine(EmitExpression(expressionStatement.Expression));
                break;
            case IrIfStatement ifStatement:
                writer.WriteLine($"if {EmitExpression(ifStatement.Condition)} then");
                writer.Indent();
                foreach (var child in ifStatement.ThenStatements)
                {
                    EmitStatement(writer, child);
                }
                writer.Unindent();
                if (ifStatement.ElseStatements is not null && ifStatement.ElseStatements.Count > 0)
                {
                    writer.WriteLine("else");
                    writer.Indent();
                    foreach (var child in ifStatement.ElseStatements)
                    {
                        EmitStatement(writer, child);
                    }
                    writer.Unindent();
                }
                writer.WriteLine("end");
                break;
            case IrWhileStatement whileStatement:
                writer.WriteLine($"while {EmitExpression(whileStatement.Condition)} do");
                writer.Indent();
                foreach (var child in whileStatement.Body)
                {
                    EmitStatement(writer, child);
                }
                writer.Unindent();
                writer.WriteLine("end");
                break;
            case IrRepeatStatement repeatStatement:
                writer.WriteLine("repeat");
                writer.Indent();
                foreach (var child in repeatStatement.Body)
                {
                    EmitStatement(writer, child);
                }
                writer.Unindent();
                writer.WriteLine($"until not ({EmitExpression(repeatStatement.Condition)})");
                break;
            case IrForEachStatement forEachStatement:
                writer.WriteLine($"for _, {forEachStatement.Identifier} in ipairs({EmitExpression(forEachStatement.Iterable)}) do");
                writer.Indent();
                foreach (var child in forEachStatement.Body)
                {
                    EmitStatement(writer, child);
                }
                writer.Unindent();
                writer.WriteLine("end");
                break;
            case IrReturnStatement returnStatement:
                writer.WriteLine(returnStatement.Expression is null ? "return" : $"return {EmitExpression(returnStatement.Expression)}");
                break;
            case IrBreakStatement:
                writer.WriteLine("break");
                break;
            case IrContinueStatement:
                writer.WriteLine("continue");
                break;
        }
    }

    private static string EmitExpression(IrExpression expression)
    {
        return expression switch
        {
            IrRawExpression raw => raw.Code,
            IrLambdaExpression lambda => EmitLambda(lambda),
            _ => "nil",
        };
    }

    private static string EmitLambda(IrLambdaExpression lambda)
    {
        var writer = new LuauWriter();
        writer.WriteLine($"function({string.Join(", ", lambda.Parameters)})");
        writer.Indent();
        foreach (var statement in lambda.Body)
        {
            EmitStatement(writer, statement);
        }
        writer.Unindent();
        writer.Write("end");
        return writer.ToString().TrimEnd();
    }

    private static string FormatArguments(IEnumerable<string> arguments)
    {
        var materialized = arguments.Where(argument => !string.IsNullOrWhiteSpace(argument)).ToArray();
        return materialized.Length == 0 ? string.Empty : ", " + string.Join(", ", materialized);
    }
}

internal static class RbxmxSerializer
{
    public static string Serialize(string name, ScriptKind scriptKind, string source, IReadOnlyList<RuntimeAsset> runtimeAssets)
    {
        var scriptClassName = scriptKind switch
        {
            ScriptKind.Script => "Script",
            ScriptKind.LocalScript => "LocalScript",
            _ => "ModuleScript",
        };

        var identifier = 0;
        var document = new XDocument(
            new XElement("roblox",
                new XAttribute("version", "4"),
                new XElement("Meta", new XAttribute("name", "ExplicitAutoJoints"), "true"),
                CreateItem("Model", name, null, ref identifier,
                    [
                        CreateItem(scriptClassName, name, source, ref identifier),
                        ..runtimeAssets.Select(asset => CreateItem("ModuleScript", Path.GetFileNameWithoutExtension(asset.RelativePath), asset.Content, ref identifier)),
                    ])));

        return document.ToString();
    }

    private static XElement CreateItem(string className, string name, string? source, ref int identifier, IEnumerable<XElement>? children = null)
    {
        var element = new XElement("Item",
            new XAttribute("class", className),
            new XAttribute("referent", $"RBX{identifier++}"),
            new XElement("Properties",
                new XElement("string", new XAttribute("name", "Name"), name)));

        if (source is not null)
        {
            element.Element("Properties")!.Add(new XElement("ProtectedString", new XAttribute("name", "Source"), source));
        }

        if (children is not null)
        {
            foreach (var child in children)
            {
                element.Add(child);
            }
        }

        return element;
    }
}

internal sealed class LuauWriter
{
    private readonly StringWriter writer = new();
    private int indentLevel;

    public void Indent() => indentLevel++;
    public void Unindent() => indentLevel = Math.Max(0, indentLevel - 1);

    public void WriteLine(string text = "")
    {
        if (text.Length > 0)
        {
            writer.Write(new string(' ', indentLevel * 4));
        }

        writer.WriteLine(text);
    }

    public void Write(string text)
    {
        if (text.Length > 0)
        {
            writer.Write(new string(' ', indentLevel * 4));
        }

        writer.Write(text);
    }

    public override string ToString() => writer.ToString();
}
