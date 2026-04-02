using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;

namespace BabaShell.Vsix;

internal sealed class BabaShellClassifier : IClassifier
{
    private readonly IClassificationType _keyword;
    private readonly IClassificationType _string;
    private readonly IClassificationType _number;
    private readonly IClassificationType _comment;

    private static readonly Regex KeywordRegex = new(@"\b(emit|when|else|loop|func|return|import|true|false|null|and|or|map)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex StringRegex = new(@"""([^""\\]|\\.)*""", RegexOptions.Compiled);
    private static readonly Regex NumberRegex = new(@"\b\d+(\.\d+)?\b", RegexOptions.Compiled);
    private static readonly Regex CommentRegex = new(@"//.*$", RegexOptions.Compiled);

    public BabaShellClassifier(IClassificationTypeRegistryService registry)
    {
        _keyword = registry.GetClassificationType("keyword");
        _string = registry.GetClassificationType("string");
        _number = registry.GetClassificationType("number");
        _comment = registry.GetClassificationType("comment");
    }

    public event EventHandler<ClassificationChangedEventArgs>? ClassificationChanged;

    public IList<ClassificationSpan> GetClassificationSpans(SnapshotSpan span)
    {
        var spans = new List<ClassificationSpan>();
        var snapshot = span.Snapshot;

        var startLine = span.Start.GetContainingLine().LineNumber;
        var endLine = (span.End - 1).GetContainingLine().LineNumber;

        for (var i = startLine; i <= endLine; i++)
        {
            var line = snapshot.GetLineFromLineNumber(i);
            var text = line.GetText();

            foreach (Match m in CommentRegex.Matches(text))
            {
                spans.Add(new ClassificationSpan(new SnapshotSpan(line.Start + m.Index, m.Length), _comment));
            }

            foreach (Match m in StringRegex.Matches(text))
            {
                spans.Add(new ClassificationSpan(new SnapshotSpan(line.Start + m.Index, m.Length), _string));
            }

            foreach (Match m in NumberRegex.Matches(text))
            {
                spans.Add(new ClassificationSpan(new SnapshotSpan(line.Start + m.Index, m.Length), _number));
            }

            foreach (Match m in KeywordRegex.Matches(text))
            {
                spans.Add(new ClassificationSpan(new SnapshotSpan(line.Start + m.Index, m.Length), _keyword));
            }
        }

        return spans;
    }
}
