using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Document = Microsoft.CodeAnalysis.Document;

namespace Disasmo
{

    internal class DisasmMethodOrClassAction : BaseSuggestedAction
    {
        public DisasmMethodOrClassAction(CommonSuggestedActionsSource actionsSource) : base(actionsSource) {}

        public override async void Invoke(CancellationToken cancellationToken)
        {
            var window = await IdeUtils.ShowWindowAsync<DisasmWindow>(cancellationToken);
            window?.ViewModel?.RunOperationAsync(_symbol, _codeDoc, OperationType.Disasm);
        }

        protected override async Task<ISymbol> GetSymbol(Document document, int tokenPosition, CancellationToken cancellationToken)
        {
            SemanticModel semanticModel = await document.GetSemanticModelAsync(cancellationToken);

            var syntaxTree = await semanticModel.SyntaxTree.GetRootAsync(cancellationToken);
            var token = syntaxTree.FindToken(tokenPosition);

            if (token.Parent is MethodDeclarationSyntax m)
                return ModelExtensions.GetDeclaredSymbol(semanticModel, m);

            if (token.Parent is ClassDeclarationSyntax c)
                return ModelExtensions.GetDeclaredSymbol(semanticModel, c);

            return null;
        }

        public override string DisplayText
        {
            get
            {
                if (_symbol is IMethodSymbol)
                    return $"Disasm '{_symbol.Name}' method";
                return $"Disasm '{_symbol.Name}' class";
            }
        }
    }
}