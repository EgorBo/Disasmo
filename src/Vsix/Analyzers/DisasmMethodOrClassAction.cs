using System.Threading;
using System.Threading.Tasks;
using Disasmo.Properties;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Document = Microsoft.CodeAnalysis.Document;

namespace Disasmo
{

    internal class DisasmMethodOrClassAction : BaseSuggestedAction
    {
        public DisasmMethodOrClassAction(CommonSuggestedActionsSource actionsSource) : base(actionsSource) {}

        public override async void Invoke(CancellationToken cancellationToken)
        {
            var window = await IdeUtils.ShowWindowAsync<DisasmWindow>(cancellationToken);
            window?.ViewModel?.RunOperationAsync(_symbol);
        }

        protected override async Task<ISymbol> GetSymbol(Document document, int tokenPosition, CancellationToken cancellationToken)
        {
            try
            {
                SemanticModel semanticModel = await document.GetSemanticModelAsync(cancellationToken);

                var syntaxTree = await semanticModel.SyntaxTree.GetRootAsync(cancellationToken);
                var token = syntaxTree.FindToken(tokenPosition);

                if (Settings.Default?.AllowDisasmInvocations == true &&
                    token.Parent?.Parent?.Parent is InvocationExpressionSyntax i)
                    return semanticModel.GetSymbolInfo(i, cancellationToken).Symbol;

                if (token.Parent is MethodDeclarationSyntax m)
                    return semanticModel.GetDeclaredSymbol(m, cancellationToken);

                if (token.Parent is ClassDeclarationSyntax c)
                    return semanticModel.GetDeclaredSymbol(c, cancellationToken);

                if (token.Parent is StructDeclarationSyntax s)
                    return semanticModel.GetDeclaredSymbol(s, cancellationToken);
                
                // TODO: local functions
                // if (token.Parent is LocalFunctionStatementSyntax lf)
                //     return semanticModel.GetDeclaredSymbol(lf, cancellationToken);

                return null;
            }
            catch
            {
                return null;
            }
        }

        public override string DisplayText
        {
            get
            {
                try
                {
                    if (_symbol is IMethodSymbol)
                        return $"Disasm this method";
                    return $"Disasm this class";
                }
                catch
                {
                    return "-";
                }
            }
        }
    }
}