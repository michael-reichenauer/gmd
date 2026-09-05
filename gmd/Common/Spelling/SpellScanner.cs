namespace gmd.Common.Spelling;

// A word of a line that is worth spell checking, and where it sits in the line so it can be
// colored and replaced. Start and Length are indexes into the line as given to the scanner.
record WordSpan(int Start, int Length, string Word)
{
    public int End => Start + Length;

    public bool Contains(int index) => index >= Start && index < End;
}

// Which words of a line to spell check. A commit message is prose with code in it — identifiers,
// paths, shas, issue references, flags — and a dictionary knows none of that, so what is skipped
// here is what makes the checker a help rather than an annoyance. Pure string math: no dictionary
// and no view, so it is unit testable.
static class SpellScanner
{
    // Punctuation that surrounds a word without being part of it, e.g. "(word)." or 'word'
    const string Surrounding = ".,;:!?()[]{}<>\"'“”‘’*`-";

    // The words of a line that are prose, i.e. worth looking up
    public static IReadOnlyList<WordSpan> Words(string line)
    {
        List<WordSpan> words = [];
        int i = 0;
        while (i < line.Length)
        {
            if (char.IsWhiteSpace(line[i]))
            {
                i++;
                continue;
            }
            if (line[i] == '`')
            { // A `quoted` run is code and is skipped as a whole (an unmatched backtick is just punctuation)
                int close = line.IndexOf('`', i + 1);
                if (close > i)
                {
                    i = close + 1;
                    continue;
                }
            }

            int start = i;
            while (i < line.Length && !char.IsWhiteSpace(line[i]))
                i++;

            var word = Trim(line, start, i);
            if (word != null)
                words.Add(word);
        }

        return words;
    }

    // The words of a line that the dictionary does not know
    public static IReadOnlyList<WordSpan> Misspelled(string line, Func<string, bool> isMisspelled) =>
        Words(line).Where(w => isMisspelled(w.Word)).ToList();

    // Strips the surrounding punctuation of a whitespace separated chunk and returns the word,
    // or null if what is left is not a prose word.
    static WordSpan? Trim(string line, int start, int end)
    {
        while (start < end && Surrounding.Contains(line[start]))
            start++;
        while (end > start && Surrounding.Contains(line[end - 1]))
            end--;

        if (end - start < 2)
            return null;

        var word = line[start..end];
        return IsProse(word) ? new WordSpan(start, end - start, word) : null;
    }

    // A prose word is letters, with an inner apostrophe or hyphen allowed (don't, cherry-pick).
    // Anything with a digit or symbol in it is an identifier, path, url, sha or reference; a word
    // with a capital after its first letter is an identifier too (CommitDlg, GitHub), and an
    // all-capitals word is an acronym.
    static bool IsProse(string word)
    {
        bool hasLowerCase = false;
        for (int i = 0; i < word.Length; i++)
        {
            char c = word[i];
            if (char.IsLetter(c))
            {
                if (char.IsUpper(c) && i > 0)
                    return false;
                if (char.IsLower(c))
                    hasLowerCase = true;
                continue;
            }
            if (c != '\'' && c != '’' && c != '-')
                return false;
        }

        return hasLowerCase;
    }
}
