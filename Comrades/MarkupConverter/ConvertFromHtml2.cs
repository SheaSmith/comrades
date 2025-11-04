using AdaptiveCards;
using AdaptiveCards.Rendering;
using AngleSharp;
using AngleSharp.Css.Dom;
using AngleSharp.Dom;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using CommunityToolkit.WinUI.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Text;
using ColumnDefinition = Microsoft.UI.Xaml.Controls.ColumnDefinition;
using Image = Microsoft.UI.Xaml.Controls.Image;
using ColorCode;
using Comrades.Services;
using System.Net.Http;
using Windows.Data.Json;
using Windows.Storage.Streams;
using AdaptiveCards.ObjectModel.WinUI3;
using AdaptiveCards.Rendering.WinUI3;
using Microsoft.Graph;
using Microsoft.Graph.Beta;
using Microsoft.Graph.Beta.Models;

namespace Comrades.MarkupConverter
{
    public static class ConvertFromHtml2
    {
        private static readonly List<INode> ElementStack = [];
        private static Paragraph _currentParagraph = new();
        private static InlineCollection _lastInlines = _currentParagraph.Inlines;
        public static SolidColorBrush QuoteBrush { get; set; }

        private static int _listIndex = 1;

        public static async Task FromHtml(string html, RichTextBlock richText, double width,
            Dictionary<ChatMessageAttachment, string> attachments)
        {
            richText.Blocks.Clear();

            var config = AngleSharp.Configuration.Default.WithCss();

            var context = BrowsingContext.New(config);

            var document = await context.OpenAsync(req => req.Content(html));

            await ParseElements(document.GetElementsByTagName("BODY").First().ChildNodes.ToList(), false, richText,
                width, TextAlignment.Start, attachments);
            TrimBlock(richText);
        }


        public static void UpdateSize(RichTextBlock richText, double width, double oldWidth)
        {
            foreach (var block in richText.Blocks)
            {
                if (block is Paragraph paragraph)
                {
                    foreach (var inline in paragraph.Inlines)
                    {
                        if (inline is not InlineUIContainer inlineUi) continue;

                        if (inlineUi.Child is not FrameworkElement frameworkElement) continue;

                        if (frameworkElement!.Width.Equals(oldWidth) || frameworkElement.Width == oldWidth - 10)
                        {
                            frameworkElement.Width = Math.Max(width - 10, 0);
                        }
                    }
                }
            }
        }


