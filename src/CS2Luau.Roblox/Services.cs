using Roblox.Instances;
using Roblox.Signals;

namespace Roblox.Services;

public sealed class Players : Roblox.Instance
{
    public Player LocalPlayer => throw new NotSupportedException("Roblox shim members are compile-time only.");
    public RBXScriptSignal<Player> PlayerAdded => throw new NotSupportedException("Roblox shim members are compile-time only.");
}

public sealed class ReplicatedStorage : Roblox.Instance
{
}

public sealed class RunService : Roblox.Instance
{
    public RBXScriptSignal Heartbeat => throw new NotSupportedException("Roblox shim members are compile-time only.");
}

public sealed class Workspace : Model
{
}
