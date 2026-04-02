namespace Roblox.Datatypes;

public readonly record struct Vector2(double X, double Y);
public readonly record struct Vector3(double X, double Y, double Z);
public readonly record struct CFrame(double X, double Y, double Z);
public readonly record struct Color3(double R, double G, double B);
public readonly record struct UDim(double Scale, double Offset);
public readonly record struct UDim2(UDim X, UDim Y);
