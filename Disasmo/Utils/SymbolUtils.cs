using Microsoft.CodeAnalysis;

namespace Disasmo.Utils;

public class SymbolUtils
{
    public static DisasmoSymbolInfo FromSymbol(ISymbol symbol)
    {
        // whole class
        if (symbol is not IMethodSymbol ms)
            return new(
                symbol.Name + ":*",
                symbol.ToString(),
                "*");

        // method name is always "*" now, remove it?
        return ms.MethodKind switch
        {
            MethodKind.LocalFunction =>
                // hack for mangled names
                new("*" + symbol.Name + "*",
                    symbol.ContainingType.ToString(),
                    "*"),
            MethodKind.Constructor =>
                new("*" + symbol.ContainingType.Name + ":.ctor",
                    symbol.ContainingType.ToString(),
                    "*"),
            _ => new("*" + symbol.ContainingType.Name + ":" + symbol.Name,
                    symbol.ContainingType.ToString(),
                    "*")
        };
    }
}