using System;
using AngleSharp.Dom;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;

namespace Comrades.MarkupConverter.MarkupConversion.SpanConverters;

public class HyperlinkConverter : INodeConversion
{
    public bool IsSupportedElement(INode node)
    {
        return node is IElement {TagName: "A"};
    }

    public InlineCollection RenderNode(INode node, RichTextBlock richText, InlineCollection currentInlines,
        HtmlToWinUiConverter converter, bool shallowRender = false)
    {
        var element = ((IElement) node);
        var hyperlink = new Hyperlink {NavigateUri = new Uri(element.GetAttribute("href")!)};

        if (element.HasAttribute("title"))
        {
            ToolTipService.SetToolTip(hyperlink, element.GetAttribute("title"));
        }

        if (!shallowRender)
            converter.RenderNodes(node.ChildNodes, richText, hyperlink.Inlines);
        currentInlines.Add(hyperlink);
        return shallowRender ? hyperlink.Inlines : currentInlines;
    }
}