using System;
using System.Drawing;
using System.Text.RegularExpressions;
using Windows.UI.Text;
using AngleSharp.Css.Dom;
using AngleSharp.Dom;
using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Color = Windows.UI.Color;

namespace Comrades.MarkupConverter.MarkupConversion.SpanConverters;

public partial class TextColourConverter : INodeConversion
{
    public bool IsSupportedElement(INode node)
    {
        return node is IElement element &&
               element.GetStyle().GetColor() is not null and not "inherit" and not "";
    }

    public InlineCollection RenderNode(INode node, RichTextBlock richText, InlineCollection currentInlines,
        HtmlToWinUiConverter converter, bool shallowRender = false)
    {
        var element = (IElement) node;
        
        var bold = new Span
        {
            Foreground =
                new SolidColorBrush(ParseColor(element.GetStyle().GetColor()))
        };
        if (!shallowRender)
            converter.RenderNodes(node.ChildNodes, richText, bold.Inlines);
        currentInlines.Add(bold);
        return shallowRender ? bold.Inlines : currentInlines;
    }
    
    private static Color ParseColor(string input)
    {
        input = input.Trim();

        // HTML hex: #RRGGBB or #AARRGGBB
        if (HexRegex().IsMatch(input))
        {
            string hex = input.Substring(1);
            byte a = 255, r, g, b;

            if (hex.Length == 6)
            {
                r = Convert.ToByte(hex.Substring(0, 2), 16);
                g = Convert.ToByte(hex.Substring(2, 2), 16);
                b = Convert.ToByte(hex.Substring(4, 2), 16);
            }
            else // 8 chars = AARRGGBB
            {
                a = Convert.ToByte(hex.Substring(0, 2), 16);
                r = Convert.ToByte(hex.Substring(2, 2), 16);
                g = Convert.ToByte(hex.Substring(4, 2), 16);
                b = Convert.ToByte(hex.Substring(6, 2), 16);
            }

            return ColorHelper.FromArgb(a, r, g, b);
        }

        // rgb(r,g,b) or rgba(r,g,b,a)
        var rgbMatch = RgbRegex().Match(input);
        if (rgbMatch.Success)
        {
            var r = byte.Parse(rgbMatch.Groups[1].Value);
            var g = byte.Parse(rgbMatch.Groups[2].Value);
            var b = byte.Parse(rgbMatch.Groups[3].Value);
            byte a = 255;

            if (rgbMatch.Groups[4].Success)
            {
                var alpha = float.Parse(rgbMatch.Groups[4].Value, System.Globalization.CultureInfo.InvariantCulture);
                a = (byte)(alpha * 255);
            }

            return ColorHelper.FromArgb(a, r, g, b);
        }

        throw new ArgumentException($"Invalid color format: {input}");
    }

    [GeneratedRegex("^#([0-9a-fA-F]{6}|[0-9a-fA-F]{8})$")]
    private static partial Regex HexRegex();
    [GeneratedRegex(@"^rgba?\(\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d{1,3})(?:\s*,\s*(\d*\.?\d+))?\s*\)$")]
    private static partial Regex RgbRegex();
}