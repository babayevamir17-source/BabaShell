using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Utilities;

namespace BabaShell.Vsix;

internal static class BabaShellContentType
{
    [Export]
    [Name("babashell")]
    [BaseDefinition("code")]
    public static ContentTypeDefinition? BabashellContentTypeDefinition;

    [Export]
    [FileExtension(".babashell")]
    [ContentType("babashell")]
    public static FileExtensionToContentTypeDefinition? BabashellFileExtensionDefinition;
}