        private static async Task ParseElements(List<INode> nodes, bool isNewParagraph, RichTextBlock richText,
            double width, TextAlignment textAlignment, Dictionary<ChatMessageAttachment, string> attachments,
            bool? orderedList = null, int listLevel = 0)
        {
            foreach (var node in nodes)
            {
                if (node.NodeType == NodeType.Element)
                {
                    var element = node as IElement;

                    if (element.TagName == "A")
                    {
                        var hyperlink = new Hyperlink();
                        _lastInlines.Add(hyperlink);
                        await ProcessSpan(element, hyperlink, richText, width, isNewParagraph, textAlignment,
                            attachments);
                    }
                    else if (element.TagName == "STRONG")
                    {
                        var bold = new Bold();
                        _lastInlines.Add(bold);
                        await ProcessSpan(element, bold, richText, width, isNewParagraph, textAlignment, attachments);
                    }
                    else if (element.TagName == "EM")
                    {
                        var italic = new Italic();
                        _lastInlines.Add(italic);
                        await ProcessSpan(element, italic, richText, width, isNewParagraph, textAlignment, attachments);
                    }
                    else if (element.TagName == "U")
                    {
                        var underline = new Underline();
                        _lastInlines.Add(underline);
                        await ProcessSpan(element, underline, richText, width, isNewParagraph, textAlignment,
                            attachments);
                    }
                    else if (element.TagName == "S")
                    {
                        var strikethrough = new Span
                        {
                            TextDecorations = TextDecorations.Strikethrough
                        };
                        _lastInlines.Add(strikethrough);
                        await ProcessSpan(element, strikethrough, richText, width, isNewParagraph, textAlignment,
                            attachments);
                    }
                    else if (element.TagName == "SPAN" || element.TagName == "P")
                    {
                        if (element.TagName == "P")
                        {
                            await CreateParagraph(element, richText, width, true, textAlignment, attachments);
                        }

                        var span = new Span();

                        var styles = element.GetStyle();
                        var highlightColor = styles.GetBackgroundColor();

                        var fontColor = styles.GetColor();

                        if (!string.IsNullOrEmpty(fontColor))
                        {
                            var colors = fontColor.Replace("rgba(", "").Replace(")", "").Split(", ");
                            var color = Color.FromArgb(
                                Convert.ToByte(Decimal.ToUInt16((Decimal.Parse(colors[3]) * 255))),
                                Byte.Parse(colors[0]), Byte.Parse(colors[1]), Byte.Parse(colors[2]));
                            var brush = new SolidColorBrush(color);
                            span.Foreground = brush;
                        }

                        var textSize = styles.GetFontSize();

                        if (!string.IsNullOrEmpty(textSize))
                        {
                            textSize = Regex.Replace(textSize, @"\s+", "");

                            var unitMatch = Regex.Match(textSize, @"([\d\.]+)((?:px)|(?:em)|(?:rem)|(?:pt))");

                            double textSizeValue = richText.FontSize;

                            if (unitMatch.Success)
                            {
                                var unit = unitMatch.Groups[2].Value;
                                var value = double.Parse(unitMatch.Groups[1].Value);

                                if (unit == "px")
                                {
                                    textSizeValue = value;
                                }
                                else if (unit == "rem" || unit == "em")
                                {
                                    textSizeValue = textSizeValue * value;
                                }
                                else if (unit == "pt")
                                {
                                    textSizeValue = value * 1.333333;
                                }
                            }
                            else if (textSize == "x-large")
                            {
                                textSizeValue = 18;
                            }
                            else if (textSize == "xx-small")
                            {
                                textSizeValue = 12;
                            }

                            span.FontSize = textSizeValue;
                        }

                        var fontFamily = element.GetStyle().GetFontFamily();

                        if (!string.IsNullOrEmpty(fontFamily))
                        {
                            var font = fontFamily.Replace("\"", "").Replace("serif", "Cambira")
                                .Replace("sans-serif", "Segoe UI").Replace("monospace", "Consolas");
                            span.FontFamily = new FontFamily(font);
                        }


                        if (!string.IsNullOrEmpty(highlightColor))
                        {
                            var richTextBlock = new RichTextBlock();

                            var lastParagraph = _currentParagraph;
                            var lastInlines = _lastInlines;
                            await CreateParagraph(element, richTextBlock, width, false, textAlignment, attachments);
                            _currentParagraph = lastParagraph;
                            _lastInlines = lastInlines;

                            var colors = highlightColor.Replace("rgba(", "").Replace(")", "").Split(", ");
                            var color = Color.FromArgb(
                                Convert.ToByte(Decimal.ToUInt16((Decimal.Parse(colors[3]) * 255))),
                                Byte.Parse(colors[0]), Byte.Parse(colors[1]), Byte.Parse(colors[2]));

                            var border = new Border
                            {
                                VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Bottom,
                                Background = new SolidColorBrush(color),
                                Child = richTextBlock
                            };

                            var inlineUi = new InlineUIContainer
                            {
                                Child = border
                            };

                            _lastInlines.Add(inlineUi);
                        }
                        else
                        {
                            _lastInlines.Add(span);
                            await ProcessSpan(element, span, richText, width, isNewParagraph, textAlignment,
                                attachments);
                        }


                        if (element.TagName == "P")
                        {
                            await CreateParagraph(element, richText, width, true, textAlignment, attachments);
                        }
                    }
                    else if (element.TagName == "DIV")
                    {
                        await CreateParagraph(element, richText, width, false, textAlignment, attachments);
                        await CreateParagraph(element, richText, width, true, textAlignment, attachments);
                    }
                    else if (element.TagName == "BLOCKQUOTE")
                    {
                        await CreateParagraph(element, richText, width, true, textAlignment, attachments);

                        var inlineUiContainer = new InlineUIContainer();

                        var richTextBlock = new RichTextBlock();
                        var oldParagraph = _currentParagraph;
                        var lastInlines = _lastInlines;
                        await ParseElements(ElementStack, true, richTextBlock, width, textAlignment, attachments);
                        await ParseElements(element.ChildNodes.ToList(), false, richTextBlock, width, textAlignment,
                            attachments);
                        TrimBlock(richTextBlock);
                        _currentParagraph = oldParagraph;
                        _lastInlines = lastInlines;

                        var stackPanel = new StackPanel();
                        stackPanel.Children.Add(richTextBlock);
                        stackPanel.Padding = new Thickness(12, 12, 200, 12);
                        stackPanel.CornerRadius = new CornerRadius(8);
                        stackPanel.Background = QuoteBrush;
                        stackPanel.Width = width - 10;

                        inlineUiContainer.Child = stackPanel;
                        _lastInlines.Add(inlineUiContainer);

                        await CreateParagraph(element, richText, width, true, textAlignment, attachments);
                    }
                    else if (element.TagName == "H1" || element.TagName == "H2" || element.TagName == "H3")
                    {
                        await CreateParagraph(element, richText, width, true, textAlignment, attachments);
                        var span = new Span();

                        if (element.TagName == "H1")
                        {
                            span.FontSize = 40;
                        }
                        else if (element.TagName == "H2")
                        {
                            span.FontSize = 28;
                        }
                        else
                        {
                            span.FontSize = 20;
                        }

                        span.FontWeight = FontWeights.SemiBold;

                        _lastInlines.Add(span);
                        await ProcessSpan(element, span, richText, width, isNewParagraph, textAlignment, attachments);

                        await CreateParagraph(element, richText, width, true, textAlignment, attachments);
                    }
                    else if (element.TagName == "HR")
                    {
                        var horizontalRule = new Border
                        {
                            Width = width - 10,
                            BorderThickness = new Thickness(0.5),
                            Opacity = 0.5,
                            BorderBrush = richText.Foreground
                        };

                        var inlineUi = new InlineUIContainer
                        {
                            Child = horizontalRule
                        };

                        await CreateParagraph(element, richText, width, true, textAlignment, attachments);
                        _lastInlines.Add(inlineUi);
                        await CreateParagraph(element, richText, width, true, textAlignment, attachments);
                    }
                    else if (element.TagName == "TABLE")
                    {
                        var table = new StackPanel
                        {
                            Width = Math.Max(width - 10, 0)
                        };

                        var inlineUi = new InlineUIContainer
                        {
                            Child = table
                        };

                        await CreateParagraph(element, richText, width, true, textAlignment, attachments);
                        _lastInlines.Add(inlineUi);
                        await ProcessTable(element, table, richText.Foreground, textAlignment, width, attachments);
                        await CreateParagraph(element, richText, width, true, textAlignment, attachments);
                    }
                    else if (element.TagName == "IMG")
                    {
                        var image = new Image();

                        var url = element.GetAttribute("src");

                        if (url.StartsWith("https://graph.microsoft.com"))
                        {
                            var graphClient = await AuthenticationService.GetGraphService();
                            await using (var stream = await graphClient
                                             .Teams["test"]
                                             .Channels["test"]
                                             .Messages["test"]
                                             .HostedContents["test"]
                                             .Content
                                             .WithUrl(url)
                                             .GetAsync())
                            {
                                var bitmap = new BitmapImage();
                                try {
                                
                                        // 2. Copy to InMemoryRandomAccessStream
                                        var mem = new InMemoryRandomAccessStream();

                                        // Copy without using 'using' — keep both streams alive
                                        var output = mem.AsStreamForWrite();
                                        await stream.CopyToAsync(output);
                                        await output.FlushAsync(); // make sure all bytes are written
                                        mem.Seek(0); // reset position for BitmapImage

                                        mem.Seek(0);
                                        await bitmap.SetSourceAsync(mem);

                                        image.Source = bitmap;
                                }
                                catch (Exception e)
                                {
                                    Debugger.Log(0, "Test", e.StackTrace);
                                }
                            }
                        }
                        else
                        {
                            var bitmapImage = new BitmapImage
                            {
                                UriSource = new Uri(element.GetAttribute("src"))
                            };
                            image.Source = bitmapImage;
                        }

                        var imageWidthCss = element.GetAttribute("width") ?? element.GetStyle().GetWidth();
                        var imageWidth = 0.0;

                        if (imageWidthCss != null)
                        {
                            imageWidth = double.Parse(imageWidthCss.Replace("px", "").Trim());
                        }

                        var imageHeightCss = element.GetAttribute("height") ?? element.GetStyle().GetHeight();
                        var imageHeight = 0.0;

                        if (imageHeightCss != null)
                        {
                            imageHeight = double.Parse(imageHeightCss.Replace("px", "").Trim());
                        }

                        image.Height = imageHeight;
                        image.Width = imageWidth;

                        var inlineUi = new InlineUIContainer
                        {
                            Child = image
                        };

                        _lastInlines.Add(inlineUi);
                    }
                    else if (element.TagName == "UL")
                    {
                        await ParseElements(element.ChildNodes.ToList(), false, richText, width, textAlignment,
                            attachments, false, listLevel + 1);

                        if (listLevel == 0)
                        {
                            await CreateParagraph(element, richText, width, true, textAlignment, attachments);
                        }
                    }
                    else if (element.TagName == "OL")
                    {
                        var lastListIndex = _listIndex;
                        _listIndex = 1;
                        await ParseElements(element.ChildNodes.ToList(), false, richText, width, textAlignment,
                            attachments, true, listLevel + 1);
                        _listIndex = lastListIndex;

                        if (listLevel == 0)
                        {
                            await CreateParagraph(element, richText, width, true, textAlignment, attachments);
                        }
                    }
                    else if (element.TagName == "LI")
                    {
                        await CreateParagraph(element, richText, width, false, textAlignment, attachments, orderedList,
                            listLevel);
                        _listIndex++;
                    }
                    else if (element.TagName == "CODE")
                    {
                        var textBlock = new RichTextBlock
                        {
                            FontFamily = new FontFamily("Consolas")
                        };
                        var paragraph = new Paragraph();
                        var run = new Run
                        {
                            Text = element.TextContent
                        };
                        paragraph.Inlines.Add(run);
                        textBlock.Blocks.Add(paragraph);
                        textBlock.Padding = new Thickness(3);

                        var border = new Border
                        {
                            Background = QuoteBrush,
                            Child = textBlock
                        };

                        var inlineUi = new InlineUIContainer
                        {
                            Child = border
                        };
                        _lastInlines.Add(inlineUi);
                    }
                    else if (element.TagName == "PRE")
                    {
                        var textBlock = new RichTextBlock
                        {
                            FontFamily = new FontFamily("Consolas")
                        };
                        var paragraph = new Paragraph();
                        var run = new Run
                        {
                            Text = Regex.Replace(element.TextContent, @"\n$", "")
                        };
                        paragraph.Inlines.Add(run);
                        textBlock.Blocks.Add(paragraph);
                        textBlock.Padding = new Thickness(20, 10, 20, 10);

                        var border = new Border
                        {
                            Background = QuoteBrush,
                            Child = textBlock,
                            Width = width
                        };

                        var inlineUi = new InlineUIContainer
                        {
                            Child = border
                        };

                        await CreateParagraph(element, richText, width, true, textAlignment, attachments);

                        _lastInlines.Add(inlineUi);

                        await CreateParagraph(element, richText, width, true, textAlignment, attachments);
                    }
                    else if (element.TagName == "ATTACHMENT")
                    {
                        var id = element.GetAttribute("id");

                        var attachment = attachments.First(a => a.Key.Id == id);

                        if (attachment.Key.ContentType == "application/vnd.microsoft.card.adaptive")
                        {
                            var card = AdaptiveCard.FromJsonString(attachment.Key.Content);

                            var renderer = new AdaptiveCardRenderer();

                            // AdaptiveHostConfig hostConfig = new AdaptiveHostConfig()
                            // {
                            //     SupportsInteractivity = false,
                            //     ContainerStyles = new AdaptiveContainerStylesDefinition()
                            //     {
                            //         Default = new ContainerStyleConfig()
                            //         {
                            //             BackgroundColor = "#00000000",
                            //             ForegroundColors = new ForegroundColorsConfig()
                            //             {
                            //                 Default = new FontColorConfig(ColorHelper.ToHex(((SolidColorBrush)richText.Foreground).Color))
                            //             }
                            //         }
                            //     }
                            // };
                            //
                            // AdaptiveCardRenderer renderer = new AdaptiveCardRenderer(hostConfig);

                            RenderedAdaptiveCard renderedCard = renderer.RenderAdaptiveCard(card.AdaptiveCard);


                            var inlineUi = new InlineUIContainer
                            {
                                Child = renderedCard.FrameworkElement
                            };

                            await CreateParagraph(element, richText, width, true, textAlignment, attachments);
                            _lastInlines.Add(inlineUi);

                            // var paragraph = new Paragraph();
                            // var run = new Run();
                            // run.Text = "This message may have interactive elements which are not supported in Comrades. Please use the official Teams client to interact with this message.";
                            // run.FontStyle = Windows.UI.Text.FontStyle.Italic;
                            // paragraph.Inlines.Add(run);
                            // richText.Blocks.Add(paragraph);
                            //
                            // await CreateParagraph(element, richText, width, true, textAlignment, attachments);
                        }
                        else if (attachment.Key.ContentType == "application/vnd.microsoft.card.codesnippet")
                        {
                            var json = JsonObject.Parse(attachment.Key.Content);

                            var languageString = json.GetNamedString("language");
                            var displayName = languageString;

                            ILanguage? language = null;

                            if (languageString == "ASP")
                            {
                                language = Languages.Aspx;
                                displayName = "ASP.NET";
                            }
                            else if (languageString == "CSharp")
                            {
                                language = Languages.CSharp;
                                displayName = "C#";
                            }
                            else if (languageString == "CPP")
                            {
                                language = Languages.Cpp;
                                displayName = "C++";
                            }
                            else if (languageString == "CSS")
                            {
                                language = Languages.Css;
                            }
                            else if (languageString == "FSharp")
                            {
                                language = Languages.FSharp;
                                displayName = "F#";
                            }
                            else if (languageString == "Haskell")
                            {
                                language = Languages.Haskell;
                            }
                            else if (languageString == "HTML")
                            {
                                language = Languages.Html;
                            }
                            else if (languageString == "Java")
                            {
                                language = Languages.Java;
                            }
                            else if (languageString == "JavaScript")
                            {
                                language = Languages.JavaScript;
                            }
                            else if (languageString == "Markdown")
                            {
                                language = Languages.Markdown;
                            }
                            else if (languageString == "PHP")
                            {
                                language = Languages.Php;
                            }
                            else if (languageString == "PowerShell")
                            {
                                language = Languages.PowerShell;
                            }
                            else if (languageString == "SQL")
                            {
                                language = Languages.Sql;
                            }
                            else if (languageString == "TypeScript")
                            {
                                language = Languages.Typescript;
                            }
                            else if (languageString == "VB")
                            {
                                language = Languages.VbDotNet;
                            }
                            else if (languageString == "XML")
                            {
                                language = Languages.Xml;
                            }


                            var graph = await AuthenticationService.GetGraphService();

                            var formatter = new RichTextBlockFormatter(richText.RequestedTheme);

                            var textBlock = new RichTextBlock
                            {
                                FontFamily = new FontFamily("Consolas"),
                                Padding = new Thickness(20, 10, 20, 10)
                            };

                            if (language != null)
                            {
                                formatter.FormatRichTextBlock(attachment.Value, language, textBlock);
                            }
                            else
                            {
                                var para = new Paragraph();
                                var run = new Run
                                {
                                    Text = attachment.Value
                                };
                                para.Inlines.Add(run);
                                textBlock.Blocks.Add(para);
                            }

                            var border = new Border
                            {
                                Background = QuoteBrush,
                                Child = textBlock,
                                Width = width
                            };

                            var inlineUi = new InlineUIContainer
                            {
                                Child = border
                            };

                            await CreateParagraph(element, richText, width, true, textAlignment, attachments);

                            _lastInlines.Add(inlineUi);

                            await CreateParagraph(element, richText, width, true, textAlignment, attachments);
                        }
                        else
                        {
                            var paragraph = new Paragraph();
                            var run = new Run
                            {
                                Text =
                                    "This part of the message contains content which is unsupported by Comrades. Please use the official Teams client to view this message.",
                                FontStyle = Windows.UI.Text.FontStyle.Italic
                            };
                            paragraph.Inlines.Add(run);
                            richText.Blocks.Add(paragraph);

                            await CreateParagraph(element, richText, width, true, textAlignment, attachments);
                        }
                    }
                    else
                    {
                        await ParseElements(element.ChildNodes.ToList(), false, richText, width, textAlignment,
                            attachments);
                    }
                }
                else if (node.NodeType == NodeType.Text)
                {
                    if (node.TextContent.Trim() != "" && node.TextContent.ToCharArray().Any(c => c != '\n'))
                    {
                        var run = new Run
                        {
                            Text = node.TextContent
                        };

                        if (listLevel > 0)
                        {
                            run.Text = Regex.Replace(run.Text, @"\n+\t+\n*$", "");
                        }

                        if (run.Text != "")
                        {
                            _lastInlines.Add(run);
                        }
                    }
                }
            }
        }

