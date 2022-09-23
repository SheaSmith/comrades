using AngleSharp;
using AngleSharp.Css.Dom;
using AngleSharp.Dom;
using CommunityToolkit.WinUI.Helpers;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Text;

namespace Comrades.MarkupConverter
{
    public class ConvertFromHtml
    {
        public static SolidColorBrush QuoteBrush { get; set; }

        private static Paragraph CurrentParagraph = new Paragraph();

        public static async Task FromHtml(string html, RichTextBlock textBlock, double width)
        {
            textBlock.Blocks.Clear();

            var config = Configuration.Default.WithCss();

            var context = BrowsingContext.New(config);

            var document = await context.OpenAsync(req => req.Content(html));

            ParseParagraph((IElement)document.DocumentElement.Descendents().FirstOrDefault(d => d.NodeType == NodeType.Element && ((IElement)d).TagName == "DIV"), textBlock.Blocks, textBlock, width);
        }

        private static void ParseParagraph(IElement element, BlockCollection blocks, RichTextBlock mainBlock, double width)
        {
            if (element == null)
            {
                return;
            }

            if (element.TagName == "DIV")
            {
                var paragraph = new Paragraph();
                blocks.Add(paragraph);
                ParseContent(element, paragraph.Inlines, mainBlock, width, 1);
            }
        }

