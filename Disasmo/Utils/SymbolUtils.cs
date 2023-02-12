using Microsoft.CodeAnalysis;

namespace Disasmo;

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
                hostType = symbol.ContainingType.ToString();
                methodName = "*";
            }
            else if (ms.MethodKind == MethodKind.Constructor)
            {
                target = "*" + symbol.ContainingType.Name + ":.ctor";
                hostType = symbol.ContainingType.ToString();
                methodName = "*";
            }
            else
            {
                target = "*" + symbol.ContainingType.Name + ":" + symbol.Name;
                hostType = symbol.ContainingType.ToString();
                methodName = symbol.Name;
            }
        }
        else if (symbol is IPropertySymbol)
        {
            target = "*" + symbol.ContainingType.Name + ":get_" + symbol.Name + " " + "*" + symbol.ContainingType.Name + ":set_" + symbol.Name;
            hostType = symbol.ContainingType.ToString();
            methodName = symbol.Name;
        }
        else
        {
            // the whole class
            target = symbol.Name + ":*";
            hostType = symbol.ToString();
            methodName = "*";
        }
        return new DisasmoSymbolInfo(target, hostType, methodName);
    }
}