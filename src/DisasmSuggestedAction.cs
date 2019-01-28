using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;
using Document = Microsoft.CodeAnalysis.Document;
using Task = System.Threading.Tasks.Task;

namespace Disasmo
{
    [Export(typeof(ISuggestedActionsSourceProvider))]
    [Name("Test Suggested Actions")]
    [ContentType("text")]
    internal class DisasmSuggestedActionsSourceProvider : ISuggestedActionsSourceProvider
    {
        [Import(typeof(ITextStructureNavigatorSelectorService))]
        internal ITextStructureNavigatorSelectorService NavigatorService { get; set; }

        [ImportingConstructor]
        public DisasmSuggestedActionsSourceProvider([Import(typeof(VisualStudioWorkspace), AllowDefault = true)] Workspace workspace) {        }

        public ISuggestedActionsSource CreateSuggestedActionsSource(ITextView textView, ITextBuffer textBuffer)
        {
            if (textBuffer == null && textView == null)
                return null;

            return new DisasmtSuggestedActionsSource(this, textView, textBuffer);
        }
    }

    internal class DisasmtSuggestedActionsSource : ISuggestedActionsSource
    {
        public DisasmtSuggestedActionsSource(DisasmSuggestedActionsSourceProvider sp, 
            ITextView textView, ITextBuffer textBuffer) {}

        public event EventHandler<EventArgs> SuggestedActionsChanged;

        public void Dispose() {}

        public IEnumerable<SuggestedActionSet> GetSuggestedActions(
            ISuggestedActionCategorySet requestedActionCategories, SnapshotSpan range,
            CancellationToken cancellationToken)
        {
            var action = new DisasmSuggestedAction( range);
            var actions = new List<DisasmSuggestedAction>();

            if (action.Validate(cancellationToken).Result)
                actions.Add(action);

            return new[] { new SuggestedActionSet(actions) };
        }

        public Task<bool> HasSuggestedActionsAsync(ISuggestedActionCategorySet requestedActionCategories,
            SnapshotSpan range, CancellationToken cancellationToken)
        {
            return new DisasmSuggestedAction(range).Validate(cancellationToken);
        }

        public bool TryGetTelemetryId(out Guid telemetryId)
        {
            telemetryId = Guid.Empty;
            return false;
        }
    }

    internal class DisasmSuggestedAction : ISuggestedAction
    {
        private readonly SnapshotSpan _range;
        private ISymbol _symbol;

        public DisasmSuggestedAction(SnapshotSpan range)
        {
            _range = range;
        }

        public async Task<bool> Validate(CancellationToken cancellationToken)
        {
            try
            {
                var document = _range.Snapshot.TextBuffer.GetRelatedDocuments().FirstOrDefault();
                _symbol = document != null ? await GetSymbol(document, _range.Start, cancellationToken) : null;
                return _symbol != null;
            }
            catch
            {
                return false;
            }
        }

        private static async Task<ISymbol> GetSymbol(Document document, int tokenPosition, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var syntaxTree = await semanticModel.SyntaxTree.GetRootAsync(cancellationToken);
            var token = syntaxTree.FindToken(tokenPosition);

            if (token.Parent is MethodDeclarationSyntax m)
                return semanticModel.GetDeclaredSymbol(m);

            if (token.Parent is ClassDeclarationSyntax c)
                return semanticModel.GetDeclaredSymbol(c);

            return null;
        }

        public string DisplayText
        {
            get
            {
                if (_symbol is IMethodSymbol)
                    return $"Disasm {_symbol.Name}";
                return $"Disasm all methods of {_symbol.Name}";
            }
        }

        public string IconAutomationText => null;
        ImageMoniker ISuggestedAction.IconMoniker => default(ImageMoniker);
        public string InputGestureText => null;
        public bool HasActionSets => false;
        public Task<IEnumerable<SuggestedActionSet>> GetActionSetsAsync(CancellationToken cancellationToken) => null;
        public bool HasPreview => false;
        public Task<object> GetPreviewAsync(CancellationToken cancellationToken) => Task.FromResult<object>(null);
        public void Dispose() {}

        public async void Invoke(CancellationToken cancellationToken)
        {
            if (DisasmoPackage.Current == null)
            {
                MessageBox.Show("DisasmoPackage is not loaded yet, please try again later.");
                return;
            }

            await DisasmoPackage.Current.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            ToolWindowPane window = await DisasmoPackage.Current.ShowToolWindowAsync(typeof(DisasmWindow), 0, create: true, cancellationToken: DisasmoPackage.Current.DisposalToken);
            await DisasmoPackage.Current.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            // no idea why I have to call it twice, it doesn't work if I do it only once on the first usage
            window = await DisasmoPackage.Current.ShowToolWindowAsync(typeof(DisasmWindow), 0, create: true, cancellationToken: DisasmoPackage.Current.DisposalToken);

            ((DisasmWindow) window).ViewModel.DisasmAsync(_symbol);
        }

        public bool TryGetTelemetryId(out Guid telemetryId)
        {
            telemetryId = Guid.Empty;
            return false;
        }
    }
}