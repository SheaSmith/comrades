
using System;
using System.Collections.Generic;
using System.Linq;
using AngleSharp;
using AngleSharp.Dom;
using Comrades.MarkupConverter.MarkupConversion.BlockConverters;
using Comrades.MarkupConverter.MarkupConversion.SpanConverters;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;

namespace Comrades.MarkupConverter.MarkupConversion;

public class HtmlToWinUiConverter
{
    public List<INodeConversion> Converters { get; set; } = [
        new ParagraphConverter(),
        new BoldConverter(),
        new PlainTextConverter(),
        new FontSizeConverter(),
        new HyperlinkConverter(),
        new ItalicConverter(),
        new StrikethroughConverter(),
        new TextColourConverter(),
        new UnderlineConverter()
    ];
    
    public async void Convert(string html, RichTextBlock richText)
    {
        if (richText == null) throw new ArgumentNullException(nameof(richText));
        richText.Blocks.Clear();

        if (string.IsNullOrWhiteSpace(html))
        {
            return;
        }

        var config = AngleSharp.Configuration.Default.WithCss();
        var context = BrowsingContext.New(config);
        var document = await context.OpenAsync(req => req.Content(html));

        var body = document.Body ?? document.DocumentElement;
        if (body == null) return;

        var initialParagraph = new Paragraph();
        RenderNodes(body.ChildNodes, richText, initialParagraph.Inlines);
        
        if (initialParagraph.Inlines.Count > 0)
            richText.Blocks.Insert(0, initialParagraph);
    }

    public void RenderNodes(INodeList nodes, RichTextBlock richText, InlineCollection inlineCollection)
    {
        foreach (var node in nodes)
        {
            var converters = Converters.Where(m => m.IsSupportedElement(node)).ToList();
            
            if (converters.Count == 0)
                // do nothing for now
                continue;

            var nestedInlineCollection = inlineCollection;
            for (int i = 0 ; i < converters.Count ; i++)
            {
                var converter = converters[i];
                
                nestedInlineCollection = converter.RenderNode(node, richText, nestedInlineCollection, this, i < converters.Count - 1);
            }
        }
    }
}