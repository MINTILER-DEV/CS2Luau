using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CS2Luau.Roblox.Generator;

internal static class ApiDumpReader
{
    public static ApiDumpModel Read(string apiDumpPath)
    {
        if (!File.Exists(apiDumpPath))
        {
            throw new FileNotFoundException("Could not find the Roblox API dump.", apiDumpPath);
        }

        using var stream = File.OpenRead(apiDumpPath);
        using var textReader = new StreamReader(stream);
        using var reader = new JsonTextReader(textReader)
        {
            CloseInput = true,
        };

        var classes = new List<ApiClassDefinition>();
        var enums = new List<ApiEnumDefinition>();

        Require(reader.Read(), "Expected the JSON document to start with an object.");
        Require(reader.TokenType == JsonToken.StartObject, "Expected the API dump root to be a JSON object.");

        while (reader.Read())
        {
            if (reader.TokenType == JsonToken.EndObject)
            {
                break;
            }

            if (reader.TokenType != JsonToken.PropertyName)
            {
                continue;
            }

            var propertyName = (string?)reader.Value;
            switch (propertyName)
            {
                case "Classes":
                    ReadClasses(reader, classes);
                    break;
                case "Enums":
                    ReadEnums(reader, enums);
                    break;
                default:
                    reader.Read();
                    reader.Skip();
                    break;
            }
        }

        return new ApiDumpModel(classes, enums);
    }

    private static void ReadClasses(JsonTextReader reader, List<ApiClassDefinition> classes)
    {
        Require(reader.Read(), "Expected a classes array.");
        Require(reader.TokenType == JsonToken.StartArray, "Expected 'Classes' to be an array.");

        while (reader.Read())
        {
            if (reader.TokenType == JsonToken.EndArray)
            {
                break;
            }

            var classObject = JObject.Load(reader);
            classes.Add(ParseClass(classObject));
        }
    }

    private static void ReadEnums(JsonTextReader reader, List<ApiEnumDefinition> enums)
    {
        Require(reader.Read(), "Expected an enums array.");
        Require(reader.TokenType == JsonToken.StartArray, "Expected 'Enums' to be an array.");

        while (reader.Read())
        {
            if (reader.TokenType == JsonToken.EndArray)
            {
                break;
            }

            var enumObject = JObject.Load(reader);
            enums.Add(ParseEnum(enumObject));
        }
    }

    private static ApiClassDefinition ParseClass(JObject classObject)
    {
        var members = classObject["Members"] is JArray memberArray
            ? memberArray.OfType<JObject>().Select(ParseMember).ToArray()
            : [];
        var tags = ReadTags(classObject["Tags"]);

        return new ApiClassDefinition(
            classObject.Value<string>("Name") ?? throw new InvalidOperationException("Encountered a class without a name."),
            classObject.Value<string>("Superclass"),
            tags,
            members);
    }

    private static ApiMemberDefinition ParseMember(JObject memberObject)
    {
        var memberType = memberObject.Value<string>("MemberType") ?? string.Empty;
        ApiTypeReference? type = memberType switch
        {
            "Property" => ParseType(memberObject["ValueType"]),
            "Function" => ParseType(memberObject["ReturnType"]),
            "YieldFunction" => ParseType(memberObject["ReturnType"]),
            "Event" => null,
            "Callback" => ParseType(memberObject["ReturnType"]),
            _ => null,
        };

        var parameters = memberObject["Parameters"] is JArray parameterArray
            ? parameterArray.OfType<JObject>().Select(ParseParameter).ToArray()
            : [];

        return new ApiMemberDefinition(
            memberType,
            memberObject.Value<string>("Name") ?? throw new InvalidOperationException("Encountered a member without a name."),
            type,
            parameters,
            ReadTags(memberObject["Tags"]),
            ReadSecurity(memberObject["Security"]));
    }

    private static ApiParameterDefinition ParseParameter(JObject parameterObject)
    {
        return new ApiParameterDefinition(
            parameterObject.Value<string>("Name") ?? "value",
            ParseType(parameterObject["Type"]));
    }

    private static ApiEnumDefinition ParseEnum(JObject enumObject)
    {
        var items = enumObject["Items"] is JArray itemArray
            ? itemArray.OfType<JObject>().Select(item => new ApiEnumItemDefinition(
                item.Value<string>("Name") ?? "Unknown",
                item.Value<long?>("Value") ?? 0L)).ToArray()
            : [];

        return new ApiEnumDefinition(
            enumObject.Value<string>("Name") ?? throw new InvalidOperationException("Encountered an enum without a name."),
            items);
    }

    private static ApiTypeReference? ParseType(JToken? token)
    {
        if (token is not JObject typeObject)
        {
            return null;
        }

        return new ApiTypeReference(
            typeObject.Value<string>("Category") ?? "Unknown",
            typeObject.Value<string>("Name"),
            ParseType(typeObject["ValueType"] ?? typeObject["Type"]),
            ParseType(typeObject["KeyType"]),
            ParseType(typeObject["ItemType"] ?? typeObject["ItemsType"] ?? typeObject["TupleType"]));
    }

    private static IReadOnlySet<string> ReadTags(JToken? token)
    {
        return token is JArray tagArray
            ? new HashSet<string>(tagArray.Values<string>().Where(value => !string.IsNullOrWhiteSpace(value))!, StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
    }

    private static IReadOnlySet<string> ReadSecurity(JToken? token)
    {
        var values = new HashSet<string>(StringComparer.Ordinal);
        switch (token)
        {
            case JValue value when value.Type == JTokenType.String:
                AddSecurityValue(values, value.Value<string>());
                break;
            case JObject securityObject:
                AddSecurityValue(values, securityObject.Value<string>("Read"));
                AddSecurityValue(values, securityObject.Value<string>("Write"));
                break;
        }

        return values;
    }

    private static void AddSecurityValue(ISet<string> values, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            values.Add(value);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
