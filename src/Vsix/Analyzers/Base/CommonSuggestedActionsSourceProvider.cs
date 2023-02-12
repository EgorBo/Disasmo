﻿using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Disasmo.Analyzers;

[Export(typeof(ISuggestedActionsSourceProvider))]
[Name("Disasmo Suggested Actions")]
[ContentType("text")]
internal class CommonSuggestedActionsSourceProvider : ISuggestedActionsSourceProvider
{
    public ISuggestedActionsSource CreateSuggestedActionsSource(ITextView textView, ITextBuffer textBuffer)
    {
        if (textBuffer == null && textView == null)
            return null;

        return new CommonSuggestedActionsSource(this, textView, textBuffer);
    }
}
