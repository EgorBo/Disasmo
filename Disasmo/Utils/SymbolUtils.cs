using Microsoft.CodeAnalysis;

namespace Disasmo.Utils;

public static class SymbolUtils
{
    public static DisasmoSymbolInfo FromSymbol(ISymbol symbol)
    {
        string target;
        string hostType;
        string methodName;

        if (symbol is IMethodSymbol ms)
        {
            if (ms.MethodKind == MethodKind.LocalFunction)
            {
                // hack for mangled names
                target = "*" + symbol.Name + "*";
                hostType = symbol.ContainingType.MetadataName;
                methodName = "*";
            }
            else if (ms.MethodKind == MethodKind.Constructor)
            {
                target = "*" + symbol.ContainingType.MetadataName + ":.ctor";
                hostType = symbol.ContainingType.MetadataName();
                methodName = "*";
            }
            else
            {
                target = "*" + symbol.ContainingType.MetadataName + ":" + symbol.Name;
                hostType = symbol.ContainingType.MetadataName();
                methodName = symbol.Name;
            }
        }
        else if (symbol is IPropertySymbol prop)
        {
            target = "*" + symbol.ContainingType.MetadataName + ":get_" + symbol.Name + " " + "*" + symbol.ContainingType.MetadataName + ":set_" + symbol.Name;
            hostType = symbol.ContainingType.MetadataName();
            methodName = symbol.Name;
        }
        else
        {
            // the whole class
            target = symbol.Name + ":*";
            hostType = symbol.MetadataName;
            methodName = "*";
        }
        return new DisasmoSymbolInfo(target, hostType, methodName);
    }

    private static string MetadataName(this INamedTypeSymbol type)
    {
        string name = "";
        if (type.ContainingType != null)
        {
            name = type.ContainingType.MetadataName() + "+";
        }
        else if (type.ContainingNamespace != null && !type.ContainingNamespace.IsGlobalNamespace)
        {
            name = type.ContainingNamespace.ToString() + ".";
        }
        return name + type.MetadataName;
    }
}