using System.Text;

namespace CS2Luau.Roblox.Generator;

internal static class RobloxCodeGenerator
{
    private static readonly HashSet<string> RootTypes = new(StringComparer.Ordinal)
    {
        "Object",
        "Instance",
        "DataModel",
    };

    private static readonly HashSet<string> ManualDatatypes = new(StringComparer.Ordinal)
    {
        "Vector2",
        "Vector3",
        "CFrame",
        "Color3",
        "UDim",
        "UDim2",
    };

    private static readonly HashSet<string> ReservedDatatypes = new(StringComparer.Ordinal)
    {
        "RBXScriptSignal",
        "RBXScriptConnection",
    };

    private static readonly HashSet<string> IgnoredMembers = new(StringComparer.Ordinal)
    {
        "ClassName",
        "className",
    };

    private static readonly Dictionary<string, string> PrimitiveTypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["bool"] = "bool",
        ["boolean"] = "bool",
        ["string"] = "string",
        ["int"] = "int",
        ["int32"] = "int",
        ["int64"] = "long",
        ["int16"] = "short",
        ["int8"] = "sbyte",
        ["float"] = "float",
        ["double"] = "double",
        ["number"] = "double",
        ["token"] = "string",
        ["content"] = "string",
        ["binarystring"] = "byte[]",
        ["sharedstring"] = "string",
        ["uniqueid"] = "string",
        ["qdir"] = "string",
        ["protectedstring"] = "string",
        ["objects"] = "Roblox.Instance[]",
        ["dictionary"] = "Dictionary<string, object?>",
        ["map"] = "Dictionary<string, object?>",
        ["variant"] = "object?",
        ["tuple"] = "object?",
        ["null"] = "void",
    };

    public static void Generate(ApiDumpModel dump, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        var classLookup = dump.Classes.ToDictionary(definition => definition.Name, StringComparer.Ordinal);
        var enumNames = dump.Enums.Select(definition => SanitizeTypeName(definition.Name)).ToHashSet(StringComparer.Ordinal);
        var datatypes = CollectReferencedDatatypes(dump);

        File.WriteAllText(Path.Combine(outputDirectory, "Core.g.cs"), GenerateClassesFile(
            namespaceName: "Roblox",
            classes: dump.Classes.Where(definition => RootTypes.Contains(definition.Name)).ToArray(),
            classLookup: classLookup,
            enumNames: enumNames));

        File.WriteAllText(Path.Combine(outputDirectory, "Instances.g.cs"), GenerateClassesFile(
            namespaceName: "Roblox.Instances",
            classes: dump.Classes.Where(definition => !RootTypes.Contains(definition.Name) && !definition.Tags.Contains("Service")).ToArray(),
            classLookup: classLookup,
            enumNames: enumNames));

        File.WriteAllText(Path.Combine(outputDirectory, "Services.g.cs"), GenerateClassesFile(
            namespaceName: "Roblox.Services",
            classes: dump.Classes.Where(definition => !RootTypes.Contains(definition.Name) && definition.Tags.Contains("Service")).ToArray(),
            classLookup: classLookup,
            enumNames: enumNames));

        File.WriteAllText(Path.Combine(outputDirectory, "Enums.g.cs"), GenerateEnumsFile(dump.Enums));
        File.WriteAllText(Path.Combine(outputDirectory, "Enum.g.cs"), GenerateEnumWrapperFile(dump.Enums));
        File.WriteAllText(Path.Combine(outputDirectory, "Datatypes.g.cs"), GenerateDatatypesFile(datatypes));
    }

    private static IReadOnlyList<string> CollectReferencedDatatypes(ApiDumpModel dump)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var definition in dump.Classes)
        {
            foreach (var member in definition.Members)
            {
                CollectTypeNames(member.Type, names);
                foreach (var parameter in member.Parameters)
                {
                    CollectTypeNames(parameter.Type, names);
                }
            }
        }

        foreach (var name in ManualDatatypes)
        {
            names.Remove(name);
        }

        foreach (var name in ReservedDatatypes)
        {
            names.Remove(name);
        }

        return names.OrderBy(name => name, StringComparer.Ordinal).ToArray();
    }

    private static void CollectTypeNames(ApiTypeReference? type, ISet<string> names)
    {
        if (type is null)
        {
            return;
        }

        if (string.Equals(type.Category, "DataType", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(type.Name))
        {
            names.Add(type.Name);
        }

        CollectTypeNames(type.ValueType, names);
        CollectTypeNames(type.KeyType, names);
        CollectTypeNames(type.ItemType, names);
    }

    private static string GenerateClassesFile(
        string namespaceName,
        IReadOnlyList<ApiClassDefinition> classes,
        IReadOnlyDictionary<string, ApiClassDefinition> classLookup,
        IReadOnlySet<string> enumNames)
    {
        var writer = new CodeWriter();
        writer.WriteLine("// <auto-generated />");
        writer.WriteLine("#nullable enable");
        writer.WriteLine("using System.Collections.Generic;");
        writer.WriteLine("using Roblox;");
        writer.WriteLine("using Roblox.Datatypes;");
        writer.WriteLine("using Roblox.Enums;");
        writer.WriteLine("using Roblox.Signals;");
        writer.WriteLine();
        writer.WriteLine($"namespace {namespaceName};");
        writer.WriteLine();

        foreach (var definition in classes.OrderBy(definition => definition.Name, StringComparer.Ordinal))
        {
            var sanitizedClassName = SanitizeTypeName(definition.Name);
            var baseType = ResolveBaseType(definition.Superclass, classLookup);
            writer.WriteLine($"public partial class {sanitizedClassName} : {baseType}");
            writer.WriteLine("{");
            writer.Indent();

            foreach (var member in FilterMembers(definition.Members))
            {
                var memberName = SanitizeMemberName(member.Name, sanitizedClassName);
                switch (member.MemberType)
                {
                    case "Property":
                        writer.WriteLine($"public {ResolveType(member.Type, classLookup, enumNames, isReturnType: false)} {memberName} {{ get; set; }}");
                        break;
                    case "Function":
                    case "YieldFunction":
                    case "Callback":
                        writer.WriteLine($"public {ResolveType(member.Type, classLookup, enumNames, isReturnType: true)} {memberName}({FormatParameters(member.Parameters, classLookup, enumNames)}) => throw new NotSupportedException(\"Roblox shim methods are compile-time only.\");");
                        break;
                    case "Event":
                        writer.WriteLine($"public {ResolveSignalType(member.Parameters, classLookup, enumNames)} {memberName} => throw new NotSupportedException(\"Roblox shim members are compile-time only.\");");
                        break;
                }
            }

            writer.Unindent();
            writer.WriteLine("}");
            writer.WriteLine();
        }

        return writer.ToString();
    }

    private static string GenerateEnumsFile(IReadOnlyList<ApiEnumDefinition> enums)
    {
        var writer = new CodeWriter();
        writer.WriteLine("// <auto-generated />");
        writer.WriteLine("#nullable enable");
        writer.WriteLine();
        writer.WriteLine("namespace Roblox.Enums;");
        writer.WriteLine();

        foreach (var definition in enums.OrderBy(definition => definition.Name, StringComparer.Ordinal))
        {
            writer.WriteLine($"public enum {SanitizeTypeName(definition.Name)}");
            writer.WriteLine("{");
            writer.Indent();
            foreach (var item in definition.Items)
            {
                writer.WriteLine($"{SanitizeMemberName(item.Name, SanitizeTypeName(definition.Name))} = {item.Value},");
            }
            writer.Unindent();
            writer.WriteLine("}");
            writer.WriteLine();
        }

        return writer.ToString();
    }

    private static string GenerateDatatypesFile(IReadOnlyList<string> datatypes)
    {
        var writer = new CodeWriter();
        writer.WriteLine("// <auto-generated />");
        writer.WriteLine("#nullable enable");
        writer.WriteLine();
        writer.WriteLine("namespace Roblox.Datatypes;");
        writer.WriteLine();

        foreach (var datatype in datatypes)
        {
            writer.WriteLine($"public readonly record struct {SanitizeTypeName(datatype)};");
        }

        return writer.ToString();
    }

    private static string GenerateEnumWrapperFile(IReadOnlyList<ApiEnumDefinition> enums)
    {
        var writer = new CodeWriter();
        writer.WriteLine("// <auto-generated />");
        writer.WriteLine("#nullable enable");
        writer.WriteLine();
        writer.WriteLine("namespace Roblox;");
        writer.WriteLine();
        writer.WriteLine("public static class Enum");
        writer.WriteLine("{");
        writer.Indent();

        foreach (var definition in enums.OrderBy(definition => definition.Name, StringComparer.Ordinal))
        {
            var enumName = SanitizeTypeName(definition.Name);
            writer.WriteLine($"public static class {enumName}");
            writer.WriteLine("{");
            writer.Indent();
            foreach (var item in definition.Items)
            {
                var memberName = SanitizeMemberName(item.Name, enumName);
                writer.WriteLine($"public static readonly global::Roblox.Enums.{enumName} {memberName} = global::Roblox.Enums.{enumName}.{memberName};");
            }
            writer.Unindent();
            writer.WriteLine("}");
            writer.WriteLine();
        }

        writer.Unindent();
        writer.WriteLine("}");
        return writer.ToString();
    }

    private static IReadOnlyList<ApiMemberDefinition> FilterMembers(IReadOnlyList<ApiMemberDefinition> members)
    {
        var grouped = new Dictionary<string, ApiMemberDefinition>(StringComparer.Ordinal);
        foreach (var member in members)
        {
            if (IgnoredMembers.Contains(member.Name) ||
                member.Tags.Contains("Deprecated") ||
                string.IsNullOrWhiteSpace(member.MemberType))
            {
                continue;
            }

            var key = $"{member.MemberType}:{member.Name}";
            if (!grouped.ContainsKey(key))
            {
                grouped[key] = member;
            }
        }

        return grouped.Values.OrderBy(member => member.Name, StringComparer.Ordinal).ThenBy(member => member.MemberType, StringComparer.Ordinal).ToArray();
    }

    private static string ResolveBaseType(string? superclass, IReadOnlyDictionary<string, ApiClassDefinition> classLookup)
    {
        if (string.IsNullOrWhiteSpace(superclass) || string.Equals(superclass, "<<<ROOT>>>", StringComparison.Ordinal))
        {
            return "object";
        }

        if (string.Equals(superclass, "Instance", StringComparison.Ordinal) ||
            string.Equals(superclass, "DataModel", StringComparison.Ordinal) ||
            string.Equals(superclass, "Object", StringComparison.Ordinal))
        {
            return SanitizeTypeName(superclass);
        }

        if (classLookup.TryGetValue(superclass, out var baseDefinition))
        {
            var baseName = SanitizeTypeName(superclass);
            return baseDefinition.Tags.Contains("Service")
                ? $"Roblox.Services.{baseName}"
                : $"Roblox.Instances.{baseName}";
        }

        return "object";
    }

    private static string ResolveType(ApiTypeReference? type, IReadOnlyDictionary<string, ApiClassDefinition> classLookup, IReadOnlySet<string> enumNames, bool isReturnType)
    {
        if (type is null)
        {
            return isReturnType ? "void" : "object?";
        }

        if (string.Equals(type.Category, "Primitive", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(type.Name) &&
            PrimitiveTypeMap.TryGetValue(type.Name, out var mappedPrimitive))
        {
            var primitive = mappedPrimitive == "void" && !isReturnType ? "object?" : mappedPrimitive;
            return ApplyNullableReferenceType(primitive);
        }

        if (string.Equals(type.Category, "Enum", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(type.Name))
        {
            var enumName = SanitizeTypeName(type.Name);
            var enumType = enumNames.Contains(enumName) ? $"Roblox.Enums.{enumName}" : "int";
            return ApplyNullableReferenceType(enumType);
        }

        if (string.Equals(type.Category, "DataType", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(type.Name))
        {
            if (ReservedDatatypes.Contains(type.Name))
            {
                return $"Roblox.Signals.{SanitizeTypeName(type.Name)}";
            }

            return ApplyNullableReferenceType($"Roblox.Datatypes.{SanitizeTypeName(type.Name)}");
        }

        if ((string.Equals(type.Category, "Class", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(type.Category, "Instance", StringComparison.OrdinalIgnoreCase)) &&
            !string.IsNullOrWhiteSpace(type.Name))
        {
            if (string.Equals(type.Name, "Instance", StringComparison.Ordinal) ||
                string.Equals(type.Name, "DataModel", StringComparison.Ordinal) ||
                string.Equals(type.Name, "Object", StringComparison.Ordinal))
            {
                return ApplyNullableReferenceType(SanitizeTypeName(type.Name));
            }

            if (classLookup.TryGetValue(type.Name, out var classDefinition))
            {
                var name = SanitizeTypeName(type.Name);
                var resolvedClass = classDefinition.Tags.Contains("Service")
                    ? $"Roblox.Services.{name}"
                    : $"Roblox.Instances.{name}";
                return ApplyNullableReferenceType(resolvedClass);
            }
        }

        if (string.Equals(type.Category, "Array", StringComparison.OrdinalIgnoreCase))
        {
            return ApplyNullableReferenceType($"{ResolveType(type.ValueType ?? type.ItemType, classLookup, enumNames, isReturnType: false)}[]");
        }

        if (string.Equals(type.Category, "Map", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(type.Category, "Dictionary", StringComparison.OrdinalIgnoreCase))
        {
            return "Dictionary<string, object?>?";
        }

        if (string.Equals(type.Category, "Tuple", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(type.Category, "Group", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(type.Category, "Variant", StringComparison.OrdinalIgnoreCase))
        {
            return "object?";
        }

        if (!string.IsNullOrWhiteSpace(type.Name) && PrimitiveTypeMap.TryGetValue(type.Name, out var mappedByName))
        {
            return ApplyNullableReferenceType(mappedByName == "void" && !isReturnType ? "object?" : mappedByName);
        }

        return isReturnType ? "object?" : "object?";
    }

    private static string ResolveSignalType(IReadOnlyList<ApiParameterDefinition> parameters, IReadOnlyDictionary<string, ApiClassDefinition> classLookup, IReadOnlySet<string> enumNames)
    {
        if (parameters.Count == 0)
        {
            return "Roblox.Signals.RBXScriptSignal";
        }

        if (parameters.Count > 8)
        {
            return "Roblox.Signals.RBXScriptSignal";
        }

        var parameterTypes = parameters
            .Select(parameter => ResolveType(parameter.Type, classLookup, enumNames, isReturnType: false))
            .ToArray();
        return $"Roblox.Signals.RBXScriptSignal<{string.Join(", ", parameterTypes)}>";
    }

    private static string FormatParameters(IReadOnlyList<ApiParameterDefinition> parameters, IReadOnlyDictionary<string, ApiClassDefinition> classLookup, IReadOnlySet<string> enumNames)
    {
        if (parameters.Count == 0)
        {
            return string.Empty;
        }

        var usedNames = new HashSet<string>(StringComparer.Ordinal);
        var builder = new StringBuilder();
        for (var index = 0; index < parameters.Count; index++)
        {
            if (index > 0)
            {
                builder.Append(", ");
            }

            var parameter = parameters[index];
            var name = SanitizeParameterName(parameter.Name, usedNames);
            var type = ResolveType(parameter.Type, classLookup, enumNames, isReturnType: false);
            builder.Append(type);
            builder.Append(' ');
            builder.Append(name);
        }

        return builder.ToString();
    }

    private static string SanitizeTypeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "UnknownType";
        }

        var builder = new StringBuilder(name.Length);
        foreach (var character in name)
        {
            builder.Append(char.IsLetterOrDigit(character) || character == '_' ? character : '_');
        }

        var candidate = builder.ToString();
        return IsKeyword(candidate) ? "@" + candidate : candidate;
    }

    private static string SanitizeMemberName(string name, string containingTypeName)
    {
        var candidate = SanitizeTypeName(name);
        if (string.Equals(candidate, containingTypeName, StringComparison.Ordinal))
        {
            return candidate + "Member";
        }

        return candidate;
    }

    private static string SanitizeParameterName(string name, ISet<string> usedNames)
    {
        var candidate = string.IsNullOrWhiteSpace(name) ? "value" : SanitizeTypeName(ToCamelCase(name));
        if (string.Equals(candidate, "event", StringComparison.OrdinalIgnoreCase))
        {
            candidate = "@event";
        }

        var baseName = candidate;
        var suffix = 1;
        while (!usedNames.Add(candidate))
        {
            candidate = $"{baseName}{suffix++}";
        }

        return candidate;
    }

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "value";
        }

        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    private static bool IsKeyword(string value)
    {
        return value is
            "abstract" or "as" or "base" or "bool" or "break" or "byte" or "case" or "catch" or "char" or
            "checked" or "class" or "const" or "continue" or "decimal" or "default" or "delegate" or "do" or
            "double" or "else" or "enum" or "event" or "explicit" or "extern" or "false" or "finally" or
            "fixed" or "float" or "for" or "foreach" or "goto" or "if" or "implicit" or "in" or "int" or
            "interface" or "internal" or "is" or "lock" or "long" or "namespace" or "new" or "null" or
            "object" or "operator" or "out" or "override" or "params" or "private" or "protected" or
            "public" or "readonly" or "ref" or "return" or "sbyte" or "sealed" or "short" or "sizeof" or
            "stackalloc" or "static" or "string" or "struct" or "switch" or "this" or "throw" or "true" or
            "try" or "typeof" or "uint" or "ulong" or "unchecked" or "unsafe" or "ushort" or "using" or
            "virtual" or "void" or "volatile" or "while";
    }

    private static string ApplyNullableReferenceType(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName) ||
            typeName.EndsWith("?", StringComparison.Ordinal) ||
            typeName == "void" ||
            typeName == "bool" ||
            typeName == "byte" ||
            typeName == "sbyte" ||
            typeName == "short" ||
            typeName == "ushort" ||
            typeName == "int" ||
            typeName == "uint" ||
            typeName == "long" ||
            typeName == "ulong" ||
            typeName == "float" ||
            typeName == "double" ||
            typeName.StartsWith("Roblox.Enums.", StringComparison.Ordinal) ||
            typeName.StartsWith("Roblox.Datatypes.", StringComparison.Ordinal))
        {
            return typeName;
        }

        return typeName + "?";
    }
}

internal sealed class CodeWriter
{
    private readonly StringBuilder builder = new();
    private int indentLevel;

    public void Indent() => indentLevel++;
    public void Unindent() => indentLevel = Math.Max(0, indentLevel - 1);

    public void WriteLine(string line = "")
    {
        if (line.Length > 0)
        {
            builder.Append(' ', indentLevel * 4);
        }

        builder.AppendLine(line);
    }

    public override string ToString() => builder.ToString();
}
