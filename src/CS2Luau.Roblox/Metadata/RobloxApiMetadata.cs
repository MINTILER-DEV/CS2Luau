namespace Roblox.Metadata;

public sealed record RobloxApiTypeDefinition(string Namespace, string Name, string? BaseType);

public interface IRobloxApiMetadataSource
{
    IReadOnlyList<RobloxApiTypeDefinition> GetTypes();
}

public sealed class ManualRobloxApiMetadataSource : IRobloxApiMetadataSource
{
    public IReadOnlyList<RobloxApiTypeDefinition> GetTypes()
    {
        return
        [
            new RobloxApiTypeDefinition("Roblox", "Instance", null),
            new RobloxApiTypeDefinition("Roblox", "DataModel", "Instance"),
            new RobloxApiTypeDefinition("Roblox.Services", "Workspace", "Roblox.Instances.Model"),
            new RobloxApiTypeDefinition("Roblox.Instances", "Part", "Roblox.Instances.BasePart"),
        ];
    }
}
