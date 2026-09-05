using gmd.Common.Spelling;

namespace gmdTest.Fixtures;

// A spell checker with a word list and suggestions given by the test, for what sits above the
// dictionary: the scanning, the spans and the menu.
class FakeSpellChecker : ISpellChecker
{
    readonly HashSet<string> misspelled;
    readonly Dictionary<string, IReadOnlyList<string>> suggestions;

    public FakeSpellChecker(
        IEnumerable<string> misspelled,
        Dictionary<string, IReadOnlyList<string>>? suggestions = null
    )
    {
        this.misspelled = misspelled.ToHashSet();
        this.suggestions = suggestions ?? [];
    }

    public List<string> Added { get; } = [];

    public bool IsEnabled { get; set; } = true;

    public bool IsMisspelled(string word) => IsEnabled && misspelled.Contains(word);

    public IReadOnlyList<string> Suggest(string word) => suggestions.GetValueOrDefault(word, []);

    public void AddToDictionary(string word)
    {
        Added.Add(word);
        misspelled.Remove(word);
    }

    public void WarmUp() { }
}
