using WeCantSpell.Hunspell;

namespace gmd.Common.Spelling;

// Spell checks single words against the embedded dictionary plus the words the user has added
interface ISpellChecker
{
    // False when the user has turned spell checking off, or no dictionary could be loaded
    bool IsEnabled { get; }

    bool IsMisspelled(string word);

    IReadOnlyList<string> Suggest(string word);

    // Adds a word to the live dictionary and to the user's config, so it stays known
    void AddToDictionary(string word);

    // Loads the dictionary on a background thread, so the first dialog does not have to
    void WarmUp();
}

// A managed Hunspell (WeCantSpell.Hunspell) over the SCOWL en_US dictionary embedded in the
// binary, or over a Hunspell dictionary of the user's choosing when Config.SpellDictionary is set.
// The word list is loaded once, lazily; a load failure is logged and leaves the checker disabled
// rather than failing anything that draws. Meant to be called on the UI thread only.
[SingleInstance]
class SpellChecker : ISpellChecker
{
    const string DicResource = "gmd.doc.spelling.en_US.dic";
    const string AffResource = "gmd.doc.spelling.en_US.aff";
    const int MaxSuggestions = 6;
    const int MaxCachedWords = 5000;

    readonly Config config;
    readonly Lazy<WordList?> wordList;
    readonly Dictionary<string, bool> isMisspelledCache = [];

    internal SpellChecker(Config config)
    {
        this.config = config;
        wordList = new Lazy<WordList?>(Load, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public bool IsEnabled => config.SpellCheck && wordList.Value != null;

    public bool IsMisspelled(string word)
    {
        if (!IsEnabled)
            return false;
        if (isMisspelledCache.TryGetValue(word, out var isMisspelled))
            return isMisspelled;

        isMisspelled = !wordList.Value!.Check(word);
        if (isMisspelledCache.Count >= MaxCachedWords)
            isMisspelledCache.Clear();
        isMisspelledCache[word] = isMisspelled;
        return isMisspelled;
    }

    public IReadOnlyList<string> Suggest(string word)
    {
        if (!IsEnabled)
            return [];
        return wordList.Value!.Suggest(word).Take(MaxSuggestions).ToList();
    }

    public void AddToDictionary(string word)
    {
        if (word == "" || wordList.Value == null)
            return;

        wordList.Value.Add(word);
        isMisspelledCache.Clear();
        if (!config.SpellWords.Contains(word, StringComparer.OrdinalIgnoreCase))
            config.Set(c => c.SpellWords.Add(word));
    }

    public void WarmUp() => Task.Run(() => wordList.Value).RunInBackground();

    WordList? Load()
    {
        var t = Timing.Start();
        var source = config.SpellDictionary != "" ? config.SpellDictionary : DicResource;
        if (
            !Try(
                out var list,
                out var e,
                config.SpellDictionary != "" ? LoadFiles(config.SpellDictionary) : LoadEmbedded()
            )
        )
        {
            Log.Error($"Failed to load spell check dictionary '{source}', {e}");
            return null;
        }

        foreach (var word in config.SpellWords)
            list.Add(word);

        Log.Info($"Loaded spell check dictionary '{source}' and {config.SpellWords.Count} user words in {t}");
        return list;
    }

    static R<WordList> LoadEmbedded()
    {
        if (!Try(out var dic, out var e, Files.GetEmbeddedFileStream(DicResource)))
            return e;
        using (dic)
        {
            if (!Try(out var aff, out e, Files.GetEmbeddedFileStream(AffResource)))
                return e;
            using (aff)
            {
                if (!Try(out var list, out e, () => WordList.CreateFromStreams(dic, aff)))
                    return e;
                return list;
            }
        }
    }

    // A Hunspell dictionary on disk: the .dic path, with the .aff expected beside it
    static R<WordList> LoadFiles(string dicPath)
    {
        if (!Try(out var list, out var e, () => WordList.CreateFromFiles(dicPath)))
            return e;
        return list;
    }
}
