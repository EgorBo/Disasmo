using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Disasmo.Analyzers;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;

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
                    new ObjectLayoutSuggestedAction(this),
                };
        }
        
        public event EventHandler<EventArgs> SuggestedActionsChanged;

        public void Dispose() {}

        public IEnumerable<SuggestedActionSet> GetSuggestedActions(
            ISuggestedActionCategorySet requestedActionCategories, SnapshotSpan range,
            CancellationToken cancellationToken)
        {
            return _baseActions
                .Where(a => a.Symbol != null)
                .Select(a =>
                {
                    a.SnapshotSpan = range;
                    return new SuggestedActionSet(a.GetType().Name, new[] {a}, priority: SuggestedActionSetPriority.Low);
                });
        }

        public async Task<bool> HasSuggestedActionsAsync(ISuggestedActionCategorySet requestedActionCategories,
            SnapshotSpan range, CancellationToken cancellationToken)
        {
            return await await Task.WhenAny(_baseActions.Select(t =>
                {
                    t.SnapshotSpan = range;
                    return t.Validate(cancellationToken);
                }));
        }

        public bool TryGetTelemetryId(out Guid telemetryId)
        {
            telemetryId = Guid.Empty;
            return false;
        }

        public bool TryGetWordUnderCaret(out TextExtent wordExtent)
        {
            ITextCaret caret = TextView.Caret;
            SnapshotPoint point;

            if (caret.Position.BufferPosition > 0)
            {
                point = caret.Position.BufferPosition - 1;
            }
            else
            {
                wordExtent = default(TextExtent);
                return false;
            }

            ITextStructureNavigator navigator = SourceProvider.NavigatorService.GetTextStructureNavigator(TextBuffer);

            wordExtent = navigator.GetExtentOfWord(point);
            return true;
        }
    }
}