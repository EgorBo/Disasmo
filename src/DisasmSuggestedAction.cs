using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
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
        public Workspace Workspace { get; }

        [Import(typeof(ITextStructureNavigatorSelectorService))]
        internal ITextStructureNavigatorSelectorService NavigatorService { get; set; }

        [ImportingConstructor]
        public DisasmSuggestedActionsSourceProvider([Import(typeof(VisualStudioWorkspace), AllowDefault = true)] Workspace workspace)
        {
            Workspace = workspace;
        }

        public ISuggestedActionsSource CreateSuggestedActionsSource(ITextView textView, ITextBuffer textBuffer)
        {
            if (textBuffer == null && textView == null)
                return null;

            return new DisasmSuggestedActionsSource(this, textView, textBuffer);
        }
    }

    internal class DisasmSuggestedActionsSource : ISuggestedActionsSource
    {
        public DisasmSuggestedActionsSourceProvider SourceProvider { get; }

        public DisasmSuggestedActionsSource(DisasmSuggestedActionsSourceProvider sourceProvider,
            ITextView textView, ITextBuffer textBuffer)
        {
            SourceProvider = sourceProvider;
        }

        public event EventHandler<EventArgs> SuggestedActionsChanged;

        public void Dispose() {}

        public IEnumerable<SuggestedActionSet> GetSuggestedActions(
            ISuggestedActionCategorySet requestedActionCategories, SnapshotSpan range,
            CancellationToken cancellationToken)
        {
            var disasmAction = new DisasmMethodOrClassAction(this, range);
            var objectLayoutAction = new ObjectLayoutSuggestedAction(this, range);
            var actions = new List<ISuggestedAction>();

            if (disasmAction.Validate(cancellationToken).Result)
                actions.Add(disasmAction);

            if (objectLayoutAction.Validate(cancellationToken).Result)
                actions.Add(objectLayoutAction);

            return new[]
            {
                new SuggestedActionSet("Disasm", actions)
            };
        }

        public Task<bool> HasSuggestedActionsAsync(ISuggestedActionCategorySet requestedActionCategories,
            SnapshotSpan range, CancellationToken cancellationToken)
        {
            if (requestedActionCategories.Contains("Disasm"))
                return new DisasmMethodOrClassAction(this, range).Validate(cancellationToken);
            else
                return new ObjectLayoutSuggestedAction(this, range).Validate(cancellationToken);
        }

        public bool TryGetTelemetryId(out Guid telemetryId)
        {
            telemetryId = Guid.Empty;
            return false;
        }
    }

    internal class DisasmMethodOrClassAction : BaseSuggestedAction
    {
        public DisasmMethodOrClassAction(DisasmSuggestedActionsSource actionsSource, SnapshotSpan range) 
            : base(actionsSource, range) {}

        public override string DisplayText
        {
            get
            {
                if (_symbol is IMethodSymbol)
                    return $"Disasm '{_symbol.Name}' method";
                return $"Disasm '{_symbol.Name}' class";
            }
        }

        public override async void Invoke(CancellationToken cancellationToken)
        {
            var window = await ShowWindow(cancellationToken);
            window?.ViewModel?.DisasmAsync(_symbol, _codeDoc, false);
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
    }

    internal class ObjectLayoutSuggestedAction : BaseSuggestedAction
    {
        public ObjectLayoutSuggestedAction(DisasmSuggestedActionsSource actionsSource, SnapshotSpan range) 
            : base(actionsSource, range) {}

        protected override async Task<ISymbol> GetSymbol(Document document, int tokenPosition, CancellationToken cancellationToken)
        {
            SemanticModel semanticModel = await document.GetSemanticModelAsync(cancellationToken);

            var syntaxTree = await semanticModel.SyntaxTree.GetRootAsync(cancellationToken);
            var token = syntaxTree.FindToken(tokenPosition);

            if (token.Parent is ClassDeclarationSyntax c)
                return ModelExtensions.GetDeclaredSymbol(semanticModel, c);

            var vds = token.Parent is VariableDeclarationSyntax variable ? variable : token.Parent?.Parent as VariableDeclarationSyntax;
            if (vds != null)
            {
                var info = semanticModel.GetSymbolInfo(vds.Type);
                if (string.IsNullOrWhiteSpace(info.Symbol.ToString()))
                    return null;
                return info.Symbol;
            }

            return null;
        }

        public override async void Invoke(CancellationToken cancellationToken)
        {
            var window = await ShowWindow(cancellationToken);
            window?.ViewModel?.DisasmAsync(_symbol, _codeDoc, true);
        }

        public override string DisplayText => $"Run ObjectLayoutInspector for '{_symbol}'";
    }

    internal abstract class BaseSuggestedAction : ISuggestedAction
    {
        protected readonly DisasmSuggestedActionsSource _actionsSource;
        protected readonly SnapshotSpan _range;
        protected ISymbol _symbol;
        protected Document _codeDoc;

        public BaseSuggestedAction(DisasmSuggestedActionsSource actionsSource, SnapshotSpan range)
        {
            _actionsSource = actionsSource;
            _range = range;
        }

        public async Task<bool> Validate(CancellationToken cancellationToken)
        {
            try
            {
                var document = _range.Snapshot.TextBuffer.GetRelatedDocuments().FirstOrDefault();
                _codeDoc = document;
                _symbol = document != null ? await GetSymbol(document, _range.Start, cancellationToken) : null;
                return _symbol != null;
            }
            catch
            {
                return false;
            }
        }

        protected abstract Task<ISymbol> GetSymbol(Document document, int tokenPosition,
            CancellationToken cancellationToken);

        public abstract string DisplayText { get; }

        public string IconAutomationText => null;
        ImageMoniker ISuggestedAction.IconMoniker => default(ImageMoniker);
        public string InputGestureText => null;
        public bool HasActionSets => false;
        public Task<IEnumerable<SuggestedActionSet>> GetActionSetsAsync(CancellationToken cancellationToken) => null;
        public bool HasPreview => false;
        public Task<object> GetPreviewAsync(CancellationToken cancellationToken) => Task.FromResult<object>(null);
        public void Dispose() { }

        public abstract void Invoke(CancellationToken cancellationToken);

        protected async Task<DisasmWindow> ShowWindow(CancellationToken cancellationToken)
        {
            if (DisasmoPackage.Current == null)
            {
                MessageBox.Show("DisasmoPackage is not loaded yet, please try again later.");
                return null;
            }

            await DisasmoPackage.Current.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            ToolWindowPane window = await DisasmoPackage.Current.ShowToolWindowAsync(typeof(DisasmWindow), 0, create: true, cancellationToken: DisasmoPackage.Current.DisposalToken);
            await DisasmoPackage.Current.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            // no idea why I have to call it twice, it doesn't work if I do it only once on the first usage
            window = await DisasmoPackage.Current.ShowToolWindowAsync(typeof(DisasmWindow), 0, create: true, cancellationToken: DisasmoPackage.Current.DisposalToken);
            return window as DisasmWindow;
        }

        public bool TryGetTelemetryId(out Guid telemetryId)
        {
            telemetryId = Guid.Empty;
            return false;
        }
    }
}