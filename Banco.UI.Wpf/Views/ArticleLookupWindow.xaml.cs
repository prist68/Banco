using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Linq;
using Banco.UI.Wpf.ViewModels;
using Banco.Vendita.Articles;

namespace Banco.UI.Wpf.Views;

public partial class ArticleLookupWindow : Window
{
    private readonly ArticleLookupViewModel _viewModel;

    public ArticleLookupWindow(ArticleLookupViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Loaded += OnLoaded;
        Closed += OnClosed;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    public GestionaleArticleSearchResult? SelectedArticle => _viewModel.SelectedArticle;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var workArea = SystemParameters.WorkArea;
        var targetWidth = Math.Min(Width, Math.Max(MinWidth, workArea.Width - 48));
        var targetHeight = Math.Min(Height, Math.Max(MinHeight, workArea.Height - 48));

        Width = targetWidth;
        Height = targetHeight;
        Left = workArea.Left + ((workArea.Width - Width) / 2);
        Top = workArea.Top + ((workArea.Height - Height) / 2);

        SearchTextBox.Focus();
        SearchTextBox.CaretIndex = SearchTextBox.Text?.Length ?? 0;
        RenderHtmlDocuments();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ArticleLookupViewModel.SelectedDetail)
            or nameof(ArticleLookupViewModel.ShortDescriptionHtmlDocument)
            or nameof(ArticleLookupViewModel.LongDescriptionHtmlDocument))
        {
            Dispatcher.Invoke(RenderHtmlDocuments);
        }
    }

    private void RenderHtmlDocuments()
    {
        LongDescriptionRichTextBox.Document = BuildHtmlFlowDocument(
            _viewModel.SelectedDetail?.DescrizioneLungaHtml,
            "Nessuna descrizione lunga presente nel legacy.");
    }

    private void Header_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void ConfirmButton_OnClick(object sender, RoutedEventArgs e)
    {
        ConfirmCurrentSelection();
    }

    private void ResultsListBox_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source &&
            FindParent<ListBoxItem>(source)?.DataContext is GestionaleArticleSearchResult article)
        {
            _viewModel.SelectedArticle = article;
            ResultsListBox.SelectedItem = article;
        }

        ConfirmCurrentSelection();
    }

    private void ConfirmCurrentSelection()
    {
        if (_viewModel.SelectedArticle is null &&
            ResultsListBox.SelectedItem is GestionaleArticleSearchResult selectedArticle)
        {
            _viewModel.SelectedArticle = selectedArticle;
        }

        if (_viewModel.SelectedArticle is null && ResultsListBox.Items.Count > 0)
        {
            ResultsListBox.SelectedIndex = 0;
        }

        if (_viewModel.SelectedArticle is null)
        {
            return;
        }

        if (!_viewModel.CanConfirmSelectedArticle)
        {
            _viewModel.NotifyVariantSelectionRequired();
            return;
        }

        DialogResult = true;
    }

    private static T? FindParent<T>(DependencyObject? source)
        where T : DependencyObject
    {
        while (source is not null)
        {
            if (source is T typed)
            {
                return typed;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return null;
    }

    private void ResultsListBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _viewModel.SelectedArticle is not null)
        {
            e.Handled = true;
            DialogResult = true;
        }
    }

    private void SearchTextBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Down)
        {
            if (ResultsListBox.Items.Count == 0)
            {
                return;
            }

            e.Handled = true;

            if (_viewModel.SelectedArticle is null)
            {
                ResultsListBox.SelectedIndex = 0;
            }
            else
            {
                ResultsListBox.SelectedItem = _viewModel.SelectedArticle;
            }

            ResultsListBox.Focus();
            if (ResultsListBox.SelectedItem is not null)
            {
                ResultsListBox.ScrollIntoView(ResultsListBox.SelectedItem);
            }

            return;
        }

        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;

        if (_viewModel.SelectedArticle is null && ResultsListBox.Items.Count > 0)
        {
            ResultsListBox.SelectedIndex = 0;
        }

        if (_viewModel.SelectedArticle is null)
        {
            return;
        }

        ResultsListBox.SelectedItem = _viewModel.SelectedArticle;
        Dispatcher.BeginInvoke(() =>
        {
            if (ResultsListBox.Items.Contains(_viewModel.SelectedArticle))
            {
                ResultsListBox.ScrollIntoView(_viewModel.SelectedArticle);
            }

            DialogResult = true;
        }, DispatcherPriority.Background);
    }

    private static FlowDocument BuildHtmlFlowDocument(string? html, string fallbackText)
    {
        var document = new FlowDocument
        {
            PagePadding = new Thickness(0),
            TextAlignment = TextAlignment.Left,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 13,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#23344f"))
        };

        if (string.IsNullOrWhiteSpace(html))
        {
            document.Blocks.Add(CreateParagraph(fallbackText));
            return document;
        }

        try
        {
            var normalized = NormalizeHtmlFragment(html);
            var root = XElement.Parse($"<root>{normalized}</root>", LoadOptions.PreserveWhitespace);
            AppendBlockNodes(document.Blocks, root.Nodes());
            if (document.Blocks.Count == 0)
            {
                document.Blocks.Add(CreateParagraph(fallbackText));
            }
        }
        catch
        {
            document.Blocks.Add(CreateParagraph(HtmlToPlainTextSafe(html)));
        }

        return document;
    }

    private static string NormalizeHtmlFragment(string html)
    {
        var normalized = html;
        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"<script[\s\S]*?</script>", string.Empty, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"<style[\s\S]*?</style>", string.Empty, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"<iframe[\s\S]*?</iframe>", string.Empty, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"<(br)\b([^>]*)>", "<br />", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"<(hr)\b([^>]*)>", "<hr />", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"&(?![a-zA-Z#0-9]+;)", "&amp;");
        return normalized;
    }

    private static void AppendBlockNodes(BlockCollection blocks, IEnumerable<XNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (node is XElement element)
            {
                switch (element.Name.LocalName.ToLowerInvariant())
                {
                    case "p":
                    case "div":
                    case "section":
                    case "article":
                        var paragraph = CreateParagraph();
                        ApplyStyleAttributes(paragraph, element);
                        AppendInlineNodes(paragraph.Inlines, element.Nodes());
                        if (!IsParagraphEmpty(paragraph))
                        {
                            blocks.Add(paragraph);
                        }
                        else if (element.Name.LocalName.Equals("p", StringComparison.OrdinalIgnoreCase))
                        {
                            blocks.Add(CreateSpacerParagraph());
                        }
                        break;
                    case "h1":
                    case "h2":
                    case "h3":
                    case "h4":
                        var heading = CreateParagraph();
                        heading.FontWeight = FontWeights.Bold;
                        heading.FontSize = element.Name.LocalName.ToLowerInvariant() switch
                        {
                            "h1" => 18,
                            "h2" => 16,
                            _ => 14
                        };
                        heading.Margin = new Thickness(0, 0, 0, 8);
                        ApplyStyleAttributes(heading, element);
                        AppendInlineNodes(heading.Inlines, element.Nodes());
                        if (!IsParagraphEmpty(heading))
                        {
                            blocks.Add(heading);
                        }
                        break;
                    case "ul":
                    case "ol":
                        var list = new List
                        {
                            MarkerStyle = element.Name.LocalName.Equals("ol", StringComparison.OrdinalIgnoreCase)
                                ? TextMarkerStyle.Decimal
                                : TextMarkerStyle.Disc,
                            Margin = new Thickness(18, 0, 0, 8)
                        };

                        foreach (var item in element.Elements().Where(static e => e.Name.LocalName.Equals("li", StringComparison.OrdinalIgnoreCase)))
                        {
                            var itemParagraph = CreateParagraph();
                            AppendInlineNodes(itemParagraph.Inlines, item.Nodes());
                            if (!IsParagraphEmpty(itemParagraph))
                            {
                                list.ListItems.Add(new ListItem(itemParagraph));
                            }
                        }

                        if (list.ListItems.Count > 0)
                        {
                            blocks.Add(list);
                        }
                        break;
                    case "hr":
                        blocks.Add(CreateParagraph(" "));
                        break;
                    default:
                        AppendBlockNodes(blocks, element.Nodes());
                        break;
                }
            }
            else if (node is XText textNode)
            {
                var text = NormalizeText(textNode.Value);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    blocks.Add(CreateParagraph(text));
                }
            }
        }
    }

    private static void AppendInlineNodes(InlineCollection inlines, IEnumerable<XNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (node is XText textNode)
            {
                var text = NormalizeText(textNode.Value);
                if (!string.IsNullOrEmpty(text))
                {
                    inlines.Add(new Run(text));
                }

                continue;
            }

            if (node is not XElement element)
            {
                continue;
            }

            Inline inline = element.Name.LocalName.ToLowerInvariant() switch
            {
                "strong" or "b" => new Bold(),
                "em" or "i" => new Italic(),
                "u" => new Underline(),
                "a" => new Underline { Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2f6ebd")) },
                "span" => new Span(),
                "br" => new LineBreak(),
                _ => new Span()
            };

            if (inline is LineBreak)
            {
                inlines.Add(inline);
                continue;
            }

            if (inline is Span span)
            {
                ApplyStyleAttributes(span, element);
                AppendInlineNodes(span.Inlines, element.Nodes());
                if (span.Inlines.Count > 0)
                {
                    inlines.Add(span);
                }
            }
        }
    }

    private static Paragraph CreateParagraph(string? text = null)
    {
        var paragraph = new Paragraph
        {
            Margin = new Thickness(0, 0, 0, 10),
            LineHeight = 19
        };

        if (!string.IsNullOrWhiteSpace(text))
        {
            paragraph.Inlines.Add(new Run(text));
        }

        return paragraph;
    }

    private static Paragraph CreateSpacerParagraph()
    {
        return new Paragraph(new Run(" "))
        {
            Margin = new Thickness(0, 0, 0, 10),
            LineHeight = 6
        };
    }

    private static bool IsParagraphEmpty(Paragraph paragraph)
    {
        if (paragraph.Inlines.Count == 0)
        {
            return true;
        }

        var range = new TextRange(paragraph.ContentStart, paragraph.ContentEnd);
        return string.IsNullOrWhiteSpace(range.Text);
    }

    private static string NormalizeText(string value)
    {
        return WebUtility.HtmlDecode(value.Replace('\u00A0', ' '));
    }

    private static void ApplyStyleAttributes(TextElement element, XElement source)
    {
        var styleValue = source.Attribute("style")?.Value;
        if (string.IsNullOrWhiteSpace(styleValue))
        {
            return;
        }

        foreach (var segment in styleValue.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separatorIndex = segment.IndexOf(':');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var property = segment[..separatorIndex].Trim().ToLowerInvariant();
            var value = segment[(separatorIndex + 1)..].Trim();

            switch (property)
            {
                case "font-size":
                    if (TryParseCssFontSize(value, out var fontSize))
                    {
                        element.FontSize = fontSize;
                    }

                    break;

                case "font-weight":
                    if (value.Contains("bold", StringComparison.OrdinalIgnoreCase) || value == "700")
                    {
                        element.FontWeight = FontWeights.Bold;
                    }

                    break;

                case "font-style":
                    if (value.Contains("italic", StringComparison.OrdinalIgnoreCase))
                    {
                        element.FontStyle = FontStyles.Italic;
                    }

                    break;

                case "color":
                    try
                    {
                        element.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
                    }
                    catch
                    {
                    }

                    break;
            }
        }
    }

    private static bool TryParseCssFontSize(string value, out double fontSize)
    {
        fontSize = 0;
        var normalized = value.Trim().ToLowerInvariant();

        if (normalized == "medium")
        {
            fontSize = 16;
            return true;
        }

        if (normalized == "small")
        {
            fontSize = 12;
            return true;
        }

        if (normalized == "large")
        {
            fontSize = 18;
            return true;
        }

        if (normalized.EndsWith("px", StringComparison.Ordinal))
        {
            normalized = normalized[..^2];
        }
        else if (normalized.EndsWith("pt", StringComparison.Ordinal))
        {
            normalized = normalized[..^2];
        }

        return double.TryParse(normalized, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out fontSize);
    }

    private static string HtmlToPlainTextSafe(string html)
    {
        var normalized = System.Text.RegularExpressions.Regex.Replace(html, @"<(br|/p|/div|/li|/h[1-6])\b[^>]*>", Environment.NewLine, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"<li\b[^>]*>", "- ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"<[^>]+>", string.Empty, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return WebUtility.HtmlDecode(normalized).Trim();
    }

    private static string HtmlToPlainText(string html)
    {
        var normalized = System.Text.RegularExpressions.Regex.Replace(html, @"<(br|/p|/div|/li|/h[1-6])\b[^>]*>", Environment.NewLine, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"<li\b[^>]*>", "• ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"<[^>]+>", string.Empty, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return WebUtility.HtmlDecode(normalized).Trim();
    }
}
