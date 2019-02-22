using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.Text.Operations;

namespace Disasmo
{
    internal class ObjectLayoutSuggestedAction : BaseSuggestedAction
    {
        public ObjectLayoutSuggestedAction(CommonSuggestedActionsSource actionsSource) : base(actionsSource) {}

        protected override async Task<ISymbol> GetSymbol(Document document, int tokenPosition, CancellationToken cancellationToken)
        {
            SemanticModel semanticModel = await document.GetSemanticModelAsync(cancellationToken);

            var syntaxTree = await semanticModel.SyntaxTree.GetRootAsync(cancellationToken);
            var token = syntaxTree.FindToken(tokenPosition);

            if (token.Parent is ClassDeclarationSyntax c)
                return ModelExtensions.GetDeclaredSymbol(semanticModel, c, cancellationToken);

            var vds = token.Parent is VariableDeclarationSyntax variable
                ? variable
                : token.Parent?.Parent as VariableDeclarationSyntax;
            if (vds != null)
            {
                var info = semanticModel.GetSymbolInfo(vds.Type, cancellationToken);
                if (string.IsNullOrWhiteSpace(info.Symbol.ToString()))
                    return null;
                return info.Symbol;
            }

            if (token.Parent is ParameterSyntax parameterSyntax)
            {
                var info = semanticModel.GetSymbolInfo(parameterSyntax.Type, cancellationToken);
                if (string.IsNullOrWhiteSpace(info.Symbol.ToString()))
                    return null;
                return info.Symbol;
            }

            if (_actionsSource.TryGetWordUnderCaret(out TextExtent wordExtent) && wordExtent.IsSignificant)
            {
                var text = wordExtent.Span.GetText();
                // TODO: analyze Invocation expressions, etc
            }

            return null;
        }

        public override async void Invoke(CancellationToken cancellationToken)
        {
            var window = await IdeUtils.ShowWindowAsync<DisasmWindow>(cancellationToken);
            window?.ViewModel?.RunOperationAsync(_symbol, _codeDoc, OperationType.ObjectLayout);
        }

        public override string DisplayText => $"Show memory layout for '{_symbol}' (Adds ObjectLayoutInspector package)";
    }
}