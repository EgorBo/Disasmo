using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace Disasmo
{
    public static class RoslynUtils
    {
        public static string GetTypeName(this ISymbol symbol)
        {
            if (symbol is IMethodSymbol)
                return symbol.ContainingType.ToString();
            else
                return symbol.ToString();
        }
    }
}
