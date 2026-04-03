using System.Collections.Generic;
using Roblox.Instances;
using Roblox.Services;

namespace Roblox;

public static class Globals
{
    public static DataModel game => throw new NotSupportedException("Roblox shim members are compile-time only.");
    public static Plugin plugin => throw new NotSupportedException("Roblox shim members are compile-time only.");
    public static Dictionary<object, object?> shared => throw new NotSupportedException("Roblox shim members are compile-time only.");
    public static LuaSourceContainer script => throw new NotSupportedException("Roblox shim members are compile-time only.");
    public static Workspace workspace => throw new NotSupportedException("Roblox shim members are compile-time only.");

    public static T assert<T>(T value, string? errorMessage = null) => throw new NotSupportedException("Roblox shim methods are compile-time only.");
    public static object? collectgarbage(string? operation) => throw new NotSupportedException("Roblox shim methods are compile-time only.");
    public static void delay(double delayTime, Action callback) => throw new NotSupportedException("Roblox shim methods are compile-time only.");
    public static DebuggerManager DebuggerManager() => throw new NotSupportedException("Roblox shim methods are compile-time only.");
    public static void error(object? message, double level) => throw new NotSupportedException("Roblox shim methods are compile-time only.");
    [Obsolete("elapsedTime is deprecated in Luau.")]
    public static double elapsedTime() => throw new NotSupportedException("Roblox shim methods are compile-time only.");
    public static double gcinfo() => throw new NotSupportedException("Roblox shim methods are compile-time only.");
    [Obsolete("getfenv is deprecated in Luau.")]
    public static object? getfenv(object? stack) => throw new NotSupportedException("Roblox shim methods are compile-time only.");
    public static object? getmetatable(object? t) => throw new NotSupportedException("Roblox shim methods are compile-time only.");
    public static (object? Iterator, object? Table, double Index) ipairs(object? t) => throw new NotSupportedException("Roblox shim methods are compile-time only.");
    public static object? loadstring(string? contents, string? chunkname) => throw new NotSupportedException("Roblox shim methods are compile-time only.");
    public static object? newproxy(bool addMetatable) => throw new NotSupportedException("Roblox shim methods are compile-time only.");
    public static (object? Key, object? Value) next(object? t, object? lastKey) => throw new NotSupportedException("Roblox shim methods are compile-time only.");
    public static (bool Success, object? Result) pcall(Delegate func, params object?[] args) => throw new NotSupportedException("Roblox shim methods are compile-time only.");
    public static (object? Iterator, object? Table) pairs(object? t) => throw new NotSupportedException("Roblox shim methods are compile-time only.");
    public static PluginManager PluginManager() => throw new NotSupportedException("Roblox shim methods are compile-time only.");
    public static void print(params object?[] values) => throw new NotSupportedException("Roblox shim methods are compile-time only.");
    [Obsolete("printidentity is deprecated in Luau.")]
    public static void printidentity(string? prefix) => throw new NotSupportedException("Roblox shim methods are compile-time only.");
    public static bool rawequal(object? v1, object? v2) => throw new NotSupportedException("Roblox shim methods are compile-time only.");
    public static object? rawget(object? t, object? index) => throw new NotSupportedException("Roblox shim methods are compile-time only.");
    public static double rawlen(object? t) => throw new NotSupportedException("Roblox shim methods are compile-time only.");
    public static object? rawset(object? t, object? index, object? value) => throw new NotSupportedException("Roblox shim methods are compile-time only.");
    public static object? require(ModuleScript module) => throw new NotSupportedException("Roblox shim methods are compile-time only.");
    public static object? require(string modulePath) => throw new NotSupportedException("Roblox shim methods are compile-time only.");
    public static object? require(long moduleAssetId) => throw new NotSupportedException("Roblox shim methods are compile-time only.");
    public static object?[] select(object? index, params object?[] args) => throw new NotSupportedException("Roblox shim methods are compile-time only.");
    public static GlobalSettings settings() => throw new NotSupportedException("Roblox shim methods are compile-time only.");
    [Obsolete("setfenv is deprecated in Luau.")]
    public static object? setfenv(object? f, object? fenv) => throw new NotSupportedException("Roblox shim methods are compile-time only.");
    public static object? setmetatable(object? t, object? newMeta) => throw new NotSupportedException("Roblox shim methods are compile-time only.");
    [Obsolete("spawn is deprecated in Luau.")]
    public static void spawn(Action callback) => throw new NotSupportedException("Roblox shim methods are compile-time only.");
    public static Stats stats() => throw new NotSupportedException("Roblox shim methods are compile-time only.");
    [Obsolete("tick is deprecated in Luau.")]
    public static double tick() => throw new NotSupportedException("Roblox shim methods are compile-time only.");
    public static double time() => throw new NotSupportedException("Roblox shim methods are compile-time only.");
    public static object? tonumber(object? arg, double @base) => throw new NotSupportedException("Roblox shim methods are compile-time only.");
    public static string tostring(object? e) => throw new NotSupportedException("Roblox shim methods are compile-time only.");
    public static string type(object? v) => throw new NotSupportedException("Roblox shim methods are compile-time only.");
    public static string @typeof(object? value) => throw new NotSupportedException("Roblox shim methods are compile-time only.");
    public static object? unpack(object? list, double i, double j) => throw new NotSupportedException("Roblox shim methods are compile-time only.");
    public static UserSettings UserSettings() => throw new NotSupportedException("Roblox shim methods are compile-time only.");
    public static string version() => throw new NotSupportedException("Roblox shim methods are compile-time only.");
    [Obsolete("wait is deprecated in Luau.")]
    public static (double Elapsed, double Time) wait(double seconds) => throw new NotSupportedException("Roblox shim methods are compile-time only.");
    public static void warn(params object?[] values) => throw new NotSupportedException("Roblox shim methods are compile-time only.");
    public static (bool Success, object? Result) xpcall(Delegate f, Delegate err, params object?[] args) => throw new NotSupportedException("Roblox shim methods are compile-time only.");
    [Obsolete("ypcall is deprecated in Luau.")]
    public static (bool Success, object? Result) ypcall(Delegate f, params object?[] args) => throw new NotSupportedException("Roblox shim methods are compile-time only.");
}
