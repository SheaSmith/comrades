using AngleSharp.Css.Dom;
using AngleSharp.Dom;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;

namespace Comrades.MarkupConverter.MarkupConversion.SpanConverters;

public class FontSizeConverter : INodeConversion
{
    public bool IsSupportedElement(INode node)
    {
        return node is IElement element && element.GetStyle().GetFontSize() is not null and not "inherit" and not "";
    }

    public InlineCollection RenderNode(INode node, RichTextBlock richText, InlineCollection currentInlines,
        HtmlToWinUiConverter converter, bool shallowRender = false)
    {
        var fontSize = ((IElement) node).GetStyle().GetFontSize();

        double size;
        if (fontSize == "x-large")
        {
            size = (double) Application.Current.Resources["BodyLargeTextBlockFontSize"];
        }
        else if (fontSize == "xx-small")
        {
            size = (double) Application.Current.Resources["CaptionTextBlockFontSize"];
        }
        else
        {
            size = double.Parse(fontSize);
        }

        var span = new Span {FontSize = size};

        if (!shallowRender)
            converter.RenderNodes(node.ChildNodes, richText, span.Inlines);
        currentInlines.Add(span);
        return shallowRender ? span.Inlines : currentInlines;
    }
}