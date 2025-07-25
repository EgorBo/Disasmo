﻿using Microsoft.CodeAnalysis;

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

        else
        {
            INamespaceSymbol ns = containingType.ContainingNamespace;
            while (ns?.Name is { Length: > 0 } containingNamespace)
            {
                prefix = containingNamespace + "." + prefix;
                ns = ns.ContainingNamespace;
            }
        }

        prefix += containingType.MetadataName;

        if (containingType is INamedTypeSymbol { IsGenericType: true })
            prefix += "*";

        if (symbol is IMethodSymbol ms)
        {
            if (ms.MethodKind == MethodKind.LocalFunction)
            {
                // hack for mangled names
                target = prefix + ":*" + symbol.MetadataName + "*";
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
                target = prefix + ":" + symbol.MetadataName;
                hostType = symbol.ContainingType.ToString();
                methodName = symbol.MetadataName;
            }
        }
        else if (symbol is IPropertySymbol)
        {
            target = prefix + ":get_" + symbol.MetadataName + " " + prefix + ":set_" + symbol.MetadataName;
            hostType = symbol.ContainingType.ToString();
            methodName = symbol.MetadataName;
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
