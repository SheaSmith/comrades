using AngleSharp;
using AngleSharp.Css.Dom;
using AngleSharp.Dom;
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
    public class ConvertFromHtml2
    {
        private static List<INode> ElementStack = new List<INode>();
        private static Paragraph CurrentParagraph = new Paragraph();
        private static InlineCollection LastInlines = CurrentParagraph.Inlines;
        public static SolidColorBrush QuoteBrush { get; set; }

        private static int ListIndex = 1;

        public async static Task FromHtml(string html, RichTextBlock richText, double width)
        {
            richText.Blocks.Clear();

            var config = Configuration.Default.WithCss();

            var context = BrowsingContext.New(config);

            var document = await context.OpenAsync(req => req.Content(html));

            ParseElements(document.GetElementsByTagName("BODY").First().ChildNodes.ToList(), false, richText, width, TextAlignment.Start);
            TrimBlock(richText);
        }


        private static void ParseElements(List<INode> nodes, bool isNewParagraph, RichTextBlock richText, double width, TextAlignment textAlignment, bool? orderedList = null, int listLevel = 0)
        {
            foreach (var node in nodes)
            {
                if (node.NodeType == NodeType.Element)
                {
                    var element = node as IElement;

                    if (element.TagName == "A")
                    {
                        var hyperlink = new Hyperlink();
                        LastInlines.Add(hyperlink);
                        ProcessSpan(element, hyperlink, richText, width, isNewParagraph, textAlignment);
                    }
                    else if (element.TagName == "STRONG")
                    {
                        var bold = new Bold();
                        LastInlines.Add(bold);
                        ProcessSpan(element, bold, richText, width, isNewParagraph, textAlignment);
                    }
                    else if (element.TagName == "EM")
                    {
                        var italic = new Italic();
                        LastInlines.Add(italic);
                        ProcessSpan(element, italic, richText, width, isNewParagraph, textAlignment);
                    }
                    else if (element.TagName == "U")
                    {
                        var underline = new Underline();
                        LastInlines.Add(underline);
                        ProcessSpan(element, underline, richText, width, isNewParagraph, textAlignment);
                    }
                    else if (element.TagName == "S")
                    {
                        var strikethrough = new Span();
                        strikethrough.TextDecorations = TextDecorations.Strikethrough;
                        LastInlines.Add(strikethrough);
                        ProcessSpan(element, strikethrough, richText, width, isNewParagraph, textAlignment);
                    }
                    else if (element.TagName == "SPAN" || element.TagName == "P")
                    {
                        if (element.TagName == "P")
                        {
                            CreateParagraph(element, richText, width, true, textAlignment);
                        }

                        var span = new Span();

                        var styles = element.GetStyle();
                        var highlightColor = styles.GetBackgroundColor();

                        var fontColor = styles.GetColor();

                        if (fontColor != null && fontColor != "")
                        {
                            var colors = fontColor.Replace("rgba(", "").Replace(")", "").Split(", ");
                            var color = Color.FromArgb(Convert.ToByte(Decimal.ToUInt16((Decimal.Parse(colors[3]) * 255))), Byte.Parse(colors[0]), Byte.Parse(colors[1]), Byte.Parse(colors[2]));
                            var brush = new SolidColorBrush(color);
                            span.Foreground = brush;
                        }

                        var textSize = styles.GetFontSize();

                        if (textSize != null && textSize != "")
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

                        if (fontFamily != null && fontFamily != "")
                        {
                            var font = fontFamily.Replace("\"", "").Replace("serif", "Cambira").Replace("sans-serif", "Segoe UI").Replace("monospace", "Consolas");
                            span.FontFamily = new FontFamily(font);
                        }


                        if (highlightColor != null && highlightColor != "")
                        {
                            var richTextBlock = new RichTextBlock();

                            var lastParagraph = CurrentParagraph;
                            var lastInlines = LastInlines;
                            CreateParagraph(element, richTextBlock, width, false, textAlignment);
                            CurrentParagraph = lastParagraph;
                            LastInlines = lastInlines;

                            var colors = highlightColor.Replace("rgba(", "").Replace(")", "").Split(", ");
                            var color = Color.FromArgb(Convert.ToByte(Decimal.ToUInt16((Decimal.Parse(colors[3]) * 255))), Byte.Parse(colors[0]), Byte.Parse(colors[1]), Byte.Parse(colors[2]));

                            var border = new Border();
                            border.VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Bottom;
                            border.Background = new SolidColorBrush(color);
                            border.Child = richTextBlock;

                            var inlineUi = new InlineUIContainer();
                            inlineUi.Child = border;

                            LastInlines.Add(inlineUi);
                        } else
                        {
                            LastInlines.Add(span);
                            ProcessSpan(element, span, richText, width, isNewParagraph, textAlignment);
                        }


                        if (element.TagName == "P")
                        {
                            CreateParagraph(element, richText, width, true, textAlignment);
                        }

                    }
                    else if (element.TagName == "DIV")
                    {
                        CreateParagraph(element, richText, width, false, textAlignment);
                        CreateParagraph(element, richText, width, true, textAlignment);
                    }
                    else if (element.TagName == "BLOCKQUOTE")
                    {
                        CreateParagraph(element, richText, width, true, textAlignment);

                        var inlineUiContainer = new InlineUIContainer();

                        var richTextBlock = new RichTextBlock();
                        var oldParagraph = CurrentParagraph;
                        var lastInlines = LastInlines;
                        ParseElements(ElementStack, true, richTextBlock, width, textAlignment);
                        ParseElements(element.ChildNodes.ToList(), false, richTextBlock, width, textAlignment);
                        TrimBlock(richTextBlock);
                        CurrentParagraph = oldParagraph;
                        LastInlines = lastInlines;

                        var stackPanel = new StackPanel();
                        stackPanel.Children.Add(richTextBlock);
                        stackPanel.Padding = new Microsoft.UI.Xaml.Thickness(12, 12, 200, 12);
                        stackPanel.CornerRadius = new Microsoft.UI.Xaml.CornerRadius(8);
                        stackPanel.Background = QuoteBrush;
                        stackPanel.Width = width - 10;

                        inlineUiContainer.Child = stackPanel;
                        LastInlines.Add(inlineUiContainer);

                        CreateParagraph(element, richText, width, true, textAlignment);
                    }
                    else if (element.TagName == "H1" || element.TagName == "H2" || element.TagName == "H3")
                    {
                        CreateParagraph(element, richText, width, true, textAlignment);
                        var span = new Span();
                        
                        if (element.TagName == "H1")
                        {
                            span.FontSize = 40;
                        } else if (element.TagName == "H2")
                        {
                            span.FontSize = 28;
                        } else
                        {
                            span.FontSize = 20;
                        }

                        span.FontWeight = FontWeights.SemiBold;

                        LastInlines.Add(span);
                        ProcessSpan(element, span, richText, width, isNewParagraph, textAlignment);

                        CreateParagraph(element, richText, width, true, textAlignment);
                    }
                    else if (element.TagName == "HR")
                    {
                        var horizontalRule = new Border();
                        horizontalRule.Width = width - 10;
                        horizontalRule.BorderThickness = new Thickness(0.5);
                        horizontalRule.Opacity = 0.5;
                        horizontalRule.BorderBrush = richText.Foreground;

                        var inlineUi = new InlineUIContainer();
                        inlineUi.Child = horizontalRule;

                        CreateParagraph(element, richText, width, true, textAlignment);
                        LastInlines.Add(inlineUi);
                        CreateParagraph(element, richText, width, true, textAlignment);
                    }
                    else if (element.TagName == "TABLE")
                    {
                        var table = new StackPanel();
                        table.Width = Math.Max(width - 10, 0);

                        var inlineUi = new InlineUIContainer();
                        inlineUi.Child = table;

                        CreateParagraph(element, richText, width, true, textAlignment);
                        LastInlines.Add(inlineUi);
                        ProcessTable(element, table, richText.Foreground, textAlignment, width);
                        CreateParagraph(element, richText, width, true, textAlignment);
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

                        LastInlines.Add(inlineUi);
                    }
                    else if (element.TagName == "UL")
                    {
                        ParseElements(element.ChildNodes.ToList(), false, richText, width, textAlignment, false, listLevel + 1);

                        if (listLevel == 0)
                        {
                            CreateParagraph(element, richText, width, true, textAlignment);
                        }
                    }
                    else if (element.TagName == "OL")
                    {
                        var lastListIndex = ListIndex;
                        ListIndex = 1;
                        ParseElements(element.ChildNodes.ToList(), false, richText, width, textAlignment, true, listLevel + 1);
                        ListIndex = lastListIndex;

                        if (listLevel == 0)
                        {
                            CreateParagraph(element, richText, width, true, textAlignment);
                        }
                    }
                    else if (element.TagName == "LI")
                    {
                        CreateParagraph(element, richText, width, false, textAlignment, orderedList, listLevel);
                        ListIndex++;
                    }
                    else if (element.TagName == "CODE")
                    {
                        var textBlock = new RichTextBlock();
                        textBlock.FontFamily = new FontFamily("Consolas");
                        var paragraph = new Paragraph();
                        var run = new Run();
                        run.Text = element.TextContent;
                        paragraph.Inlines.Add(run);
                        textBlock.Blocks.Add(paragraph);
                        textBlock.Padding = new Thickness(3);

                        var border = new Border();
                        border.Background = QuoteBrush;
                        border.Child = textBlock;

                        var inlineUi = new InlineUIContainer();
                        inlineUi.Child = border;
                        LastInlines.Add(inlineUi);
                    }
                    else if (element.TagName == "PRE")
                    {
                        var textBlock = new RichTextBlock();
                        textBlock.FontFamily = new FontFamily("Consolas");
                        var paragraph = new Paragraph();
                        var run = new Run();
                        run.Text = Regex.Replace(element.TextContent, @"\n$", "");
                        paragraph.Inlines.Add(run);
                        textBlock.Blocks.Add(paragraph);
                        textBlock.Padding = new Thickness(20, 10, 20, 10);

                        var border = new Border();
                        border.Background = QuoteBrush;
                        border.Child = textBlock;
                        border.Width = width;

                        var inlineUi = new InlineUIContainer();
                        inlineUi.Child = border;

                        CreateParagraph(element, richText, width, true, textAlignment);

                        LastInlines.Add(inlineUi);

                        CreateParagraph(element, richText, width, true, textAlignment);
                    }
                    else
                    {
                        ParseElements(element.ChildNodes.ToList(), false, richText, width, textAlignment);
                    }
                }
                else if (node.NodeType == NodeType.Text)
                {
                    if (node.TextContent.Trim() != "" && !node.TextContent.ToCharArray().All(c => c == '\n'))
                    {
                        var run = new Run();
                        run.Text = node.TextContent;

                        if (listLevel > 0)
                        {
                            run.Text = Regex.Replace(run.Text, @"\n+\t+\n*$", "");
                        }

                        if (run.Text != "")
                        {
                            LastInlines.Add(run);
                        }
                    }
                }
            }
        }

        private static void ProcessSpan(IElement element, Span span, RichTextBlock richText, double width, bool isNewParagraph, TextAlignment lastAlignment)
        {
            if (!isNewParagraph)
            {
                ElementStack.Add(element);
            }

            var oldInline = LastInlines;
            LastInlines = span.Inlines;

            if (!isNewParagraph)
            {
                ParseElements(element.ChildNodes.ToList(), false, richText, width, lastAlignment);
                LastInlines = oldInline;
                ElementStack.Remove(element);
            }
        }
        
        private static void CreateParagraph(IElement root, RichTextBlock richText, double width, bool empty, TextAlignment lastAlignment, bool? orderedList = null, int listLevel = 0)
        {
            CurrentParagraph = new Paragraph();
            LastInlines = CurrentParagraph.Inlines;

            if (orderedList.HasValue && root.TagName == "LI")
            {
                CurrentParagraph.TextIndent = listLevel * 10;
                var run = new Run();
                CurrentParagraph.Inlines.Add(run);

                if (orderedList.Value)
                {
                    run.Text = $"{ListIndex}. ";
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
                CurrentParagraph.TextAlignment = TextAlignment.Left;
            } else if (alignment == "center")
            {
                CurrentParagraph.TextAlignment = TextAlignment.Center;
            }
            else if (alignment == "right")
            {
                CurrentParagraph.TextAlignment = TextAlignment.Right;
            }
            else if (alignment == "justify")
            {
                CurrentParagraph.TextAlignment = TextAlignment.Justify;
            }
            else if (alignment == "start")
            {
                CurrentParagraph.TextAlignment = TextAlignment.Start;
            }
            else if (alignment == "end")
            {
                CurrentParagraph.TextAlignment = TextAlignment.End;
            }
            else
            {
                CurrentParagraph.TextAlignment = lastAlignment;
            }

            richText.Blocks.Add(CurrentParagraph);
            ParseElements(ElementStack, true, richText, width, lastAlignment, orderedList, listLevel);

            if (!empty)
            {
                ParseElements(root.ChildNodes.ToList(), false, richText, width, lastAlignment, orderedList, listLevel);
            }
        }



        private static void ProcessTable(IElement tableNode, StackPanel table, Brush brush, TextAlignment lastAlignment, double parentWidth)
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
                    } else
                    {
                        var colNumbers = elementsToTraverse.Where(c => c.TagName == "COL").Count();
                        var colWidth = 100.0 / colNumbers;

                        columnDefinitions.Add(new Microsoft.UI.Xaml.GridLength(colWidth, Microsoft.UI.Xaml.GridUnitType.Star));
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

                    lastRow = elementsToTraverse.Where(e => e.TagName == "TR").Last() == element;
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

                    if (++cellIndex == totalCells)
                    {
                        cell.BorderThickness = new Thickness(borderWidth, topBorder, borderWidth, bottomBorder);
                    }
                    else
                    {
                        cell.BorderThickness = new Thickness(borderWidth, topBorder, 0, bottomBorder);
                    }

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

                    var richTextBlock = new RichTextBlock();
                    richTextBlock.HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch;
                    cell.HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch;

                    var oldParagraph = CurrentParagraph;
                    var lastInlines = LastInlines;

                    var insideWidth = 0.0;
                    if (columnDef.Width.IsStar)
                    {
                        insideWidth = parentWidth * (columnDef.Width.Value / 100);
                    } else
                    {
                        insideWidth = columnDef.Width.Value;
                    }

                    insideWidth = Math.Max(insideWidth - 10, 0);

                    CreateParagraph(element, richTextBlock, insideWidth, false, lastAlignment);
                    TrimBlock(richTextBlock);
                    CurrentParagraph = oldParagraph;
                    LastInlines = lastInlines;

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


        private static void TrimBlock(RichTextBlock richText)
        {
            var blocks = richText.Blocks;

            foreach (var block in blocks)
            {
                if (block is Paragraph && ((Paragraph)block).Inlines.Count == 0)
                {
                    richText.Blocks.Remove(block);
                }
            }
        }
    }
}

