using System;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;

namespace Animator.Editor;

/// <summary>
/// Adapts a <see cref="CompletionItem"/> into AvalonEdit's <see cref="ICompletionData"/> so
/// the built-in CompletionWindow can display and commit it.
/// </summary>
public sealed class AvalonCompletionData : ICompletionData
{
    private readonly CompletionItem _item;

    public AvalonCompletionData(CompletionItem item) => _item = item;

    public ImageSource? Image => null;
    public string Text => _item.DisplayText;
    public object Content => _item.DisplayText;
    public object Description => _item.Description;
    public double Priority => _item.Kind switch
    {
        CompletionKind.Local     => 100,
        CompletionKind.Parameter => 95,
        CompletionKind.Property  => 80,
        CompletionKind.Method    => 78,
        CompletionKind.Field     => 76,
        CompletionKind.Event     => 70,
        CompletionKind.Class     => 50,
        CompletionKind.Struct    => 50,
        CompletionKind.Interface => 48,
        CompletionKind.Enum      => 48,
        CompletionKind.Namespace => 30,
        _                        => 10
    };

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        => textArea.Document.Replace(completionSegment, _item.DisplayText);
}