        private static async Task ProcessSpan(IElement element, Span span, RichTextBlock richText, double width,
            bool isNewParagraph, TextAlignment lastAlignment, Dictionary<ChatMessageAttachment, string> attachments)
        {
            if (!isNewParagraph)
            {
                ElementStack.Add(element);
            }

            var oldInline = _lastInlines;
            _lastInlines = span.Inlines;

            if (!isNewParagraph)
            {
                ElementStack.Remove(element);
                await ParseElements(element.ChildNodes.ToList(), false, richText, width, lastAlignment, attachments);
                _lastInlines = oldInline;
            }
        }

        private static async Task CreateParagraph(IElement root, RichTextBlock richText, double width, bool empty,
            TextAlignment lastAlignment, Dictionary<ChatMessageAttachment, string> attachments,
            bool? orderedList = null, int listLevel = 0)
        {
            _currentParagraph = new Paragraph();
            _lastInlines = _currentParagraph.Inlines;

            if (orderedList.HasValue && root.TagName == "LI")
            {
                _currentParagraph.TextIndent = listLevel * 10;
                var run = new Run();
                _currentParagraph.Inlines.Add(run);

                if (orderedList.Value)
                {
                    run.Text = $"{_listIndex}. ";
                }
                else
                {
                    if (listLevel == 1)
                    {
                        run.Text = "• ";
                    }
                    else if (listLevel == 2)
                    {
                        run.Text = "◦ ";
                    }
                    else
                    {
                        run.Text = "▪ ";
                    }
                }
            }

            var alignment = root.GetStyle().GetTextAlign();
            if (alignment == "left")
            {
                _currentParagraph.TextAlignment = TextAlignment.Left;
            }
            else if (alignment == "center")
            {
                _currentParagraph.TextAlignment = TextAlignment.Center;
            }
            else if (alignment == "right")
            {
                _currentParagraph.TextAlignment = TextAlignment.Right;
            }
            else if (alignment == "justify")
            {
                _currentParagraph.TextAlignment = TextAlignment.Justify;
            }
            else if (alignment == "start")
            {
                _currentParagraph.TextAlignment = TextAlignment.Start;
            }
            else if (alignment == "end")
            {
                _currentParagraph.TextAlignment = TextAlignment.End;
            }
            else
            {
                _currentParagraph.TextAlignment = lastAlignment;
            }

            richText.Blocks.Add(_currentParagraph);
            await ParseElements(ElementStack, true, richText, width, lastAlignment, attachments, orderedList,
                listLevel);

            if (!empty)
            {
                await ParseElements(root.ChildNodes.ToList(), false, richText, width, lastAlignment, attachments,
                    orderedList, listLevel);
            }
        }


