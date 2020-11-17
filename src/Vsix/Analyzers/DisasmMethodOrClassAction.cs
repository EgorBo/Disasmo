using System.Threading;
using System.Threading.Tasks;
using Disasmo.Analyzers;
using Disasmo.Properties;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Document = Microsoft.CodeAnalysis.Document;

namespace Disasmo
{
    internal class DisasmMethodOrClassAction : BaseSuggestedAction
    {
        public DisasmMethodOrClassAction(CommonSuggestedActionsSource actionsSource) : base(actionsSource) { }

        public override async void Invoke(CancellationToken cancellationToken)
        {
            var window = await IdeUtils.ShowWindowAsync<DisasmWindow>(cancellationToken);
            window?.ViewModel?.RunOperationAsync(_symbol, _codeDoc, OperationType.Disasm);
        }

        protected override async Task<ISymbol> GetSymbol(Document document, int tokenPosition, CancellationToken cancellationToken)
        {
            try
            {
                var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
                return await GetSymbolAsync(semanticModel, tokenPosition, cancellationToken);
            }
            catch
            {
                return null;
            }
        }

        internal static async Task<ISymbol> GetSymbolAsync(SemanticModel semanticModel, int tokenPosition, CancellationToken cancellationToken)
        {
            var syntaxTree = await semanticModel.SyntaxTree.GetRootAsync(cancellationToken);
            var token = syntaxTree.FindToken(tokenPosition);

            SyntaxNode node = null;
            switch (token.Parent.Kind())
            {
                case SyntaxKind.MethodDeclaration:
                case SyntaxKind.PropertyDeclaration:
                case SyntaxKind.GetAccessorDeclaration:
                case SyntaxKind.SetAccessorDeclaration:
                case SyntaxKind.IndexerDeclaration:
                case SyntaxKind.ClassDeclaration:
                case SyntaxKind.StructDeclaration:
                    node = token.Parent;
                    break;
                case SyntaxKind.IdentifierName:
                    if (Settings.Default?.AllowDisasmInvocations == true)
                    {
                        node = token.Parent?.Parent?.Parent as InvocationExpressionSyntax;
                    }
                    break;
            }

            return node != null ? semanticModel.GetDeclaredSymbol(node, cancellationToken) : null;
        }

        public override string DisplayText => _symbol.GetDisasmSuggestedActionDisplayText();
    }
}