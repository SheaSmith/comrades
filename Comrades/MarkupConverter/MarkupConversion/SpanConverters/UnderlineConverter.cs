using AngleSharp.Dom;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;

namespace Comrades.MarkupConverter.MarkupConversion.SpanConverters;

public class UnderlineConverter : INodeConversion
{
    public bool IsSupportedElement(INode node)
    {
        return node is IElement {TagName: "U"};
    }

    public InlineCollection RenderNode(INode node, RichTextBlock richText, InlineCollection currentInlines,
        HtmlToWinUiConverter converter, bool shallowRender = false)
    {
        var bold = new Underline();
        if (!shallowRender)
            converter.RenderNodes(node.ChildNodes, richText, bold.Inlines);
        currentInlines.Add(bold);
        return shallowRender ? bold.Inlines : currentInlines;
    }
}