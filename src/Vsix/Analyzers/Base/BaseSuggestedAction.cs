using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Task = System.Threading.Tasks.Task;

namespace Disasmo
{
    internal abstract class BaseSuggestedAction : ISuggestedAction
    {
        protected readonly CommonSuggestedActionsSource _actionsSource;

        public BaseSuggestedAction(CommonSuggestedActionsSource actionsSource) => _actionsSource = actionsSource;

        public SnapshotSpan SnapshotSpan { get; set; }

        public int CaretPosition { get; set; }

        public async Task<bool> ValidateAsync(CancellationToken cancellationToken)
        {
            try
            {
                LastDocument = null;
                LastTokenPos = 0;
                var document = SnapshotSpan.Snapshot.TextBuffer.GetRelatedDocuments().FirstOrDefault();
                if (await IsValidSymbol(document, CaretPosition, cancellationToken))
                {
                    LastDocument = document;
                    LastTokenPos = CaretPosition;
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        public int LastTokenPos { get; set; }

        public Document LastDocument { get; set; }

        protected abstract Task<bool> IsValidSymbol(Document document, int tokenPosition, CancellationToken cancellationToken);

        protected abstract Task<ISymbol> GetSymbol(Document doc, int pos, CancellationToken ct);
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