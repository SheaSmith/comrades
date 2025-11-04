using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Css.Dom;
using AngleSharp.Dom;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Media;
using AdaptiveCards.ObjectModel.WinUI3;
using AdaptiveCards.Rendering.WinUI3;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Text;
using Microsoft.Graph.Beta.Models;
using Windows.Data.Json;
using Comrades.Services;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel.DataTransfer;
using System.Globalization;

namespace Comrades.MarkupConverter
{
    // A small, focused HTML → WinUI3 RichText converter.
    // Goals: simple, supports nesting, images, and AdaptiveCards. No WebView.
    public static class ConvertFromHtml3
    {
        public static SolidColorBrush QuoteBrush { get; set; }

        /// <summary>
        /// Teams-only HTML → RichTextBlock. Use this overload when rendering Teams messages so attachments and
        /// Graph images are handled properly.
        /// </summary>
        public static async Task FromHtml(string html, RichTextBlock richText, double width)
        {
            await FromHtml(html, richText, width, null);
        }

        /// <summary>
        /// Teams-only HTML → RichTextBlock with Teams attachments map for resolving <attachment id="..."> nodes.
        /// </summary>
        public static async Task FromHtml(string html, RichTextBlock richText, double width, Dictionary<ChatMessageAttachment, string> attachments)
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

            // Start with a paragraph placeholder; will always append to the last paragraph to keep order
            var paragraph = new Paragraph();
            richText.Blocks.Add(paragraph);

            foreach (var child in body.ChildNodes)
            {
                if (richText.Blocks.LastOrDefault() is Paragraph last)
                {
                    paragraph = last;
                }
                await AddNode(paragraph, child, richText, width, 0, attachments);
            }

            // Remove leading empty paragraph if it remained unused
            if (richText.Blocks.FirstOrDefault() is Paragraph first && !first.Inlines.Any())
            {
                richText.Blocks.Remove(first);
            }

            TrimTrailingBreaks(richText);
        }

        private static double? SafeMaxWidth(double width)
        {
            if (double.IsNaN(width) || double.IsInfinity(width)) return null;
            var v = width - 10;
            if (v <= 0) return null;
            return v;
        }

        public static void UpdateSize(RichTextBlock richText, double width, double oldWidth)
        {
            if (richText == null) return;
            double? maxOpt = SafeMaxWidth(width);
            foreach (var block in richText.Blocks)
            {
                if (block is Paragraph p)
                {
                    foreach (var inline in p.Inlines)
                    {
                        if (inline is InlineUIContainer ui && ui.Child is FrameworkElement fe)
                        {
                            // Apply a sane MaxWidth only when we have a valid container width
                            if (maxOpt.HasValue)
                            {
                                fe.MaxWidth = maxOpt.Value;
                            }

                            // If the element is meant to stretch, clear fixed Width
                            if (fe.HorizontalAlignment == Microsoft.UI.Xaml.HorizontalAlignment.Stretch)
                            {
                                fe.Width = double.NaN;
                            }
                            else if (maxOpt.HasValue)
                            {
                                // If it previously matched old container width, update to new
                                if (!double.IsNaN(fe.Width) && !double.IsNaN(oldWidth) && (Math.Abs(fe.Width - oldWidth) < 0.1 || Math.Abs(fe.Width - (oldWidth - 10)) < 0.1))
                                {
                                    fe.Width = maxOpt.Value;
                                }
                            }
                        }
                    }
                }
            }
        }

