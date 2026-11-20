using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace SmartLogistics.Web.Services;

/// <summary>A heading found while rendering, used for the document outline.</summary>
public sealed record MarkdownHeading(int Level, string Text, string Id);

/// <summary>Rendered HTML plus the outline of the document.</summary>
public sealed record RenderedMarkdown(string Html, IReadOnlyList<MarkdownHeading> Headings);

/// <summary>
/// Minimal Markdown to HTML renderer for the project's own documents (docs/*.md).
/// Supported: ATX headings (#), paragraphs, unordered and ordered lists (indented items are
/// marked as nested), pipe tables with alignment, fenced code blocks, block quotes,
/// horizontal rules, inline code and bold.
/// Every piece of text is HTML-encoded, so raw HTML in a document is shown as text, never executed.
/// </summary>
public static partial class MarkdownRenderer
{
    public static RenderedMarkdown Render(string markdown)
    {
        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        var html = new StringBuilder();
        var headings = new List<MarkdownHeading>();
        var usedIds = new HashSet<string>();
        var paragraph = new List<string>();

        void FlushParagraph()
        {
            if (paragraph.Count == 0)
            {
                return;
            }

            html.Append("<p>").Append(Inline(string.Join(" ", paragraph))).Append("</p>\n");
            paragraph.Clear();
        }

        var i = 0;
        while (i < lines.Length)
        {
            var line = lines[i];
            var trimmed = line.Trim();

            if (trimmed.Length == 0)
            {
                FlushParagraph();
                i++;
            }
            else if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                FlushParagraph();
                i = RenderCodeBlock(lines, i, html);
            }
            else if (HeadingPattern().Match(trimmed) is { Success: true } heading)
            {
                FlushParagraph();
                var level = heading.Groups["hashes"].Length;
                var text = heading.Groups["text"].Value.TrimEnd('#', ' ');
                var id = UniqueId(text, usedIds);
                headings.Add(new MarkdownHeading(level, PlainText(text), id));
                html.Append($"<h{level} id=\"{id}\">").Append(Inline(text)).Append($"</h{level}>\n");
                i++;
            }
            else if (HorizontalRulePattern().IsMatch(trimmed))
            {
                FlushParagraph();
                html.Append("<hr />\n");
                i++;
            }
            else if (IsTableStart(lines, i))
            {
                FlushParagraph();
                i = RenderTable(lines, i, html);
            }
            else if (ListItemPattern().IsMatch(line))
            {
                FlushParagraph();
                i = RenderList(lines, i, html);
            }
            else if (trimmed.StartsWith('>'))
            {
                FlushParagraph();
                i = RenderQuote(lines, i, html);
            }
            else
            {
                paragraph.Add(trimmed);
                i++;
            }
        }

