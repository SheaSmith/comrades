using AngleSharp.Dom;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;

namespace Comrades.MarkupConverter.MarkupConversion.BlockConverters;

public class ParagraphConverter : INodeConversion
{
    public bool IsSupportedElement(INode node)
    {
        return node is IElement {TagName: "P"};
    }

    public InlineCollection RenderNode(INode node, RichTextBlock richText, InlineCollection currentInlines,
        HtmlToWinUiConverter converter, bool shallowRender = false)
    {
        var paragraph = new Paragraph();
        if (!shallowRender)
            converter.RenderNodes(node.ChildNodes, richText, paragraph.Inlines);
        richText.Blocks.Add(paragraph);
        
        return paragraph.Inlines;
    }
}