        private static async Task AddNode(Paragraph currentParagraph, INode node, RichTextBlock root, double width, int listLevel, Dictionary<ChatMessageAttachment, string> attachments)
        {
            switch (node.NodeType)
            {
                case NodeType.Text:
                    var text = NormalizeWhitespace(node.Text());
                    if (!string.IsNullOrEmpty(text))
                    {
                        currentParagraph.Inlines.Add(new Run { Text = text });
                    }
                    return;

                case NodeType.Element:
                    var el = (IElement)node;
                    // AdaptiveCards first (block level)
                    if (TryRenderAdaptiveCard(el, width) is FrameworkElement card)
                    {
                        var para = EnsureNewParagraph(root, currentParagraph);
                        var container = new InlineUIContainer { Child = card };
                        para.Inlines.Add(container);
                        return;
                    }

                    // Handle elements
                    switch (el.TagName)
                    {
                        case "P":
                        {
                            // Skip paragraphs that contain only whitespace or \u00A0
                            var raw = el.TextContent ?? string.Empty;
                            var onlySpaces = string.IsNullOrWhiteSpace(raw.Replace("\u00A0", " "));
                            if (onlySpaces)
                            {
                                return;
                            }
                            currentParagraph = NewParagraph(root);
                            await AddChildren(currentParagraph, el, root, width, listLevel, attachments);
                            // Add a modest spacer after paragraphs for better rhythm
                            var spacer = NewParagraph(root);
                            spacer.Inlines.Add(new LineBreak());
                            return;
                        }
                        case "BR":
                            currentParagraph.Inlines.Add(new LineBreak());
                            return;
                        case "A":
                            var link = new Hyperlink();
                            // NavigateUri where possible
                            var href = el.GetAttribute("href");
                            if (Uri.TryCreate(href, UriKind.Absolute, out var uri))
                            {
                                link.NavigateUri = uri;
                            }
                            await AddChildren(link, el, root, width, listLevel, attachments);
                            currentParagraph.Inlines.Add(link);
                            return;
                        case "H1":
                        case "H2":
                        case "H3":
                        case "H4":
                        case "H5":
                        case "H6":
                        {
                            var paraH = NewParagraph(root);
                            var tbH = new TextBlock
                            {
                                TextWrapping = TextWrapping.WrapWholeWords,
                                Width = double.NaN,
                                HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch,
                                Margin = new Thickness(0, 8, 0, 4)
                            };
                            var _mxH = SafeMaxWidth(width);
                            if (_mxH.HasValue) tbH.MaxWidth = _mxH.Value;
                            var tempRtbH = new RichTextBlock();
                            var tempPH = new Paragraph();
                            tempRtbH.Blocks.Add(tempPH);
                            await AddChildren(tempPH, el, tempRtbH, width, listLevel, attachments);
                            foreach (var i in tempPH.Inlines.ToList())
                            {
                                tempPH.Inlines.Remove(i);
                                tbH.Inlines.Add(i);
                            }
                            // Smaller heading mapping for H1-H3 to better match Teams/Fluent scale
                            var tag = el.TagName;
                            var app = Microsoft.UI.Xaml.Application.Current;
                            bool styled = false;
                            if (app != null)
                            {
                                string styleKey = tag switch
                                {
                                    "H1" => "SubtitleTextBlockStyle",
                                    "H2" => "BodyStrongTextBlockStyle",
                                    "H3" => "BodyTextBlockStyle",
                                    _ => null
                                };
                                if (styleKey != null && app.Resources.TryGetValue(styleKey, out var obj) && obj is Style st)
                                {
                                    tbH.Style = st; styled = true;
                                }
                            }
                            if (!styled)
                            {
                                // Fallback explicit sizes
                                double baseSize = tbH.FontSize; // inherited
                                tbH.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
                                tbH.FontSize = tag switch
                                {
                                    "H1" => baseSize + 6,
                                    "H2" => baseSize + 3,
                                    "H3" => baseSize + 1,
                                    _ => baseSize
                                };
                            }
                            paraH.Inlines.Add(new InlineUIContainer { Child = tbH });
                            return;
                        }
                        case "HR":
                        {
                            var paraHr = EnsureNewParagraph(root, currentParagraph);
                            var line = new Border
                            {
                                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(64, 0, 0, 0)),
                                Height = 1,
                                Width = double.NaN,
                                HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch,
                                Margin = new Thickness(0, 8, 0, 8)
                            };
                            var _mxHr = SafeMaxWidth(width);
                            if (_mxHr.HasValue) line.MaxWidth = _mxHr.Value;
                            paraHr.Inlines.Add(new InlineUIContainer { Child = line });
                            return;
                        }
                        case "STRONG":
                        case "B":
                            var bold = new Bold();
                            await AddChildren(bold, el, root, width, listLevel, attachments);
                            currentParagraph.Inlines.Add(bold);
                            return;
                        case "EM":
                        case "I":
                            var italic = new Italic();
                            await AddChildren(italic as Span, el, root, width, listLevel, attachments);
                            currentParagraph.Inlines.Add(italic);
                            return;
                        case "U":
                            var underline = new Underline();
                            await AddChildren(underline as Span, el, root, width, listLevel, attachments);
                            currentParagraph.Inlines.Add(underline);
                            return;
                        case "S":
                        case "DEL":
                            var strike = new Span { TextDecorations = TextDecorations.Strikethrough };
                            await AddChildren(strike, el, root, width, listLevel, attachments);
                            currentParagraph.Inlines.Add(strike);
                            return;
                        case "SPAN":
                        {
                            var style = el.GetStyle();
                            var bg = style.GetBackgroundColor();
                            var fontSizeStr = style.GetFontSize();
                            var hasBg = !string.IsNullOrWhiteSpace(bg) && !bg.Equals("inherit", StringComparison.OrdinalIgnoreCase);
                            if (hasBg)
                            {
                                var border = new Border
                                {
                                    Background = new SolidColorBrush(ToColor(bg)),
                                    CornerRadius = new CornerRadius(2),
                                    Padding = new Thickness(2)
                                };
                                var inner = new RichTextBlock { TextWrapping = TextWrapping.WrapWholeWords };
                                var ip = new Paragraph();
                                inner.Blocks.Add(ip);
                                // preserve inline content inside
                                await AddChildren(ip, el, inner, width - 4, listLevel, attachments);
                                // apply foreground / size to inner via container span at top
                                if (!string.IsNullOrWhiteSpace(style.GetColor()))
                                {
                                    inner.Foreground = new SolidColorBrush(ToColor(style.GetColor()));
                                }
                                if (!string.IsNullOrWhiteSpace(fontSizeStr))
                                {
                                    inner.FontSize = MapCssFontSize(fontSizeStr, inner.FontSize);
                                }
                                border.Child = inner;
                                currentParagraph.Inlines.Add(new InlineUIContainer { Child = border });
                            }
                            else
                            {
                                var span = new Span();
                                ApplyInlineStyles(span, el);
                                await AddChildren(span, el, root, width, listLevel, attachments);
                                currentParagraph.Inlines.Add(span);
                            }
                            return;
                        }
                        case "BLOCKQUOTE":
                        {
                            var quotePara = NewParagraph(root);
                            var border = new Border
                            {
                                Background = QuoteBrush ?? new SolidColorBrush(Windows.UI.Color.FromArgb(16, 0, 0, 0)),
                                BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(32, 0, 0, 0)),
                                BorderThickness = new Thickness(1),
                                CornerRadius = new CornerRadius(6),
                                Padding = new Thickness(8),
                                Width = double.NaN,
                                HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch
                            };
                            var _mxQuote = SafeMaxWidth(width);
                            if (_mxQuote.HasValue) border.MaxWidth = _mxQuote.Value;
                            // Preserve block/inline order inside quote by adding children to the last inner paragraph
                            var innerRtb = new RichTextBlock { TextWrapping = TextWrapping.WrapWholeWords };
                            var workingP = new Paragraph();
                            innerRtb.Blocks.Add(workingP);
                            foreach (var child in el.ChildNodes)
                            {
                                // always target the last paragraph to keep sequence
                                if (innerRtb.Blocks.LastOrDefault() is Paragraph lastP)
                                    workingP = lastP;
                                await AddNode(workingP, child, innerRtb, width - 16, listLevel, attachments);
                            }
                            // remove a leading empty paragraph, if any
                            if (innerRtb.Blocks.FirstOrDefault() is Paragraph firstP && !firstP.Inlines.Any())
                            {
                                innerRtb.Blocks.Remove(firstP);
                            }
                            border.Child = innerRtb;
                            quotePara.Inlines.Add(new InlineUIContainer { Child = border });
                            return;
                        }
                        case "CODEBLOCK":
                        {
                            var para = NewParagraph(root);
                            try
                            {
                                var outer = new Border
                                {
                                    Background = new SolidColorBrush(Windows.UI.Color.FromArgb(12, 0, 0, 0)),
                                    BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(24, 0, 0, 0)),
                                    BorderThickness = new Thickness(1),
                                    CornerRadius = new CornerRadius(4),
                                    Width = double.NaN,
                                    HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch
                                };
                                var _mxCode = SafeMaxWidth(width);
                                if (_mxCode.HasValue) outer.MaxWidth = _mxCode.Value;

                                var panel = new StackPanel {Spacing = 0};

                                // Header with language label and Copy button
                                var header = new Grid
                                {
                                    Background = new SolidColorBrush(Windows.UI.Color.FromArgb(16, 0, 0, 0)),
                                    Padding = new Thickness(8, 6, 8, 6)
                                };
                                header.ColumnDefinitions.Add(new Microsoft.UI.Xaml.Controls.ColumnDefinition());
                                header.ColumnDefinitions.Add(new Microsoft.UI.Xaml.Controls.ColumnDefinition
                                    {Width = new GridLength(80)});
                                var lang = el.GetAttribute("class");
                                var langLabel = new TextBlock
                                {
                                    Text = string.IsNullOrWhiteSpace(lang) ? "Code" : lang,
                                    Opacity = 0.8
                                };
                                Grid.SetColumn(langLabel, 0);
                                var copyBtn = new Button
                                {
                                    Content = "Copy", HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Right
                                };
                                Grid.SetColumn(copyBtn, 1);
                                header.Children.Add(langLabel);
                                header.Children.Add(copyBtn);

                                var codeText = el.TextContent?.Replace("\r\n", "\n") ?? string.Empty;

                                // Body scrollable monospace
                                var bodyBorder = new Border {Padding = new Thickness(8, 6, 8, 8)};
                                var tb = new TextBlock
                                {
                                    FontFamily = GetMonospaceFont(),
                                    TextWrapping = TextWrapping.NoWrap,
                                    IsTextSelectionEnabled = true
                                };
                                tb.Text = codeText;
                                var scroll = new ScrollViewer
                                {
                                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                                    VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                                    HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch,
                                    MinWidth = 0,
                                    Content = tb
                                };
                                bodyBorder.Child = scroll;

                                // Copy handler
                                copyBtn.Click += (s, e2) =>
                                {
                                    try
                                    {
                                        var dp = new DataPackage();
                                        dp.SetText(codeText);
                                        Clipboard.SetContent(dp);
                                    }
                                    catch
                                    {
                                    }
                                };

                                panel.Children.Add(header);
                                panel.Children.Add(bodyBorder);
                                outer.Child = panel;

                                para.Inlines.Add(new InlineUIContainer {Child = outer});
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine(e);
                            }

                            return;
                            
                        }
                        case "PRE":
                        {
                            var codePara = NewParagraph(root);
                            var border = new Border
                            {
                                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(12, 0, 0, 0)),
                                BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(24, 0, 0, 0)),
                                BorderThickness = new Thickness(1),
                                CornerRadius = new CornerRadius(4),
                                Padding = new Thickness(8),
                                Width = double.NaN,
                                HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch
                            };
                            var _mxPre = SafeMaxWidth(width);
                            if (_mxPre.HasValue) border.MaxWidth = _mxPre.Value;
                            var tb = new TextBlock
                            {
                                FontFamily = GetMonospaceFont(),
                                TextWrapping = TextWrapping.NoWrap,
                                IsTextSelectionEnabled = true
                            };
                            tb.Text = el.TextContent?.Replace("\r\n", "\n");
                            var scroll = new ScrollViewer
                            {
                                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                                Content = tb
                            };
                            border.Child = scroll;
                            codePara.Inlines.Add(new InlineUIContainer { Child = border });
                            return;
                        }
                        case "CODE":
                        {
                            // Inline code with subtle background and monospace font
                            var border = new Border
                            {
                                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(20, 0, 0, 0)),
                                CornerRadius = new CornerRadius(3),
                                Padding = new Thickness(2, 0, 2, 0)
                            };
                            var tbInline = new TextBlock
                            {
                                FontFamily = GetMonospaceFont(),
                                TextWrapping = TextWrapping.NoWrap
                            };
                            // Build inline text content
                            var temp = new RichTextBlock();
                            var tp = new Paragraph();
                            temp.Blocks.Add(tp);
                            await AddChildren(tp, el, temp, width, listLevel, attachments);
                            tbInline.Inlines.Clear();
                            foreach (var i in tp.Inlines.ToList())
                            {
                                tp.Inlines.Remove(i);
                                tbInline.Inlines.Add(i);
                            }
                            border.Child = tbInline;
                            currentParagraph.Inlines.Add(new InlineUIContainer { Child = border });
                            return;
                        }
                        case "IMG":
                        {
                            var paraImg = EnsureNewParagraph(root, currentParagraph);
                            await AddImage(paraImg, el, width);
                            // small spacer after image
                            var spacer = NewParagraph(root);
                            spacer.Inlines.Add(new LineBreak());
                            return;
                        }
                        case "ATTACHMENT":
                            await AddAttachment(currentParagraph, el, width, attachments);
                            return;
                        case "UL":
                            await ProcessList(el, currentParagraph, root, width, listLevel, false, attachments);
                            return;
                        case "OL":
                            await ProcessList(el, currentParagraph, root, width, listLevel, true, attachments);
                            return;
                        case "TABLE":
                        {
                            var para = NewParagraph(root);
                            var container = new Grid
                            {
                                Width = double.NaN,
                                HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch
                            };
                            var _mxTable = SafeMaxWidth(width);
                            if (_mxTable.HasValue) container.MaxWidth = _mxTable.Value;
                            // Collect rows and columns
                            var rows = el.QuerySelectorAll("tr").ToList();
                            int cols = rows.Select(r => r.Children.Count(cn => cn.TagName == "TD" || cn.TagName == "TH")).DefaultIfEmpty(0).Max();
                            for (int c = 0; c < cols; c++) container.ColumnDefinitions.Add(new Microsoft.UI.Xaml.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                            for (int r = 0; r < rows.Count; r++) container.RowDefinitions.Add(new Microsoft.UI.Xaml.Controls.RowDefinition { Height = GridLength.Auto });

                            for (int r = 0; r < rows.Count; r++)
                            {
                                var row = rows[r];
                                int c = 0;
                                foreach (var cell in row.Children.Where(cn => cn.TagName == "TD" || cn.TagName == "TH"))
                                {
                                    var cellBorder = new Border
                                    {
                                        BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(32, 0, 0, 0)),
                                        BorderThickness = new Thickness(0.5),
                                        Padding = new Thickness(8)
                                    };
                                    var cellRtb = new RichTextBlock { TextWrapping = TextWrapping.WrapWholeWords };
                                    var cp = new Paragraph();
                                    cellRtb.Blocks.Add(cp);
                                    await AddChildren(cp, (IElement)cell, cellRtb, width - 16, listLevel, attachments);
                                    cellBorder.Child = cellRtb;
                                    Grid.SetRow(cellBorder, r);
                                    Grid.SetColumn(cellBorder, c);
                                    container.Children.Add(cellBorder);
                                    c++;
                                }
                            }
                            para.Inlines.Add(new InlineUIContainer { Child = container });
                            return;
                        }
                        case "EMOJI":
                        {
                            var alt = el.GetAttribute("alt");
                            var title = el.GetAttribute("title");
                            var emojiGlyph = !string.IsNullOrWhiteSpace(alt) ? alt : (!string.IsNullOrWhiteSpace(title) ? title : el.TextContent);
                            if (!string.IsNullOrWhiteSpace(emojiGlyph))
                            {
                                var tbEmoji = new TextBlock
                                {
                                    Text = emojiGlyph,
                                    FontFamily = new FontFamily("Segoe UI Emoji")
                                };
                                currentParagraph.Inlines.Add(new InlineUIContainer { Child = tbEmoji });
                            }
                            return;
                        }
                        default:
                            // Fallback: process children inline
                            await AddChildren(currentParagraph, el, root, width, listLevel, attachments);
                            return;
                    }
            }
        }

        private static async Task AddChildren(Paragraph paragraph, IElement element, RichTextBlock root, double width, int listLevel, Dictionary<ChatMessageAttachment, string> attachments)
        {
            foreach (var child in element.ChildNodes)
                await AddNode(paragraph, child, root, width, listLevel, attachments);
        }

        private static async Task AddChildren(Span span, IElement element, RichTextBlock root, double width, int listLevel, Dictionary<ChatMessageAttachment, string> attachments)
        {
            foreach (var child in element.ChildNodes)
            {
                // For simplicity, only text and nested inline spans into the span
                if (child.NodeType == NodeType.Text)
                {
                    var text = NormalizeWhitespace(child.Text());
                    if (!string.IsNullOrEmpty(text)) span.Inlines.Add(new Run { Text = text });
                }
                else if (child.NodeType == NodeType.Element)
                {
                    var el = (IElement)child;
                    var tempPara = new Paragraph();
                    await AddNode(tempPara, el, root, width, listLevel, attachments);
                    foreach (var inline in tempPara.Inlines.ToList())
                    {
                        tempPara.Inlines.Remove(inline);
                        span.Inlines.Add(inline);
                    }
                }
            }
        }

        private static async Task ProcessList(IElement listNode, Paragraph currentParagraph, RichTextBlock root, double width, int level, bool ordered, Dictionary<ChatMessageAttachment, string> attachments)
        {
            int index = 1;
            foreach (var li in listNode.Children.Where(c => c.TagName == "LI"))
            {
                var p = NewParagraph(root);
                // Increase indentation per level for better clarity
                var indent = new string('\u00A0', Math.Max(0, level) * 4);
                string bullet = ordered ? $"{index}. " : "• ";
                p.Inlines.Add(new Run { Text = indent + bullet });
                foreach (var child in li.ChildNodes)
                {
                    await AddNode(p, child, root, width, level + 1, attachments);
                }
                index++;
            }
            // Add a blank paragraph after a top-level list only (avoid big gaps after indented lists)
            if (level == 0)
            {
                var spacer = NewParagraph(root);
                spacer.Inlines.Add(new LineBreak());
            }
        }

        private static async Task AddImage(Paragraph currentParagraph, IElement el, double width)
        {
            var img = new Microsoft.UI.Xaml.Controls.Image();
            img.Stretch = Stretch.Uniform;

            // width/height
            double.TryParse((el.GetAttribute("width") ?? el.GetStyle().GetWidth() ?? string.Empty).Replace("px", string.Empty), out var w);
            double.TryParse((el.GetAttribute("height") ?? el.GetStyle().GetHeight() ?? string.Empty).Replace("px", string.Empty), out var h);
            if (w > 0) img.Width = w; else { img.Width = double.NaN; img.HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch; var _mxImg = SafeMaxWidth(width); if (_mxImg.HasValue) img.MaxWidth = _mxImg.Value; }
            if (h > 0) img.Height = h;

            var src = el.GetAttribute("src");
            if (!string.IsNullOrWhiteSpace(src))
            {
                if (src.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
                {
                    var match = Regex.Match(src, @"^data:image\/[a-zA-Z0-9.+-]+;base64,(?<data>.+)$");
                    if (match.Success)
                    {
                        var base64 = match.Groups["data"].Value;
                        var bytes = Convert.FromBase64String(base64);
                        using var mem = new InMemoryRandomAccessStream();
                        using (var writer = new DataWriter(mem))
                        {
                            writer.WriteBytes(bytes);
                            await writer.StoreAsync();
                            await writer.FlushAsync();
                            mem.Seek(0);
                        }
                        var bmp = new BitmapImage();
                        await bmp.SetSourceAsync(mem);
                        img.Source = bmp;
                    }
                }
                else if (IsGraphUrl(src))
                {
                    var bmp = await LoadBitmapFromGraph(src);
                    if (bmp != null)
                    {
                        img.Source = bmp;
                    }
                }
                else if (Uri.TryCreate(src, UriKind.Absolute, out var uri))
                {
                    img.Source = new BitmapImage(uri);
                }
                // Teams-only: ignore other external hosts
            }

            currentParagraph.Inlines.Add(new InlineUIContainer { Child = img });
        }

        private static bool IsGraphUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
            var host = uri.Host.ToLowerInvariant();
            return host.EndsWith("graph.microsoft.com") || host.EndsWith("graph.microsoft.us") || host.EndsWith("graph.microsoft.de") || host.EndsWith("graph.microsoft.cn");
        }

        private static async Task<BitmapImage> LoadBitmapFromGraph(string url)
        {
            try
            {
                var graphClient = await AuthenticationService.GetGraphService();
                await using (var stream = await graphClient
                    .Teams["t"].Channels["c"].Messages["m"].HostedContents["h"].Content
                    .WithUrl(url)
                    .GetAsync())
                {
                    var mem = new InMemoryRandomAccessStream();
                    var output = mem.AsStreamForWrite();
                    await stream.CopyToAsync(output);
                    await output.FlushAsync();
                    mem.Seek(0);
                    var bmp = new BitmapImage();
                    await bmp.SetSourceAsync(mem);
                    return bmp;
                }
            }
            catch
            {
                return null;
            }
        }

        private static async Task AddAttachment(Paragraph currentParagraph, IElement el, double width, Dictionary<ChatMessageAttachment, string> attachments)
        {
            if (attachments == null) return;
            var id = el.GetAttribute("id");
            if (string.IsNullOrWhiteSpace(id)) return;
            var pair = attachments.FirstOrDefault(a => a.Key.Id == id);
            if (pair.Key == null) return;

            var contentType = pair.Key.ContentType;
            if (string.Equals(contentType, "application/vnd.microsoft.card.adaptive", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var parsed = AdaptiveCard.FromJsonString(pair.Key.Content);
                    if (parsed?.AdaptiveCard != null)
                    {
                        var renderer = new AdaptiveCardRenderer();
                        var rendered = renderer.RenderAdaptiveCard(parsed.AdaptiveCard);
                        if (rendered?.FrameworkElement is FrameworkElement fe)
                        {
                            fe.Width = double.NaN;
                            fe.HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch;
                            var _mxAc = SafeMaxWidth(width);
                            if (_mxAc.HasValue) fe.MaxWidth = _mxAc.Value;
                            currentParagraph.Inlines.Add(new InlineUIContainer { Child = fe });
                        }
                    }
                }
                catch
                {
                    // ignore
                }
                return;
            }

            if (string.Equals(contentType, "application/vnd.microsoft.card.codesnippet", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var language = "";
                    try
                    {
                        var json = JsonObject.Parse(pair.Key.Content);
                        if (json.ContainsKey("language")) language = json.GetNamedString("language", "");
                    }
                    catch { }

                    var stack = new StackPanel { Spacing = 4, Width = double.NaN, HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch };
                    var _mxCs = SafeMaxWidth(width);
                    if (_mxCs.HasValue) stack.MaxWidth = _mxCs.Value;
                    if (!string.IsNullOrEmpty(language))
                    {
                        stack.Children.Add(new TextBlock { Text = language, Opacity = 0.7 });
                    }
                    var code = new TextBlock
                    {
                        Text = pair.Value ?? string.Empty,
                        FontFamily = new FontFamily("Consolas"),
                        TextWrapping = TextWrapping.Wrap
                    };
                    stack.Children.Add(code);
                    currentParagraph.Inlines.Add(new InlineUIContainer { Child = stack });
                }
                catch { }
                return;
            }

            // Unsupported attachment type — simple italic placeholder
            var placeholder = new Run
            {
                Text = $"Unsupported attachment ({contentType})",
                FontStyle = Windows.UI.Text.FontStyle.Italic
            };
            currentParagraph.Inlines.Add(placeholder);
        }

        private static FrameworkElement TryRenderAdaptiveCard(IElement el, double width)
        {
            // Common patterns: <script type="application/adaptivecard+json">{...}</script>
            // or any element with data containing a JSON that parses as AdaptiveCard
            if (el.TagName == "SCRIPT" &&
                string.Equals(el.GetAttribute("type"), "application/adaptivecard+json", StringComparison.OrdinalIgnoreCase))
            {
                var json = el.TextContent?.Trim();
                return RenderAdaptiveCard(json, width);
            }

            if (el.TagName == "ADAPTIVECARD")
            {
                var json = el.TextContent?.Trim();
                return RenderAdaptiveCard(json, width);
            }

            // Attribute-based hint
            var acJson = el.GetAttribute("data-adaptivecard-json");
            if (!string.IsNullOrWhiteSpace(acJson))
            {
                return RenderAdaptiveCard(acJson, width);
            }

            return null;
        }

        private static FrameworkElement RenderAdaptiveCard(string json, double width)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                var parse = AdaptiveCard.FromJsonString(json);
                if (parse?.AdaptiveCard == null) return null;
                var renderer = new AdaptiveCardRenderer();
                var rendered = renderer.RenderAdaptiveCard(parse.AdaptiveCard);
                var element = rendered?.FrameworkElement as FrameworkElement;
                if (element != null)
                {
                    element.Width = Math.Max(width - 10, 0);
                }
                return element;
            }
            catch
            {
                return null;
            }
        }

        private static Paragraph NewParagraph(RichTextBlock root)
        {
            var p = new Paragraph();
            root.Blocks.Add(p);
            return p;
        }

        private static Paragraph EnsureNewParagraph(RichTextBlock root, Paragraph current)
        {
            if (!current.Inlines.Any()) return current;
            return NewParagraph(root);
        }

        private static string NormalizeWhitespace(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            // preserve single spaces but collapse runs; keep newlines minimal, RichTextBlock handles breaks
            var collapsed = Regex.Replace(s, "\r?\n", " ");
            collapsed = Regex.Replace(collapsed, "[\t ]+", " ");
            return collapsed;
        }

        private static void ApplyInlineStyles(Span span, IElement el)
        {
            var style = el.GetStyle();
            // color
            var color = style.GetColor();
            if (!string.IsNullOrWhiteSpace(color) && !color.Equals("inherit", StringComparison.OrdinalIgnoreCase))
            {
                try { span.Foreground = new SolidColorBrush(ToColor(color)); } catch { }
            }
            // font-size
            var fs = style.GetFontSize();
            if (!string.IsNullOrWhiteSpace(fs) && !fs.Equals("inherit", StringComparison.OrdinalIgnoreCase))
            {
                try { span.FontSize = MapCssFontSize(fs, span.FontSize > 0 ? span.FontSize : 14); } catch { }
            }
            var fontWeight = style.GetFontWeight();
            if (!string.IsNullOrWhiteSpace(fontWeight) && fontWeight.Equals("bold", StringComparison.OrdinalIgnoreCase))
            {
                span.FontWeight = Microsoft.UI.Text.FontWeights.Bold;
            }
            var fontStyle = style.GetFontStyle();
            if (!string.IsNullOrWhiteSpace(fontStyle) && fontStyle.Equals("italic", StringComparison.OrdinalIgnoreCase))
            {
                span.FontStyle = Windows.UI.Text.FontStyle.Italic;
            }
            var textDecoration = style.GetTextDecorationLine();
            if (!string.IsNullOrWhiteSpace(textDecoration))
            {
                if (textDecoration.Contains("underline", StringComparison.OrdinalIgnoreCase))
                    span.TextDecorations |= TextDecorations.Underline;
                if (textDecoration.Contains("line-through", StringComparison.OrdinalIgnoreCase))
                    span.TextDecorations |= TextDecorations.Strikethrough;
            }
        }

        private static double MapCssFontSize(string css, double baseSize)
        {
            if (string.IsNullOrWhiteSpace(css)) return baseSize;
            css = css.Trim().ToLowerInvariant();
            // Keyword mapping relative to base
            switch (css)
            {
                case "xx-small": return Math.Max(8, baseSize * 0.6);
                case "x-small": return Math.Max(9, baseSize * 0.75);
                case "small": return Math.Max(10, baseSize * 0.9);
                case "medium": return baseSize;
                case "large": return baseSize * 1.25;
                case "x-large": return baseSize * 1.5;
                case "xx-large": return baseSize * 2.0;
                case "smaller": return baseSize * 0.9;
                case "larger": return baseSize * 1.1;
            }
            // Units
            var numMatch = Regex.Match(css, @"^([\d.]+)\s*(px|pt|em|rem|%)$");
            if (numMatch.Success)
            {
                var val = double.Parse(numMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                var unit = numMatch.Groups[2].Value;
                switch (unit)
                {
                    case "px": return val; // 1 px ≈ 1 device independent pixel in WinUI
                    case "pt": return val * 96.0 / 72.0;
                    case "em": return baseSize * val;
                    case "rem": return 14 * val; // assume 14 as root
                    case "%": return baseSize * (val / 100.0);
                }
            }
            // Fallback try plain number
            if (double.TryParse(css, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                return parsed;
            return baseSize;
        }

        private static FontFamily GetMonospaceFont()
        {
            // Prefer Cascadia Code (Windows 11 default), fallback to Consolas and Courier New
            return new FontFamily("Cascadia Code, Consolas, Courier New");
        }

        private static Windows.UI.Color ToColor(string cssColor)
        {
            // Very small parser for #RRGGBB or rgb(a)
            cssColor = cssColor.Trim();
            if (cssColor.StartsWith("#"))
            {
                var hex = cssColor.Substring(1);
                if (hex.Length == 3)
                {
                    var r = Convert.ToByte(new string(hex[0], 2), 16);
                    var g = Convert.ToByte(new string(hex[1], 2), 16);
                    var b = Convert.ToByte(new string(hex[2], 2), 16);
                    return Windows.UI.Color.FromArgb(255, r, g, b);
                }
                if (hex.Length == 6)
                {
                    var r = Convert.ToByte(hex.Substring(0, 2), 16);
                    var g = Convert.ToByte(hex.Substring(2, 2), 16);
                    var b = Convert.ToByte(hex.Substring(4, 2), 16);
                    return Windows.UI.Color.FromArgb(255, r, g, b);
                }
            }
            var m = Regex.Match(cssColor, @"rgba?\((?<r>\d+),\s*(?<g>\d+),\s*(?<b>\d+)(?:,\s*(?<a>[\d.]+))?\)");
            if (m.Success)
            {
                byte r = byte.Parse(m.Groups["r"].Value);
                byte g = byte.Parse(m.Groups["g"].Value);
                byte b = byte.Parse(m.Groups["b"].Value);
                byte a = 255;
                if (m.Groups["a"].Success)
                {
                    var af = double.Parse(m.Groups["a"].Value, CultureInfo.InvariantCulture);
                    a = (byte)Math.Round(af * 255);
                }
                return Windows.UI.Color.FromArgb(a, r, g, b);
            }
            // Fallback black
            return Windows.UI.Color.FromArgb(255, 0, 0, 0);
        }

        private static void TrimTrailingBreaks(RichTextBlock richText)
        {
            if (richText.Blocks.LastOrDefault() is Paragraph p)
            {
                // Remove trailing LineBreak(s)
                while (p.Inlines.LastOrDefault() is LineBreak)
                {
                    p.Inlines.Remove(p.Inlines.Last());
                }
            }
        }
    }
}
