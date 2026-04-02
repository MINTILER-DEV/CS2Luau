using Roblox.Instances;
using Roblox.Services;

namespace Roblox;

public static class Globals
{
    public static DataModel game => throw new NotSupportedException("Roblox shim members are compile-time only.");
    public static Workspace workspace => throw new NotSupportedException("Roblox shim members are compile-time only.");
    public static Instance script => throw new NotSupportedException("Roblox shim members are compile-time only.");

    public static void print(params object?[] values) => throw new NotSupportedException("Roblox shim methods are compile-time only.");
    public static void warn(params object?[] values) => throw new NotSupportedException("Roblox shim methods are compile-time only.");
}
