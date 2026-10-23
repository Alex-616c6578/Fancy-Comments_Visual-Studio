using System.IO;

namespace FancyComments.Model
{
    /// <summary>
    /// One way of writing a comment in a language, e.g. "/*" … "*/" or "#" with no closer.
    /// A fancy comment is the universal "## … ##" marker wrapped in one of these.
    /// </summary>
    internal sealed class CommentSyntax
    {
        public CommentSyntax(string open, string close = "")
        {
            this.Open = open;
            this.Close = close;
        }

        public string Open { get; }

        public string Close { get; }

        // "#" + "##" would fire while typing banners like "#####", so hash comments need a space: "# ##".
        public string Separator => this.Open.EndsWith("#") ? " " : "";

        /// <summary>What the user types to start a fancy comment, e.g. "/*##".</summary>
        public string Trigger => this.Open + this.Separator + "##";

        public string Format(string tag, string summary) => $"{this.Open}{this.Separator}## {tag} {summary} ##{this.Close}";
    }

    /// <summary>
    /// Comment syntaxes by file extension. Adding a language is one line here.
    /// </summary>
    internal static class CommentLanguages
    {
        private static readonly CommentSyntax[] CStyle = [new("/*", "*/"), new("//")];
        private static readonly CommentSyntax[] BlockOnly = [new("/*", "*/")];
        private static readonly CommentSyntax[] Hash = [new("#")];
        private static readonly CommentSyntax[] Markup = [new("<!--", "-->")];
        private static readonly CommentSyntax[] None = [];

        private static readonly Dictionary<string, CommentSyntax[]> ByExtension = new(StringComparer.OrdinalIgnoreCase)
        {
            [".cs"] = CStyle, [".c"] = CStyle, [".h"] = CStyle, [".cpp"] = CStyle, [".hpp"] = CStyle, [".cc"] = CStyle,
            [".java"] = CStyle, [".kt"] = CStyle, [".scala"] = CStyle, [".swift"] = CStyle, [".go"] = CStyle, [".rs"] = CStyle,
            [".js"] = CStyle, [".jsx"] = CStyle, [".ts"] = CStyle, [".tsx"] = CStyle, [".dart"] = CStyle, [".scss"] = CStyle, [".less"] = CStyle,
            [".css"] = BlockOnly,
            [".fs"] = [new("(*", "*)"), new("//")], [".fsx"] = [new("(*", "*)"), new("//")],
            [".py"] = Hash, [".ps1"] = Hash, [".psm1"] = Hash, [".sh"] = Hash, [".bash"] = Hash, [".rb"] = Hash,
            [".pl"] = Hash, [".r"] = Hash, [".yml"] = Hash, [".yaml"] = Hash, [".toml"] = Hash, [".cmake"] = Hash, ["Dockerfile"] = Hash,
            [".vb"] = [new("'")],
            [".sql"] = [new("/*", "*/"), new("--")], [".lua"] = [new("--")],
            [".bat"] = [new("::")], [".cmd"] = [new("::")],
            [".html"] = Markup, [".htm"] = Markup, [".xml"] = Markup, [".xaml"] = Markup, [".md"] = Markup,
            [".csproj"] = Markup, [".props"] = Markup, [".targets"] = Markup, [".config"] = Markup,
            [".razor"] = [new("@*", "*@"), new("<!--", "-->")], [".cshtml"] = [new("@*", "*@"), new("<!--", "-->")],
        };

        public static CommentSyntax[] For(string? filePath)
        {
            if (filePath is null)
            {
                return None;
            }

            return ByExtension.TryGetValue(Path.GetExtension(filePath), out CommentSyntax[]? syntaxes)
                || ByExtension.TryGetValue(Path.GetFileName(filePath), out syntaxes)
                ? syntaxes
                : None;
        }
    }
}
