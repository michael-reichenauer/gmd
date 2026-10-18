using TextMateSharp.Grammars;
using TextMateSharp.Registry;

namespace gmd.Cui.Common;

// The grammars code is colored by, of its language, which a file's extension names: VS Code's
// TextMate grammars, through TextMateSharp, which Terminal.Gui depends on for its own Markdown view.
// Their regular expressions are run by Oniguruma, a native library embedded in gmd's single file
// (OnigurumaLibrary); wherever it cannot be loaded there are no grammars, and code is drawn as it
// always was, in the text's color.
interface ICodeGrammars
{
    // The grammar of a file's language, or null for a file no grammar is for
    ICodeGrammar? For(string path);
}

// A language's grammar. A line is colored given the state the line before it ended in, null for the
// first one, since what a line is depends on the lines before, e.g. inside a comment of many lines.
interface ICodeGrammar
{
    CodeLine Tokenize(string line, object? state);
}

// The colored parts of a line, and the state it ended in, which the next line is colored from
record CodeLine(IReadOnlyList<CodeSpan> Spans, object? State)
{
    public static readonly CodeLine None = new([], null);
}

// Length chars of a line from Start, in a code color
readonly record struct CodeSpan(int Start, int Length, ColorName Color);

[SingleInstance]
class CodeGrammars : ICodeGrammars
{
    // How long a line may take to color, past which the rest of it is drawn as it was, as VS Code
    // gives up on a line rather than stall, and how long a line may be to be colored at all. As long
    // as VS Code gives a line: the first lines pay for compiling the grammar's regular expressions, and
    // with 50 ms, eight gmds starting at once left the end of a diff's first line uncolored.
    static readonly TimeSpan LineTimeout = TimeSpan.FromMilliseconds(500);
    internal const int MaxLineLength = 2000;

    readonly Dictionary<string, ICodeGrammar?> byExtension = [];
    RegistryOptions? options;
    Registry? registry;
    bool isUnavailable;

    public ICodeGrammar? For(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        if (isUnavailable || extension == "")
            return null;
        if (byExtension.TryGetValue(extension, out var known))
            return known;

        var grammar = Load(extension);
        byExtension[extension] = grammar;
        return grammar;
    }

    ICodeGrammar? Load(string extension)
    {
        try
        {
            OnigurumaLibrary.Register();
            options ??= new RegistryOptions(ThemeName.DarkPlus);
            registry ??= new Registry(options);
            var scope = options.GetScopeByExtension(extension);
            if (scope == null)
                return null;
            var grammar = registry.LoadGrammar(scope);
            return grammar == null ? null : new Grammar(grammar, this);
        }
        catch (Exception e) when (e.IsNotFatal())
        {
            Log.Warn($"No grammar for '{extension}': {e.Message}");
            return null;
        }
    }

    // Oniguruma is loaded as the first line is colored, so that is where a platform with no build of
    // it shows: then no line is colored, and none is tried again
    void SetUnavailable(Exception e)
    {
        if (isUnavailable)
            return;
        isUnavailable = true;
        byExtension.Clear();
        Log.Warn($"Code is not colored, its regular expressions cannot be run: {e.Message}");
    }

    class Grammar : ICodeGrammar
    {
        readonly IGrammar grammar;
        readonly CodeGrammars grammars;

        public Grammar(IGrammar grammar, CodeGrammars grammars)
        {
            this.grammar = grammar;
            this.grammars = grammars;
        }

        public CodeLine Tokenize(string line, object? state)
        {
            if (grammars.isUnavailable || line.Length > MaxLineLength)
                return CodeLine.None with { State = state };

            try
            {
                var result = grammar.TokenizeLine(line, state as IStateStack, LineTimeout);
                if (result.Tokens.Length > 0 && result.Tokens[^1].EndIndex < line.Length)
                    Log.Info($"Colored a line in part, it took longer than {LineTimeout.TotalMilliseconds} ms");
                List<CodeSpan> spans = [];
                foreach (var token in result.Tokens)
                {
                    var start = Math.Min(token.StartIndex, line.Length);
                    var end = Math.Min(token.EndIndex, line.Length);
                    if (end > start && CodeScopes.ColorOf(token.Scopes) is ColorName color)
                        spans.Add(new CodeSpan(start, end - start, color));
                }
                return new CodeLine(spans, result.RuleStack);
            }
            catch (Exception e)
                when (e is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
            {
                grammars.SetUnavailable(e);
                return CodeLine.None;
            }
            catch (TypeInitializationException e) when (e.InnerException is DllNotFoundException)
            {
                grammars.SetUnavailable(e);
                return CodeLine.None;
            }
            catch (Exception e) when (e.IsNotFatal())
            { // A grammar that fails on a line leaves that line as it was, and the lines after it
                Log.Warn($"Failed to color a line: {e.Message}");
                return CodeLine.None;
            }
        }
    }
}
