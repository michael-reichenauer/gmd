using System.Text.RegularExpressions;
using gmd.Cui.Common;

namespace gmdTest.Fixtures;

// A grammar of two rules: 'int' is a keyword, and a comment runs from '/*' to '*/', over lines, which
// is what the state a line ends in carries. It keeps every line it is asked to color. For the tests
// of the code coloring of the diff and the blame, DiffSyntaxTest and BlameSyntaxTest.
class FakeCodeGrammar : ICodeGrammar
{
    public List<string> Lines { get; } = [];

    public CodeLine Tokenize(string line, object? state)
    {
        Lines.Add(line);
        if (state is true || line.Contains("/*"))
        {
            var start = state is true ? 0 : line.IndexOf("/*");
            var end = line.Contains("*/") ? line.IndexOf("*/") + 2 : line.Length;
            return new CodeLine([new CodeSpan(start, end - start, ColorName.CodeComment)], !line.Contains("*/"));
        }
        var spans = Regex
            .Matches(line, @"\bint\b")
            .Select(m => new CodeSpan(m.Index, m.Length, ColorName.CodeKeyword))
            .ToList();
        return new CodeLine(spans, false);
    }
}

// The grammar for a .cs file, and none for any other
class FakeCodeGrammars : ICodeGrammars
{
    readonly ICodeGrammar? grammar;

    public FakeCodeGrammars(ICodeGrammar? grammar) => this.grammar = grammar;

    public ICodeGrammar? For(string path) => path.EndsWith(".cs") ? grammar : null;
}