        private static void ParseElement(IElement element, InlineCollection inlines, RichTextBlock mainBlock, double width, int listDepth)
        {
            if (element.TagName == "A")
            {
                var hyperlink = new Hyperlink();
                ParseContent(element, hyperlink.Inlines, mainBlock, width, listDepth);
                inlines.Add(hyperlink);
            }
            else if (element.TagName == "STRONG" || element.TagName == "B")
            {
                var bold = new Bold();
                ParseContent(element, bold.Inlines, mainBlock, width, listDepth);
                inlines.Add(bold);
            }
            else if (element.TagName == "EM")
            {
                var italic = new Italic();
                ParseContent(element, italic.Inlines, mainBlock, width, listDepth);
                inlines.Add(italic);
            }
            else if (element.TagName == "U")
            {
                var underline = new Underline();
                ParseContent(element, underline.Inlines, mainBlock, width, listDepth);
                inlines.Add(underline);
            }
            else if (element.TagName == "S")
            {
                var strikethrough = new Span();
                strikethrough.TextDecorations = TextDecorations.Strikethrough;
                ParseContent(element, strikethrough.Inlines, mainBlock, width, listDepth);
                inlines.Add(strikethrough);
            }
            else if (element.TagName == "SPAN")
            {
                var span = new Span();

                var styles = element.GetStyle();
                var highlightColor = styles.GetBackgroundColor();

                var fontColor = styles.GetColor();

                if (fontColor != null && fontColor != "")
                {
                    var colors = fontColor.Replace("rgba(", "").Replace(")", "").Split(", ");
                    var color = Color.FromArgb(Convert.ToByte(Decimal.ToUInt16((Decimal.Parse(colors[3]) * 255))), Byte.Parse(colors[0]), Byte.Parse(colors[1]), Byte.Parse(colors[2]));
                    span.Foreground = new SolidColorBrush(color);
                }

                var textSize = styles.GetFontSize();

                if (textSize != null && textSize != "")
                {
                    textSize = Regex.Replace(textSize, @"\s+", "");

                    var unitMatch = Regex.Match(textSize, @"([\d\.]+)((?:px)|(?:em)|(?:rem)|(?:pt))");

                    double textSizeValue = mainBlock.FontSize;

                    if (unitMatch.Success)
                    {
                        var unit = unitMatch.Groups[2].Value;
                        var value = double.Parse(unitMatch.Groups[1].Value);

                        if (unit == "px")
                        {
                            textSizeValue = value;
                        } else if (unit == "rem" || unit == "em")
                        {
                            textSizeValue = textSizeValue * value;
                        } else if (unit == "pt")
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

                if (fontFamily != null && fontFamily != "")
                {
                    var firstFont = fontFamily.Split(",")[0].Replace("\"", "").Trim();
                    span.FontFamily = new FontFamily(firstFont);
                }

                ParseContent(element, span.Inlines, mainBlock, width, listDepth);


                if (highlightColor != null && highlightColor != "")
                {
                    //var richTextBlock = new RichTextBlock();
                    //var paragraph = new Paragraph();
                    //paragraph.Inlines.Add(span);
                    //richTextBlock.Blocks.Add(paragraph);

                    var highlighter = new TextHighlighter();

                    var textRange = new TextRange(0, 999999999);

                    highlighter.Ranges.Add(textRange);

                    var colors = highlightColor.Replace("rgba(", "").Replace(")", "").Split(", ");
                    var color = Color.FromArgb(Convert.ToByte(Decimal.ToUInt16((Decimal.Parse(colors[3]) * 255))), Byte.Parse(colors[0]), Byte.Parse(colors[1]), Byte.Parse(colors[2]));

                    highlighter.Background = new SolidColorBrush(color);

                    mainBlock.TextHighlighters.Add(highlighter);

                    //var inlineUi = new InlineUIContainer();
                    //inlineUi.Child = richTextBlock;
                    //inlines.Add(inlineUi);
                }
                //else
                //{
                    inlines.Add(span);
                //}

            }
            else if (element.TagName == "BLOCKQUOTE")
            {
                var linebreak = new LineBreak();
                inlines.Add(linebreak);

                var inlineUiContainer = new InlineUIContainer();

                var richTextBlock = new RichTextBlock();
                var paragraph = new Paragraph();
                ParseContent(element, paragraph.Inlines, mainBlock, width, listDepth);

                if (paragraph.Inlines.FirstOrDefault() is LineBreak)
                {
                    paragraph.Inlines.RemoveAt(0);
                }

                if (paragraph.Inlines.LastOrDefault() is LineBreak)
                {
                    paragraph.Inlines.RemoveAt(paragraph.Inlines.Count - 1);
                }

                richTextBlock.Blocks.Add(paragraph);

                var stackPanel = new StackPanel();
                stackPanel.Children.Add(richTextBlock);
                stackPanel.Padding = new Microsoft.UI.Xaml.Thickness(12, 12, 200, 12);
                stackPanel.CornerRadius = new Microsoft.UI.Xaml.CornerRadius(8);
                stackPanel.Background = QuoteBrush;
                stackPanel.Width = width - 10;

                inlineUiContainer.Child = stackPanel;
                inlines.Add(inlineUiContainer);

                var linebreak2 = new LineBreak();
                inlines.Add(linebreak2);
            }
            else if (element.TagName == "DIV" || element.TagName == "P")
            {
                var linebreak = new LineBreak();
                inlines.Add(linebreak);

                ParseContent(element, inlines, mainBlock, width, listDepth);
            }
            else if (element.TagName == "H1")
            {
                var linebreak = new LineBreak();
                inlines.Add(linebreak);

                var span = new Span();
                span.FontSize = 40;
                span.FontWeight = FontWeights.SemiBold;
                ParseContent(element, span.Inlines, mainBlock, width, listDepth);
                inlines.Add(span);
            }
            else if (element.TagName == "H2")
            {
                var linebreak = new LineBreak();
                inlines.Add(linebreak);

                var span = new Span();
                span.FontSize = 28;
                span.FontWeight = FontWeights.SemiBold;
                ParseContent(element, span.Inlines, mainBlock, width, listDepth);
                inlines.Add(span);
            }
            else if (element.TagName == "H1")
            {
                var linebreak = new LineBreak();
                inlines.Add(linebreak);

                var span = new Span();
                span.FontSize = 20;
                span.FontWeight = FontWeights.SemiBold;
                ParseContent(element, span.Inlines, mainBlock, width, listDepth);
                inlines.Add(span);
            }
            else if (element.TagName == "HR")
            {
                var linebreak = new LineBreak();
                inlines.Add(linebreak);

                var horizontalRule = new Border();
                horizontalRule.Width = width - 10;
                horizontalRule.BorderThickness = new Microsoft.UI.Xaml.Thickness(0.5);
                horizontalRule.Opacity = 0.5;
                horizontalRule.BorderBrush = mainBlock.Foreground;

                var inlineUi = new InlineUIContainer();
                inlineUi.Child = horizontalRule;
                inlines.Add(inlineUi);
            }
            else if (element.TagName == "TABLE")
            {
                var linebreak = new LineBreak();
                inlines.Add(linebreak);

                var table = new StackPanel();
                table.Width = width - 10;

                ProcessTable(element, table, mainBlock.Foreground, mainBlock, width, listDepth);

                var inlineUi = new InlineUIContainer();
                inlineUi.Child = table;
                inlines.Add(inlineUi);
            }
            else if (element.TagName == "UL")
            {
                if (listDepth == 1)
                {
                    var linebreak = new LineBreak();
                    inlines.Add(linebreak);
                }

                ProcessList(element, inlines, mainBlock, width, listDepth, false);
            }
            else if (element.TagName == "OL")
            {
                if (listDepth == 1)
                {
                    var linebreak = new LineBreak();
                    inlines.Add(linebreak);
                }

                ProcessList(element, inlines, mainBlock, width, listDepth, true);
            }
            else if (element.TagName == "IMG")
            {
                var image = new Image();
                var bitmapImage = new BitmapImage();
                bitmapImage.UriSource = new Uri(element.GetAttribute("src"));
                image.Source = bitmapImage;

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

                var inlineUi = new InlineUIContainer();
                inlineUi.Child = image;
                inlines.Add(inlineUi);
            }
            else
            {
                ParseContent(element, inlines, mainBlock, width, listDepth);
            }
        }

        private static void ParseContent(IElement node, InlineCollection inlines, RichTextBlock mainBlock, double width, int listDepth)
        {
            foreach (var child in node.ChildNodes)
            {
                if (child.NodeType == NodeType.Text && child.TextContent != "" && !child.TextContent.ToCharArray().All(c => c == '\n'))
                {
                    var run = new Run();
                    run.Text = child.TextContent;

                    if (listDepth > 1)
                    {
                        run.Text = Regex.Replace(run.Text, @"\n+\t+\n*$", "");
                    }

                    if (run.Text != "")
                    {
                        inlines.Add(run);
                    }
                }
                else if (child.NodeType == NodeType.Element)
                {
                    ParseElement((IElement)child, inlines, mainBlock, width, listDepth);
                }
            }
        }


        private static void ProcessList(IElement listNode, InlineCollection inlines, RichTextBlock mainBlock, double width, int depth, bool ordered)
        {
            int number = 0;

            foreach (var child in listNode.Children)
            {
                if (child.TagName == "LI")
                {
                    number++;

                    var span = new Span();

                    var run = new Run();

                    run.Text = "";

                    for (int i = 0; i < depth; i++)
                    {
                        run.Text += "\t";
                    }

                    if (ordered)
                    {
                        run.Text += $"{number}. ";
                    }
                    else
                    {
                        if (depth == 1)
                        {
                            run.Text += "• ";
                        }
                        else if (depth == 2)
                        {
                            run.Text += "◦ ";
                        }
                        else
                        {
                            run.Text += "▪ ";
                        }
                    }

                    span.Inlines.Add(new LineBreak());

                    span.Inlines.Add(run);

                    ParseContent(child, span.Inlines, mainBlock, width, depth + 1);

                    inlines.Add(span);
                }
            }
        }

        private static void ProcessTable(IElement tableNode, StackPanel table, Brush brush, RichTextBlock mainBlock, double parentWidth, int listDepth)
        {
            Grid lastGrid = null;
            List<GridLength> columnDefinitions = null;
            int totalCells = 0;
            int cellIndex = 0;
            int gridIndex = 0;

            int rows = tableNode.Descendents().Count(n => n.NodeType == NodeType.Element && ((IElement)n).TagName == "TR");
            int rowIndex = 0;

            Dictionary<int, Tuple<IElement, int>> rowSpans = new Dictionary<int, Tuple<IElement, int>>();

            List<IElement> elementsToTraverse = tableNode.Children.ToList();
            int count = elementsToTraverse.Count;

            for (int i = 0; i < count; i++)
            {
                var element = elementsToTraverse[i];

                if (element.TagName == "COLGROUP")
                {
                    columnDefinitions = new List<GridLength>();

                    elementsToTraverse.InsertRange(i + 1, element.Children);
                    count = elementsToTraverse.Count();
                }
                else if (element.TagName == "COL")
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
                            columnDefinitions.Add(new Microsoft.UI.Xaml.GridLength(double.Parse(quantity)));
                        }
                        else if (unit == "%")
                        {
                            columnDefinitions.Add(new Microsoft.UI.Xaml.GridLength(double.Parse(quantity), Microsoft.UI.Xaml.GridUnitType.Star));
                        }
                    }
                }
                else if (element.TagName == "TR")
                {
                    lastGrid = new Grid();
                    lastGrid.HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch;
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
                            var columnDefinition = new ColumnDefinition();
                            columnDefinition.Width = c;
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
                            var columnDefinition = new ColumnDefinition();
                            columnDefinition.Width = Microsoft.UI.Xaml.GridLength.Auto;
                            lastGrid.ColumnDefinitions.Add(columnDefinition);
                        }
                    }

