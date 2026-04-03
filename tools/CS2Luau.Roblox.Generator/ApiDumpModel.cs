namespace CS2Luau.Roblox.Generator;

internal sealed record ApiDumpModel(
    IReadOnlyList<ApiClassDefinition> Classes,
    IReadOnlyList<ApiEnumDefinition> Enums);

internal sealed record ApiClassDefinition(
    string Name,
    string? Superclass,
    IReadOnlySet<string> Tags,
    IReadOnlyList<ApiMemberDefinition> Members);

internal sealed record ApiMemberDefinition(
    string MemberType,
    string Name,
    ApiTypeReference? Type,
    IReadOnlyList<ApiParameterDefinition> Parameters,
    IReadOnlySet<string> Tags);

internal sealed record ApiParameterDefinition(
    string Name,
    ApiTypeReference? Type);

internal sealed record ApiTypeReference(
    string Category,
    string? Name,
    ApiTypeReference? ValueType = null,
    ApiTypeReference? KeyType = null,
    ApiTypeReference? ItemType = null);

internal sealed record ApiEnumDefinition(
    string Name,
    IReadOnlyList<ApiEnumItemDefinition> Items);

internal sealed record ApiEnumItemDefinition(string Name, long Value);