        private static async Task ProcessTable(IElement tableNode, StackPanel table, Brush brush,
            TextAlignment lastAlignment, double parentWidth, Dictionary<ChatMessageAttachment, string> attachments)
        {
            Grid lastGrid = null;
            List<GridLength> columnDefinitions = null;
            int totalCells = 0;
            int cellIndex = 0;
            int gridIndex = 0;

            bool lastRow = false;
            int rowIndex = 0;

            Dictionary<int, Tuple<IElement, int>> rowSpans = new Dictionary<int, Tuple<IElement, int>>();

            List<IElement> elementsToTraverse = tableNode.Children.ToList();
            int count = elementsToTraverse.Count;

            for (int i = 0; i < count; i++)
            {
                var element = elementsToTraverse[i];

                switch (element.TagName)
                {
                    case "COLGROUP":
                        columnDefinitions = [];

                        elementsToTraverse.InsertRange(i + 1, element.Children);
                        count = elementsToTraverse.Count;
                        break;
                    case "COL":
                    {
                        var width = element.GetStyle().GetWidth();

                        var spacesPattern = @"\s+";
                        width = Regex.Replace(width, spacesPattern, "");

                        var unitsPattern = @"([\d\.]+)((?:px)|%)";

                        var matches = Regex.Matches(width, unitsPattern);

                        if (matches.Count > 0)
                        {
                            var quantity = matches[0].Groups[1].Value;
                            var unit = matches[0].Groups[2].Value;

                            if (unit == "px")
                            {
                                columnDefinitions.Add(new GridLength(double.Parse(quantity)));
                            }
                            else if (unit == "%")
                            {
                                columnDefinitions.Add(new GridLength(double.Parse(quantity),
                                    GridUnitType.Star));
                            }
                        }
                        else
                        {
                            var colNumbers = elementsToTraverse.Count(c => c.TagName == "COL");
                            var colWidth = 100.0 / colNumbers;

                            columnDefinitions.Add(
                                new GridLength(colWidth, GridUnitType.Star));
                        }

                        break;
                    }
                    case "TR":
                    {
                        lastGrid = new Grid
                        {
                            HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch
                        };
                        table.Children.Add(lastGrid);

                        elementsToTraverse.InsertRange(i + 1, element.Children);
                        totalCells = element.Children.Count();

                        var children = element.Children.ToList();

                        foreach (var pair in rowSpans)
                        {
                            var elementToInsert = pair.Value.Item1;
                            elementToInsert.InnerHtml = "";
                            elementToInsert.RemoveAttribute("rowspan");
                            elementsToTraverse.Insert(i + pair.Key + 1, elementToInsert);
                            totalCells++;
                            children.Add(elementToInsert);
                        }

                        count = elementsToTraverse.Count();

                        cellIndex = 0;
                        gridIndex = 0;

                        rowIndex++;

                        if (columnDefinitions != null && columnDefinitions.Any())
                        {
                            columnDefinitions.ForEach(c =>
                            {
                                var columnDefinition = new ColumnDefinition
                                {
                                    Width = c
                                };
                                lastGrid.ColumnDefinitions.Add(columnDefinition);
                            });
                        }
                        else
                        {
                            int columns = 0;

                            foreach (var cell in children)
                            {
                                var colspan = cell.GetAttribute("colspan");
                                if (colspan != null)
                                {
                                    columns += int.Parse(colspan);
                                }
                                else
                                {
                                    columns++;
                                }
                            }

                            for (int j = 0; j != columns; j++)
                            {
                                var columnDefinition = new ColumnDefinition
                                {
                                    Width = GridLength.Auto
                                };
                                lastGrid.ColumnDefinitions.Add(columnDefinition);
                            }
                        }

                        var rowDefinition = new RowDefinition
                        {
                            Height = GridLength.Auto
                        };
                        lastGrid.RowDefinitions.Add(rowDefinition);

                        lastRow = elementsToTraverse.Last(e => e.TagName == "TR") == element;
                        break;
                    }
                    case "TD":
                    {
                        var cell = new Border();
                        var borderWidth = 1.0;

                        var borderWidthCss = element.GetStyle().GetBorderWidth();
                        var unitsPattern = @"([\d\.]+)((?:px))";

                        var matches = Regex.Matches(borderWidthCss, unitsPattern);

                        if (matches.Count > 1)
                        {
                            var quantity = matches[0].Groups[1].Value;
                            var unit = matches[0].Groups[2].Value;

                            if (unit == "px")
                            {
                                borderWidth = double.Parse(quantity);
                            }
                        }

                        double bottomBorder = 0.0;
                        if (lastRow)
                        {
                            bottomBorder = borderWidth;
                        }

                        double topBorder = borderWidth;

                        if (rowSpans.ContainsKey(cellIndex))
                        {
                            topBorder = 0.0;
                            var contents = rowSpans[cellIndex];

                            if (contents.Item2 == 1)
                            {
                                rowSpans.Remove(cellIndex);
                            }
                            else
                            {
                                rowSpans[cellIndex] = Tuple.Create(contents.Item1, contents.Item2 - 1);
                            }
                        }

                        var rowSpan = element.GetAttribute("rowspan");
                        if (rowSpan != null)
                        {
                            rowSpans[cellIndex] = Tuple.Create(element, int.Parse(rowSpan) - 1);
                        }

                        cell.BorderThickness = ++cellIndex == totalCells
                            ? new Thickness(borderWidth, topBorder, borderWidth, bottomBorder)
                            : new Thickness(borderWidth, topBorder, 0, bottomBorder);

                        cell.BorderBrush = brush;

                        var width = element.GetAttribute("width");

                        if (width != null)
                        {
                            cell.Width = double.Parse(width);
                        }

                        Grid.SetRow(cell, 0);
                        Grid.SetColumn(cell, gridIndex);

                        var columnDef = lastGrid.ColumnDefinitions[gridIndex];

                        var colSpan = element.GetAttribute("colspan");

                        if (colSpan != null)
                        {
                            var value = int.Parse(colSpan);
                            Grid.SetColumnSpan(cell, value);
                            gridIndex += value;
                        }
                        else
                        {
                            gridIndex++;
                        }

                        var richTextBlock = new RichTextBlock
                        {
                            HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch
                        };
                        cell.HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch;

                        var oldParagraph = _currentParagraph;
                        var lastInlines = _lastInlines;

                        var insideWidth = 0.0;
                        if (columnDef.Width.IsStar)
                        {
                            insideWidth = parentWidth * (columnDef.Width.Value / 100);
                        }
                        else
                        {
                            insideWidth = columnDef.Width.Value;
                        }

                        insideWidth = Math.Max(insideWidth - 10, 0);

                        await CreateParagraph(element, richTextBlock, insideWidth, false, lastAlignment, attachments);
                        TrimBlock(richTextBlock);
                        _currentParagraph = oldParagraph;
                        _lastInlines = lastInlines;

                        cell.Child = richTextBlock;

                        var cssPadding = element.GetStyle().GetPadding();
                        Thickness? padding = null;

                        if (cssPadding != null)
                        {
                            const string paddingUnitsPattern = @"([\d\.]+)((?:px)|(?:em)|(?:rem))";

                            var paddingMatches = Regex.Matches(cssPadding, paddingUnitsPattern);

                            if (paddingMatches.Count > 0)
                            {
                                switch (paddingMatches.Count)
                                {
                                    case 1:
                                    {
                                        var allPadding = GetPadding(paddingMatches.First().Groups,
                                            richTextBlock.FontSize);
                                        padding = new Thickness(allPadding);
                                        break;
                                    }
                                    case 2:
                                    {
                                        var vertical = GetPadding(paddingMatches.First().Groups,
                                            richTextBlock.FontSize);
                                        var horizontal = GetPadding(paddingMatches[1].Groups, richTextBlock.FontSize);

                                        padding = new Thickness(horizontal, vertical, horizontal, vertical);
                                        break;
                                    }
                                    case 4:
                                    {
                                        var top = GetPadding(paddingMatches[0].Groups, richTextBlock.FontSize);
                                        var right = GetPadding(paddingMatches[1].Groups, richTextBlock.FontSize);
                                        var bottom = GetPadding(paddingMatches[2].Groups, richTextBlock.FontSize);
                                        var left = GetPadding(paddingMatches[3].Groups, richTextBlock.FontSize);

                                        padding = new Thickness(left, top, right, bottom);
                                        break;
                                    }
                                }
                            }
                        }

                        padding ??= new Thickness(5);

                        richTextBlock.Padding = padding.Value;

                        lastGrid.Children.Add(cell);
                        break;
                    }
                    case "TBODY":
                        elementsToTraverse.InsertRange(i + 1, element.Children);
                        count = elementsToTraverse.Count;
                        break;
                }
            }
        }

        private static double GetPadding(GroupCollection padding, double fontSize)
        {
            if (padding[2].Value == "px")
            {
                return double.Parse(padding[1].Value);
            }

            return double.Parse(padding[1].Value) * fontSize;
        }


        private static void TrimBlock(RichTextBlock richText)
        {
            var blocks = richText.Blocks;

            foreach (var block in blocks)
            {
                if (block is Paragraph paragraph && paragraph.Inlines.Count == 0)
                {
                    richText.Blocks.Remove(paragraph);
                }
            }
        }
    }
}