                    var rowDefinition = new RowDefinition();
                    rowDefinition.Height = Microsoft.UI.Xaml.GridLength.Auto;
                    lastGrid.RowDefinitions.Add(rowDefinition);
                }
                else if (element.TagName == "TD")
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
                    if (rowIndex == rows)
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

                    if (++cellIndex == totalCells)
                    {
                        cell.BorderThickness = new Microsoft.UI.Xaml.Thickness(borderWidth, topBorder, borderWidth, bottomBorder);
                    }
                    else
                    {
                        cell.BorderThickness = new Microsoft.UI.Xaml.Thickness(borderWidth, topBorder, 0, bottomBorder);
                    }

                    cell.BorderBrush = brush;

                    var width = element.GetAttribute("width");

                    if (width != null)
                    {
                        cell.Width = double.Parse(width);
                    }

                    Grid.SetRow(cell, 0);
                    Grid.SetColumn(cell, gridIndex);

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

                    var richTextBlock = new RichTextBlock();
                    richTextBlock.HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch;

                    var paragraph = new Paragraph();
                    richTextBlock.Blocks.Add(paragraph);

                    ParseContent(element, paragraph.Inlines, mainBlock, parentWidth, listDepth);

                    cell.Child = richTextBlock;

                    var cssPadding = element.GetStyle().GetPadding();
                    Thickness? padding = null;

