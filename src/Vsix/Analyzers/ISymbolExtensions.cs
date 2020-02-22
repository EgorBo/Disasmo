using Microsoft.CodeAnalysis;

namespace Disasmo.Analyzers
{
    public static class ISymbolExtensions
    {
        public static string GetJitDisasmTarget(this ISymbol symbol)
        {
            // see https://github.com/dotnet/coreclr/blob/master/Documentation/building/viewing-jit-dumps.md#specifying-method-names
            switch (symbol.Kind)
            {
                case SymbolKind.Method:
                    return symbol.ContainingType.Name + "::" + symbol.Name;
                case SymbolKind.Property:
                    var propertySymbol = (IPropertySymbol)symbol;
                    var getMethod = propertySymbol.GetMethod;
                    var setMethod = propertySymbol.SetMethod;
                    if (getMethod != null && setMethod != null)
                    {
                        return $"{GetJitDisasmTarget(getMethod)} {GetJitDisasmTarget(setMethod)}";
                    }
                    else
                    {
                        if (getMethod != null) return GetJitDisasmTarget(getMethod);
                        else return GetJitDisasmTarget(setMethod);
                    }
                default:
                    return symbol.Name + "::*";
            }
        }

        public static string GetDisasmSuggestedActionDisplayText(this ISymbol symbol)
        {
            try
            {
                string name = symbol?.Name;
                string symbolType;
                switch (symbol.Kind)
                {
                    case SymbolKind.Method:
                        var methodSymbol = (IMethodSymbol)symbol;
                        switch (methodSymbol.MethodKind)
                        {
                            case MethodKind.PropertyGet:
                                symbolType = $"{(IsIndexer() ? "indexer" : "property")} getter";
                                name = methodSymbol.AssociatedSymbol.Name;
                                break;
                            case MethodKind.PropertySet:
                                symbolType = $"{(IsIndexer() ? "indexer" : "property")} setter";
                                name = methodSymbol.AssociatedSymbol.Name;
                                break;
                            default:
                                symbolType = "method";
                                break;
                        }

                        bool IsIndexer() => (methodSymbol.AssociatedSymbol as IPropertySymbol)?.IsIndexer ?? false;
                        break;
                    case SymbolKind.NamedType:
                        symbolType = ((INamedTypeSymbol)symbol).IsValueType ? "struct" : "class";
                        break;
                    case SymbolKind.Property:
                        symbolType = ((IPropertySymbol)symbol).IsIndexer ? "indexer" : "property";
                        break;
                    default:
                        symbolType = string.Empty;
                        break;
                }
                return $"Disasm '{name}' {symbolType}";
            }
            catch
            {
                return "-";
            }
        }

        public static string GetContainingTypeNameOrSelf(this ISymbol symbol)
        {
            return symbol.Kind == SymbolKind.NamedType ? symbol.Name : symbol.ContainingType.Name;
        }
    }
}