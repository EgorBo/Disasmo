using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Disasmo;

public static class SymbolUtils
{
    public static DisasmoSymbolInfo FromSymbol(ISymbol symbol)
    {
        string target;
        string hostType;
        string methodName;
        string genericArguments;

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

            genericArguments = GenericsForSymbol(symbol);
        }
        else if (symbol is IPropertySymbol)
        {
            target = "*" + symbol.ContainingType.MetadataName + ":get_" + symbol.Name + " " + "*" + symbol.ContainingType.MetadataName + ":set_" + symbol.Name;
            hostType = symbol.ContainingType.MetadataName();
            methodName = symbol.Name;
            genericArguments = (prop.GetMethod, prop.SetMethod) switch
            {
                (not null, not null) => GenericsForSymbol(prop.GetMethod, false, false) + GenericsForSymbol(prop.SetMethod),
                (null, not null) => GenericsForSymbol(prop.GetMethod),
                (not null, null) => GenericsForSymbol(prop.SetMethod),
                _ => ""
            };
        }
        else
        {
            // the whole class
            target = symbol.Name + ":*";
            hostType = symbol.MetadataName;
            methodName = "*";
            genericArguments = GenericsForSymbol(symbol);
        }
        return new DisasmoSymbolInfo(target, hostType, methodName, genericArguments);
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

    private static string GenericsForSymbol(ISymbol symbol, bool includeNested = true, bool includeContaining = true)
    {
        const string MARKER = "Disasmo-Generic:";

        if (symbol is null)
        {
            return "";
        }

        string result = "";

        foreach (var syntaxReference in symbol.DeclaringSyntaxReferences)
        {
            foreach (var trivia in syntaxReference.GetSyntax().GetLeadingTrivia())
            {
                if (!trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) && !trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)) continue;

                var str = trivia.ToFullString().AsSpan();
                while (!str.IsEmpty)
                {
                    var idxStart = str.IndexOf(MARKER.AsSpan(), StringComparison.OrdinalIgnoreCase);
                    if (idxStart < 0) break;
                    var len = str.Slice(idxStart + MARKER.Length).IndexOf('\n');
                    if (len < 0) len = str.Length - idxStart - MARKER.Length;
                    result = result + SymbolName(symbol) + "=" + str.Slice(idxStart + MARKER.Length, len).Trim().ToString().Replace(" ", "") + ";";
                    str = str.Slice(idxStart + MARKER.Length + len);
                }
            }
        }

        if (includeContaining)
        {
            result += GenericsForSymbol(symbol.ContainingType, false);
        }

        if (includeNested && symbol is ITypeSymbol typeSymbol)
        {
            foreach (var member in typeSymbol.GetMembers())
            {
                if (member is IMethodSymbol)
                {
                    result += GenericsForSymbol(member, false, false);
                }
            }
        }

        return result;
    }

    private static string SymbolName(ISymbol symbol)
    {
        if (symbol is INamedTypeSymbol namedTypeSymbol) return namedTypeSymbol.MetadataName();
        return symbol.MetadataName;
    }
}