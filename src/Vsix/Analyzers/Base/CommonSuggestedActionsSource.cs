using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Disasmo.Analyzers;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Disasmo
{
    internal class CommonSuggestedActionsSource : ISuggestedActionsSource
    {
        private BaseSuggestedAction[] _baseActions;

        public CommonSuggestedActionsSourceProvider SourceProvider { get; }

        public ITextView TextView { get; }
        public ITextBuffer TextBuffer { get; }

        public CommonSuggestedActionsSource(CommonSuggestedActionsSourceProvider sourceProvider,
            ITextView textView, ITextBuffer textBuffer)
        {
            SourceProvider = sourceProvider;
            TextView = textView;
            TextBuffer = textBuffer;
            _baseActions = new BaseSuggestedAction[]
                {
                    new DisasmMethodOrClassAction(this),
                };
        }

        public event EventHandler<EventArgs> SuggestedActionsChanged;

        public void Dispose() { }

        public IEnumerable<SuggestedActionSet> GetSuggestedActions(
            ISuggestedActionCategorySet requestedActionCategories, SnapshotSpan range,
            CancellationToken cancellationToken)
        {
            try
            {
                return _baseActions
                    .Where(a => a.Symbol != null)
                    .Select(a =>
                    {
                        a.SnapshotSpan = range;
                        a.CaretPosition = GetCaretPosition();
                        return new SuggestedActionSet(PredefinedSuggestedActionCategoryNames.Any, new[] { a });
                    }).ToArray();
            }
            catch
            {
                return Enumerable.Empty<SuggestedActionSet>();
            }
        }

        public async Task<bool> HasSuggestedActionsAsync(ISuggestedActionCategorySet requestedActionCategories,
            SnapshotSpan range, CancellationToken cancellationToken)
        {
            try
            {
                foreach (var t in _baseActions)
                {
                    t.SnapshotSpan = range;
                    t.CaretPosition = GetCaretPosition();
                    if (await t.Validate(default))
                    {

                        return true;
                    }
                }
            }
            catch
            {
            }
            return false;
        }

        public bool TryGetTelemetryId(out Guid telemetryId)
        {
            telemetryId = Guid.Empty;
            return false;
        }

        public int GetCaretPosition()
        {
            try
            {
                return TextView?.Caret?.Position.BufferPosition ?? -1;
            }
            catch
            {
                return -1;
            }
        }
    }
}