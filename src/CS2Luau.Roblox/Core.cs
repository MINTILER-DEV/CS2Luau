using Roblox.Services;

namespace Roblox;

public partial class Instance
{
    public static T New<T>() where T : Instance => throw new NotSupportedException("Roblox shim methods are compile-time only.");
    public static Instance New(string className) => throw new NotSupportedException("Roblox shim methods are compile-time only.");

    public T? FindFirstChild<T>(string name) where T : Instance => throw new NotSupportedException("Roblox shim methods are compile-time only.");
    public T? FindFirstChildOfClass<T>(string className) where T : Instance => throw new NotSupportedException("Roblox shim methods are compile-time only.");
    public Instance? FindFirstChildWhichIsA(string className) => throw new NotSupportedException("Roblox shim methods are compile-time only.");
    public T? FindFirstChildWhichIsA<T>(string className, bool recursive = false) where T : Instance => throw new NotSupportedException("Roblox shim methods are compile-time only.");
    public Instance? WaitForChild(string childName) => throw new NotSupportedException("Roblox shim methods are compile-time only.");
}

public partial class DataModel
{
    public Players Players => throw new NotSupportedException("Roblox shim members are compile-time only.");
    public ReplicatedStorage ReplicatedStorage => throw new NotSupportedException("Roblox shim members are compile-time only.");

    public T GetService<T>() where T : Instance => throw new NotSupportedException("Roblox shim methods are compile-time only.");
}
