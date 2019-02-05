using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Text;

namespace Disasmo
{
    internal class BenchmarkSuggestedAction : BaseSuggestedAction
    {
        public BenchmarkSuggestedAction(CommonSuggestedActionsSource actionsSource) : base(actionsSource) {}

        protected override async Task<ISymbol> GetSymbol(Document document, int tokenPosition, CancellationToken cancellationToken)
        {
            SemanticModel semanticModel = await document.GetSemanticModelAsync(cancellationToken);

            var syntaxTree = await semanticModel.SyntaxTree.GetRootAsync(cancellationToken);
            var token = syntaxTree.FindToken(tokenPosition);

            if (token.Parent is MethodDeclarationSyntax method)
                return ModelExtensions.GetDeclaredSymbol(semanticModel, method);

            return null;
        }

        public override async void Invoke(CancellationToken cancellationToken)
        {
            DisasmWindow window = await IdeUtils.ShowWindowAsync<DisasmWindow>(cancellationToken);
            SyntaxNode syntaxNode = await _symbol.DeclaringSyntaxReferences.FirstOrDefault().GetSyntaxAsync();
            ITrackingSpan trackingSpan = SnapshotSpan.Snapshot.CreateTrackingSpan(new Span(syntaxNode.FullSpan.Start, syntaxNode.FullSpan.Length), SpanTrackingMode.EdgeInclusive);
            trackingSpan.TextBuffer.Insert(syntaxNode.SpanStart, "[BenchmarkDotNet.Attributes.Benchmark]" + Environment.NewLine + "\t\t");

            window?.ViewModel?.RunOperationAsync(_symbol, _codeDoc, OperationType.Benchmark);
        }

        public override string DisplayText => $"Benchmark '{_symbol}' (Adds BenchmarkDotNet package)";
    }
}