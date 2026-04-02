namespace CS2Luau.Runtime;

public sealed record RuntimeAsset(string RelativePath, string Content);

public static class RuntimeAssetCatalog
{
    private static readonly IReadOnlyList<RuntimeAsset> Assets =
    [
        new RuntimeAsset("runtime/Class.luau", ClassModule),
        new RuntimeAsset("runtime/Collections.luau", CollectionsModule),
    ];

    public static IReadOnlyList<RuntimeAsset> GetAssets()
    {
        return Assets;
    }

    private const string ClassModule =
        """
        local Class = {}

        function Class.extend(base)
            local derived = {}
            derived.__index = derived
            setmetatable(derived, base)
            return derived
        end

        return Class
        """;

    private const string CollectionsModule =
        """
        local Collections = {}

        function Collections.clone(list)
            local copy = {}
            for index, value in ipairs(list) do
                copy[index] = value
            end
            return copy
        end

        return Collections
        """;
}
