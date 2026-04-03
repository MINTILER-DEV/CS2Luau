using Microsoft.CodeAnalysis;

namespace CS2Luau.Compiler.Roblox;

internal sealed class RobloxSymbolRegistry
{
    private readonly INamedTypeSymbol? globalsType;
    private readonly INamedTypeSymbol? instanceType;
    private readonly INamedTypeSymbol? dataModelType;
    private readonly INamedTypeSymbol? serviceProviderType;
    private readonly INamedTypeSymbol? enumWrapperType;

    public RobloxSymbolRegistry(Compilation compilation)
    {
        globalsType = compilation.GetTypeByMetadataName("Roblox.Globals");
        instanceType = compilation.GetTypeByMetadataName("Roblox.Instance");
        dataModelType = compilation.GetTypeByMetadataName("Roblox.DataModel");
        serviceProviderType = compilation.GetTypeByMetadataName("Roblox.Instances.ServiceProvider");
        enumWrapperType = compilation.GetTypeByMetadataName("Roblox.Enum");
    }

    public bool TryGetGlobalPropertyName(ISymbol? symbol, out string name)
    {
        if (symbol is IPropertySymbol property &&
            SymbolEqualityComparer.Default.Equals(property.ContainingType, globalsType))
        {
            name = property.Name;
            return true;
        }

        name = string.Empty;
        return false;
    }

    public bool IsGlobalMethod(IMethodSymbol? method)
    {
        return method is not null &&
               SymbolEqualityComparer.Default.Equals(method.ContainingType, globalsType);
    }

    public bool IsGlobalMethod(IMethodSymbol? method, string expectedName)
    {
        return method is not null &&
               IsGlobalMethod(method) &&
               string.Equals(method.Name, expectedName, StringComparison.Ordinal);
    }

    public bool IsRobloxInstance(ITypeSymbol? type)
    {
        if (type is null || instanceType is null)
        {
            return false;
        }

        for (var current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, instanceType))
            {
                return true;
            }
        }

        return false;
    }

    public bool IsRobloxSignal(ITypeSymbol? type)
    {
        return type is INamedTypeSymbol named &&
               named.ContainingNamespace.ToDisplayString().StartsWith("Roblox.Signals", StringComparison.Ordinal);
    }

    public bool IsRobloxDatatype(INamedTypeSymbol? type)
    {
        return type is not null &&
               string.Equals(type.ContainingNamespace.ToDisplayString(), "Roblox.Datatypes", StringComparison.Ordinal);
    }

    public bool IsRobloxEnum(INamedTypeSymbol? type)
    {
        return type is not null &&
               type.TypeKind == TypeKind.Enum &&
               string.Equals(type.ContainingNamespace.ToDisplayString(), "Roblox.Enums", StringComparison.Ordinal);
    }

    public bool TryGetEnumWrapperMember(ISymbol? symbol, out string enumName, out string memberName)
    {
        switch (symbol)
        {
            case IFieldSymbol field when field.IsStatic &&
                                        field.ContainingType is not null &&
                                        field.ContainingType.ContainingType is not null &&
                                        SymbolEqualityComparer.Default.Equals(field.ContainingType.ContainingType, enumWrapperType) &&
                                        field.Type is INamedTypeSymbol enumType &&
                                        IsRobloxEnum(enumType):
                enumName = enumType.Name;
                memberName = field.Name;
                return true;
            case IPropertySymbol property when property.IsStatic &&
                                              property.ContainingType is not null &&
                                              property.ContainingType.ContainingType is not null &&
                                              SymbolEqualityComparer.Default.Equals(property.ContainingType.ContainingType, enumWrapperType) &&
                                              property.Type is INamedTypeSymbol enumType &&
                                              IsRobloxEnum(enumType):
                enumName = enumType.Name;
                memberName = property.Name;
                return true;
            default:
                enumName = string.Empty;
                memberName = string.Empty;
                return false;
        }
    }

    public bool IsGetServiceGeneric(IMethodSymbol? method)
    {
        return method is not null &&
               method.Name == "GetService" &&
               method.IsGenericMethod &&
               SymbolEqualityComparer.Default.Equals(method.ContainingType, dataModelType);
    }

    public bool IsGetServiceString(IMethodSymbol? method)
    {
        return method is not null &&
               method.Name == "GetService" &&
               !method.IsGenericMethod &&
               (SymbolEqualityComparer.Default.Equals(method.ContainingType, dataModelType) ||
                SymbolEqualityComparer.Default.Equals(method.ContainingType, serviceProviderType));
    }

    public bool IsInstanceNewGeneric(IMethodSymbol? method)
    {
        return method is not null &&
               method.Name == "New" &&
               method.IsStatic &&
               method.IsGenericMethod &&
               SymbolEqualityComparer.Default.Equals(method.ContainingType, instanceType);
    }

    public bool IsInstanceNewString(IMethodSymbol? method)
    {
        return method is not null &&
               method.Name == "New" &&
               method.IsStatic &&
               !method.IsGenericMethod &&
               SymbolEqualityComparer.Default.Equals(method.ContainingType, instanceType);
    }

    public bool ShouldUseColonCall(IMethodSymbol? method)
    {
        if (method is null || method.IsStatic)
        {
            return false;
        }

        return IsRobloxInstance(method.ContainingType) ||
               IsRobloxSignal(method.ContainingType) ||
               IsUserSourceType(method.ContainingType);
    }

    public bool IsUserSourceType(ITypeSymbol? type)
    {
        return type is INamedTypeSymbol named &&
               named.Locations.Any(location => location.IsInSource) &&
               !IsRobloxDatatype(named);
    }

    public bool IsArray(ITypeSymbol? type)
    {
        return type is IArrayTypeSymbol;
    }

    public bool IsRobloxServiceType(INamedTypeSymbol? type)
    {
        return type is not null &&
               IsRobloxInstance(type) &&
               string.Equals(type.ContainingNamespace.ToDisplayString(), "Roblox.Services", StringComparison.Ordinal);
    }
}
