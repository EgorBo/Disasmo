using Microsoft.CodeAnalysis;

namespace Disasmo;

public static class SymbolUtils
{
    public static DisasmoSymbolInfo FromSymbol(ISymbol symbol)
    {
        string target;
        string hostType;
        string methodName;

        string prefix = "";
        ISymbol containingType = symbol as ITypeSymbol ?? symbol.ContainingType;

        // match all for nested types
        if (containingType.ContainingType is { })
            prefix = "*";
        else if (containingType.ContainingNamespace?.Name is { Length: > 0 } containingNamespace)
            prefix = containingNamespace + ".";

        prefix += containingType.Name;

        if (symbol is IMethodSymbol ms)
        {
            if (ms.MethodKind == MethodKind.LocalFunction)
            {
                // hack for mangled names
                target = "*" + symbol.Name + "*";
                hostType = symbol.ContainingType.ToString();
                methodName = "*";
            }
            else if (ms.MethodKind == MethodKind.Constructor)
            {
                target = prefix + ":.ctor";
                hostType = symbol.ContainingType.ToString();
                methodName = "*";
            }
            else
            {
                target = prefix + ":" + symbol.Name;
                hostType = symbol.ContainingType.ToString();
                methodName = symbol.Name;
            }
        }
        else if (symbol is IPropertySymbol)
        {
            target = prefix + ":get_" + symbol.Name + " " + prefix + ":set_" + symbol.Name;
            hostType = symbol.ContainingType.ToString();
            methodName = symbol.Name;
        }
        else
        {
            // the whole class
            target = prefix + ":*";
            hostType = symbol.ToString();
            methodName = "*";
        }
        return new DisasmoSymbolInfo(target, hostType, methodName);
    }
}
