using Roblox.Datatypes;
using Roblox.Enums;
using Roblox.Signals;

namespace Roblox.Instances;

public class Folder : Roblox.Instance
{
}

public class Model : Roblox.Instance
{
}

public sealed class Player : Roblox.Instance
{
}

public class BasePart : Roblox.Instance
{
    public bool Anchored { get; set; }
    public Vector3 Position { get; set; }
    public CFrame CFrame { get; set; }
    public Material Material { get; set; }
    public PartType Shape { get; set; }
    public RBXScriptSignal<Roblox.Instance> Touched => throw new NotSupportedException("Roblox shim members are compile-time only.");
}

public sealed class Part : BasePart
{
}

public sealed class RemoteEvent : Roblox.Instance
{
    public void FireServer(params object?[] arguments) => throw new NotSupportedException("Roblox shim methods are compile-time only.");
    public void FireClient(Player player, params object?[] arguments) => throw new NotSupportedException("Roblox shim methods are compile-time only.");
    public void FireAllClients(params object?[] arguments) => throw new NotSupportedException("Roblox shim methods are compile-time only.");
}

public sealed class RemoteFunction : Roblox.Instance
{
    public object? InvokeServer(params object?[] arguments) => throw new NotSupportedException("Roblox shim methods are compile-time only.");
}

public sealed class BindableEvent : Roblox.Instance
{
    public void Fire(params object?[] arguments) => throw new NotSupportedException("Roblox shim methods are compile-time only.");
}

public sealed class BindableFunction : Roblox.Instance
{
    public object? Invoke(params object?[] arguments) => throw new NotSupportedException("Roblox shim methods are compile-time only.");
}
