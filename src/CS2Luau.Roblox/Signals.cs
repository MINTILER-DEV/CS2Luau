namespace Roblox.Signals;

public sealed class RBXScriptConnection
{
    public bool Connected { get; set; }
    public void Disconnect() => throw new NotSupportedException("Roblox shim methods are compile-time only.");
}

public class RBXScriptSignal
{
    public RBXScriptConnection Connect(Action callback) => throw new NotSupportedException("Roblox shim methods are compile-time only.");
}

public class RBXScriptSignal<T>
{
    public RBXScriptConnection Connect(Action<T> callback) => throw new NotSupportedException("Roblox shim methods are compile-time only.");
}

public class RBXScriptSignal<T1, T2>
{
    public RBXScriptConnection Connect(Action<T1, T2> callback) => throw new NotSupportedException("Roblox shim methods are compile-time only.");
}