                    if (cssPadding != null)
                    {
                        var paddingUnitsPattern = @"([\d\.]+)((?:px)|(?:em)|(?:rem))";

                        var paddingMatches = Regex.Matches(cssPadding, paddingUnitsPattern);

                        if (paddingMatches.Count > 0)
                        {
                            if (paddingMatches.Count == 1)
                            {
                                var allPadding = getPadding(paddingMatches.First().Groups, richTextBlock.FontSize);
                                padding = new Thickness(allPadding);

                            }
                            else if (paddingMatches.Count == 2)
                            {
                                var vertical = getPadding(paddingMatches.First().Groups, richTextBlock.FontSize);
                                var horizontal = getPadding(paddingMatches[1].Groups, richTextBlock.FontSize);

                                padding = new Thickness(horizontal, vertical, horizontal, vertical);
                            }
                            else if (paddingMatches.Count == 4)
                            {
                                var top = getPadding(paddingMatches[0].Groups, richTextBlock.FontSize);
                                var right = getPadding(paddingMatches[1].Groups, richTextBlock.FontSize);
                                var bottom = getPadding(paddingMatches[2].Groups, richTextBlock.FontSize);
                                var left = getPadding(paddingMatches[3].Groups, richTextBlock.FontSize);

                                padding = new Thickness(left, top, right, bottom);
                            }
                        }
                    }

                    if (padding == null)
                    {
                        padding = new Thickness(5);
                    }

                    richTextBlock.Padding = padding.Value;

                    lastGrid.Children.Add(cell);
                }

                else if (element.TagName == "TBODY")
                {
                    elementsToTraverse.InsertRange(i + 1, element.Children);
                    count = elementsToTraverse.Count();
                }



            }
        }

        private static double getPadding(GroupCollection padding, double fontSize)
        {
            if (padding[2].Value == "px")
            {
                return double.Parse(padding[1].Value);
            }

            return double.Parse(padding[1].Value) * fontSize;
        }
    }
}
