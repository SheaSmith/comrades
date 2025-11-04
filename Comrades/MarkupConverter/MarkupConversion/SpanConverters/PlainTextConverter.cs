using AngleSharp.Dom;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;

namespace Comrades.MarkupConverter.MarkupConversion.SpanConverters;

public class PlainTextConverter : INodeConversion
{
    public bool IsSupportedElement(INode node)
    {
        return node is IText;
    }

    public InlineCollection RenderNode(INode node, RichTextBlock richText, InlineCollection currentInlines,
        HtmlToWinUiConverter converter, bool shallowRender = false)
    {
        var run = new Run { Text = node.Text() };
        currentInlines.Add(run);
        return currentInlines;
    }
}