        FlushParagraph();
        return new RenderedMarkdown(html.ToString(), headings);
    }

    private static int RenderCodeBlock(string[] lines, int start, StringBuilder html)
    {
        html.Append("<pre class=\"md-code\"><code>");
        var i = start + 1;
        while (i < lines.Length && !lines[i].Trim().StartsWith("```", StringComparison.Ordinal))
        {
            html.Append(WebUtility.HtmlEncode(lines[i])).Append('\n');
            i++;
        }

        html.Append("</code></pre>\n");
        return i + 1; // skip the closing fence
    }

    // A table starts with a "| ... |" row followed by a separator row such as "|---|:---:|".
    private static bool IsTableStart(string[] lines, int i)
    {
        if (i + 1 >= lines.Length || !lines[i].TrimStart().StartsWith('|'))
        {
            return false;
        }

        var separator = lines[i + 1].Trim();
        return separator.Contains('-') && separator.All(c => c is '|' or ':' or '-' or ' ');
    }

    private static int RenderTable(string[] lines, int start, StringBuilder html)
    {
        var header = SplitRow(lines[start]);
        var alignments = SplitRow(lines[start + 1]).Select(AlignmentClass).ToArray();

        html.Append("<div class=\"table-responsive\"><table class=\"table table-sm table-bordered md-table\"><thead><tr>");
        for (var c = 0; c < header.Length; c++)
        {
            html.Append($"<th{ClassAttribute(alignments, c)}>").Append(Inline(header[c])).Append("</th>");
        }

        html.Append("</tr></thead><tbody>\n");

        var i = start + 2;
        while (i < lines.Length && lines[i].TrimStart().StartsWith('|'))
        {
            html.Append("<tr>");
            var cells = SplitRow(lines[i]);
            for (var c = 0; c < cells.Length; c++)
            {
                html.Append($"<td{ClassAttribute(alignments, c)}>").Append(Inline(cells[c])).Append("</td>");
            }

            html.Append("</tr>\n");
            i++;
        }

        html.Append("</tbody></table></div>\n");
        return i;
    }

    // Splits "| a | b \| c |" into ["a", "b | c"]; "\|" is an escaped pipe inside a cell.
    private static string[] SplitRow(string row)
    {
        var trimmed = row.Trim();
        if (trimmed.StartsWith('|'))
        {
            trimmed = trimmed[1..];
        }

        if (trimmed.EndsWith('|') && !trimmed.EndsWith("\\|", StringComparison.Ordinal))
        {
            trimmed = trimmed[..^1];
        }

        return UnescapedPipePattern().Split(trimmed)
            .Select(cell => cell.Replace("\\|", "|").Trim())
            .ToArray();
    }

    private static string? AlignmentClass(string separatorCell) => separatorCell switch
    {
        _ when separatorCell.StartsWith(':') && separatorCell.EndsWith(':') => "text-center",
        _ when separatorCell.EndsWith(':') => "text-end",
        _ => null,
    };

    private static string ClassAttribute(string?[] alignments, int column) =>
        column < alignments.Length && alignments[column] is { } css ? $" class=\"{css}\"" : string.Empty;

    private static int RenderList(string[] lines, int start, StringBuilder html)
    {
        var first = ListItemPattern().Match(lines[start]);
        var ordered = IsOrderedMarker(first.Groups["marker"].Value);
        var tag = ordered ? "ol" : "ul";
        html.Append($"<{tag} class=\"md-list\">\n");

        string? currentText = null;
        var currentNested = false;

        void FlushItem()
        {
            if (currentText is null)
            {
                return;
            }

            var css = currentNested ? " class=\"md-nested\"" : string.Empty;
            html.Append($"<li{css}>").Append(Inline(currentText)).Append("</li>\n");
            currentText = null;
        }

        var i = start;
        while (i < lines.Length)
        {
            var line = lines[i];
            var item = ListItemPattern().Match(line);

            if (item.Success)
            {
                var indent = item.Groups["indent"].Value.Replace("\t", "    ").Length;
                var isTopLevel = indent < 2;

                // A top-level item of the other list type starts a new list.
                if (isTopLevel && IsOrderedMarker(item.Groups["marker"].Value) != ordered)
                {
                    break;
                }

                FlushItem();
                currentText = item.Groups["text"].Value.Trim();
                currentNested = !isTopLevel;
                i++;
            }
            else if (line.Trim().Length == 0)
            {
                // A blank line ends the list unless the next non-blank line is another item.
                var next = i + 1;
                while (next < lines.Length && lines[next].Trim().Length == 0)
                {
                    next++;
                }

                if (next < lines.Length && ListItemPattern().IsMatch(lines[next]))
                {
                    i = next;
                }
                else
                {
                    break;
                }
            }
            else if (char.IsWhiteSpace(line[0]) && currentText is not null)
            {
                // Indented continuation line belongs to the current item.
                currentText += " " + line.Trim();
                i++;
            }
            else
            {
                break;
            }
        }

        FlushItem();
        html.Append($"</{tag}>\n");
        return i;
    }

    private static bool IsOrderedMarker(string marker) => char.IsDigit(marker[0]);

    private static int RenderQuote(string[] lines, int start, StringBuilder html)
    {
        var parts = new List<string>();
        var i = start;
        while (i < lines.Length && lines[i].TrimStart().StartsWith('>'))
        {
            parts.Add(lines[i].TrimStart()[1..].Trim());
            i++;
        }

        html.Append("<blockquote class=\"md-quote\"><p>").Append(Inline(string.Join(" ", parts))).Append("</p></blockquote>\n");
        return i;
    }

    // Inline formatting. Code spans are swapped for placeholders first, so their content is never
    // formatted and **bold** may still wrap them (e.g. **`Total` mode**); then the text is encoded,
    // bold is applied and the code spans are put back.
    /// <summary>Renders one line of inline Markdown (`code`, **bold**) to encoded HTML.</summary>
    public static string RenderInline(string text) => Inline(text);

    private static string Inline(string text)
    {
        var codeSpans = new List<string>();
        var withPlaceholders = CodeSpanPattern().Replace(text, code =>
        {
            codeSpans.Add(WebUtility.HtmlEncode(code.Groups["code"].Value));
            return $"{codeSpans.Count - 1}"; // private-use characters, never encoded
        });

        var html = Bold(WebUtility.HtmlEncode(withPlaceholders));
        return PlaceholderPattern().Replace(html, p => $"<code>{codeSpans[int.Parse(p.Groups["index"].Value)]}</code>");
    }

    private static string Bold(string encoded) => BoldPattern().Replace(encoded, "<strong>${text}</strong>");

    private static string PlainText(string text) => text.Replace("**", string.Empty).Replace("`", string.Empty);

    // "Izvedene činjenice" -> "izvedene-činjenice"; duplicates get a numeric suffix.
    private static string UniqueId(string text, HashSet<string> usedIds)
    {
        var slug = new StringBuilder();
        foreach (var c in PlainText(text).ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c))
            {
                slug.Append(c);
            }
            else if (slug.Length > 0 && slug[^1] != '-')
            {
                slug.Append('-');
            }
        }

        var baseId = slug.ToString().Trim('-');
        if (baseId.Length == 0)
        {
            baseId = "section";
        }

        var id = baseId;
        for (var n = 2; !usedIds.Add(id); n++)
        {
            id = $"{baseId}-{n}";
        }

        // Safe as-is in an HTML attribute: only letters, digits and '-'. Razor encodes it again where needed.
        return id;
    }

    [GeneratedRegex(@"^(?<hashes>#{1,6})\s+(?<text>.+)$")]
    private static partial Regex HeadingPattern();

    [GeneratedRegex(@"^(-{3,}|\*{3,}|_{3,})$")]
    private static partial Regex HorizontalRulePattern();

    [GeneratedRegex(@"^(?<indent>\s*)(?<marker>[-*+]|\d+\.)\s+(?<text>.*)$")]
    private static partial Regex ListItemPattern();

    [GeneratedRegex(@"(?<!\\)\|")]
    private static partial Regex UnescapedPipePattern();

    [GeneratedRegex(@"`(?<code>[^`]+)`")]
    private static partial Regex CodeSpanPattern();

    [GeneratedRegex("(?<index>\\d+)")]
    private static partial Regex PlaceholderPattern();

    [GeneratedRegex(@"\*\*(?<text>.+?)\*\*")]
    private static partial Regex BoldPattern();
}
