using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Point = System.Windows.Point;
using Size = System.Windows.Size;
using Task = System.Threading.Tasks.Task;

namespace Disasmo
{
    internal abstract class BaseSuggestedAction : ISuggestedAction
    {
        protected readonly CommonSuggestedActionsSource _actionsSource;
        protected ISymbol _symbol;

        public BaseSuggestedAction(CommonSuggestedActionsSource actionsSource) => _actionsSource = actionsSource;

        public SnapshotSpan SnapshotSpan { get; set; }

        public int CaretPosition { get; set; }

        public async Task<bool> ValidateAsync(CancellationToken cancellationToken)
        {
            try
            {
                var document = SnapshotSpan.Snapshot.TextBuffer.GetRelatedDocuments().FirstOrDefault();
                _symbol = document != null ? await GetSymbol(document, CaretPosition, cancellationToken) : null;
                return _symbol != null;
            }
            catch
            {
                return false;
            }
        }

        public ISymbol Symbol => _symbol;
        protected abstract Task<ISymbol> GetSymbol(Document document, int tokenPosition, CancellationToken cancellationToken);
        public abstract string DisplayText { get; }
        public string IconAutomationText => "Disamo";
        ImageMoniker ISuggestedAction.IconMoniker => KnownMonikers.CSLightswitch;
        public string InputGestureText => null;
        public bool HasActionSets => false;
        public Task<IEnumerable<SuggestedActionSet>> GetActionSetsAsync(CancellationToken cancellationToken) => null;
        public bool HasPreview => false;
        public Task<object> GetPreviewAsync(CancellationToken cancellationToken) => Task.FromResult<object>(null);
        public void Dispose() { }
        public abstract void Invoke(CancellationToken cancellationToken);
        public bool TryGetTelemetryId(out Guid telemetryId)
        {
            telemetryId = Guid.Empty;
            return false;
        }
    }
}