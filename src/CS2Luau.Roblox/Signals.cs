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

public class RBXScriptSignal<T1, T2, T3>
{
    public RBXScriptConnection Connect(Action<T1, T2, T3> callback) => throw new NotSupportedException("Roblox shim methods are compile-time only.");
}

public class RBXScriptSignal<T1, T2, T3, T4>
{
    public RBXScriptConnection Connect(Action<T1, T2, T3, T4> callback) => throw new NotSupportedException("Roblox shim methods are compile-time only.");
}

public class RBXScriptSignal<T1, T2, T3, T4, T5>
{
    public RBXScriptConnection Connect(Action<T1, T2, T3, T4, T5> callback) => throw new NotSupportedException("Roblox shim methods are compile-time only.");
}

public class RBXScriptSignal<T1, T2, T3, T4, T5, T6>
{
    public RBXScriptConnection Connect(Action<T1, T2, T3, T4, T5, T6> callback) => throw new NotSupportedException("Roblox shim methods are compile-time only.");
}

public class RBXScriptSignal<T1, T2, T3, T4, T5, T6, T7>
{
    public RBXScriptConnection Connect(Action<T1, T2, T3, T4, T5, T6, T7> callback) => throw new NotSupportedException("Roblox shim methods are compile-time only.");
}

public class RBXScriptSignal<T1, T2, T3, T4, T5, T6, T7, T8>
{
    public RBXScriptConnection Connect(Action<T1, T2, T3, T4, T5, T6, T7, T8> callback) => throw new NotSupportedException("Roblox shim methods are compile-time only.");
}
