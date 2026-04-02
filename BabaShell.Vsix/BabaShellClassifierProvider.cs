using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace BabaShell.Vsix;

[Export(typeof(IClassifierProvider))]
[ContentType("babashell")]
internal sealed class BabaShellClassifierProvider : IClassifierProvider
{
    [Import]
    internal IClassificationTypeRegistryService ClassificationRegistry = null!;

    public IClassifier? GetClassifier(ITextBuffer textBuffer)
    {
        return textBuffer.Properties.GetOrCreateSingletonProperty(() => new BabaShellClassifier(ClassificationRegistry));
    }
}
