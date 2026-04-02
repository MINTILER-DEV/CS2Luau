using Roblox.Instances;
using Roblox.Services;

namespace Roblox;

public abstract class Instance
{
    public string Name { get; set; } = string.Empty;
    public Instance? Parent { get; set; }

    public static T New<T>() where T : Instance => throw new NotSupportedException("Roblox shim methods are compile-time only.");
    public static Instance New(string className) => throw new NotSupportedException("Roblox shim methods are compile-time only.");

    public void Destroy() => throw new NotSupportedException("Roblox shim methods are compile-time only.");
    public Instance? FindFirstChild(string name) => throw new NotSupportedException("Roblox shim methods are compile-time only.");
    public T? FindFirstChild<T>(string name) where T : Instance => throw new NotSupportedException("Roblox shim methods are compile-time only.");
}

public class DataModel : Instance
{
    public Workspace Workspace => throw new NotSupportedException("Roblox shim members are compile-time only.");
    public Players Players => throw new NotSupportedException("Roblox shim members are compile-time only.");
    public ReplicatedStorage ReplicatedStorage => throw new NotSupportedException("Roblox shim members are compile-time only.");
    public RunService RunService => throw new NotSupportedException("Roblox shim members are compile-time only.");

    public T GetService<T>() where T : Instance => throw new NotSupportedException("Roblox shim methods are compile-time only.");
    public Instance GetService(string className) => throw new NotSupportedException("Roblox shim methods are compile-time only.");
}
