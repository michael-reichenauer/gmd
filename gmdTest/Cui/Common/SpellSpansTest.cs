using gmd.Common.Spelling;
using gmd.Cui.Common;
using gmdTest.Fixtures;
using Terminal.Gui;

namespace gmdTest.Cui.Common;

[TestClass]
public class SpellSpansTest
{
    // The misspelled words of "Fix resonable issu"
    static readonly WordSpan Resonable = new(4, 9, "resonable");
    static readonly WordSpan Issu = new(14, 4, "issu");
    static readonly WordSpan[] Both = [Resonable, Issu];

    [TestMethod]
    public void TestAt()
    {
        Assert.IsNull(SpellSpans.At(Both, 3));
        Assert.AreEqual(Resonable, SpellSpans.At(Both, 4));
        Assert.AreEqual(Resonable, SpellSpans.At(Both, 12));
        Assert.IsNull(SpellSpans.At(Both, 13));
        Assert.AreEqual(Issu, SpellSpans.At(Both, 14));
        Assert.IsNull(SpellSpans.At(Both, 18));
    }

    [TestMethod]
    public void TestIsBeingTypedWhenTheCaretIsAtTheEnd()
    {
        Assert.IsTrue(SpellSpans.IsBeingTyped(Resonable, 13));
        Assert.IsFalse(SpellSpans.IsBeingTyped(Resonable, 12));
        Assert.IsFalse(SpellSpans.IsBeingTyped(Resonable, 14));
    }

    [TestMethod]
    public void TestNextFromIsTheWordAtOrAfterTheCaret()
    {
        IReadOnlyList<IReadOnlyList<WordSpan>> lines = [Both];

        Assert.AreEqual((0, Resonable), SpellSpans.NextFrom(lines, 0, 0));
        Assert.AreEqual((0, Resonable), SpellSpans.NextFrom(lines, 0, 4));
        Assert.AreEqual((0, Resonable), SpellSpans.NextFrom(lines, 0, 13)); // At the end of the word still counts
        Assert.AreEqual((0, Issu), SpellSpans.NextFrom(lines, 0, 14));
        Assert.AreEqual((0, Issu), SpellSpans.NextFrom(lines, 0, 18));
        Assert.AreEqual((0, Resonable), SpellSpans.NextFrom(lines, 0, 19)); // Past the last one wraps around
    }

    [TestMethod]
    public void TestNextFromAcrossLines()
    {
        IReadOnlyList<IReadOnlyList<WordSpan>> lines =
        [
            [Resonable],
            [],
            [Issu],
        ];

        Assert.AreEqual((0, Resonable), SpellSpans.NextFrom(lines, 0, 0));
        Assert.AreEqual((2, Issu), SpellSpans.NextFrom(lines, 0, 14));
        Assert.AreEqual((2, Issu), SpellSpans.NextFrom(lines, 1, 0));
        Assert.AreEqual((2, Issu), SpellSpans.NextFrom(lines, 2, 18));
        Assert.AreEqual((0, Resonable), SpellSpans.NextFrom(lines, 2, 19));
    }

    [TestMethod]
    public void TestNextFromIsNullWhenNothingIsMisspelled()
    {
        Assert.IsNull(
            SpellSpans.NextFrom(
                [
                    [],
                    [],
                ],
                0,
                0
            )
        );
        Assert.IsNull(SpellSpans.NextFrom([], 0, 0));
    }

    [TestMethod]
    public void TestReplacePutsTheCaretAfterTheWord()
    {
        Assert.AreEqual(("Fix reasonable issu", 14), SpellSpans.Replace("Fix resonable issu", Resonable, "reasonable"));
        Assert.AreEqual(("Fix resonable issue", 19), SpellSpans.Replace("Fix resonable issu", Issu, "issue"));
    }

    [TestMethod]
    public void TestReplaceCountsRunesNotChars()
    {
        // The party popper is one rune but two chars; spans are in runes, as the views count
        var line = "🎉 issu";
        var spans = SpellScanner.Words(SpellSpans.LineText(line));
        Assert.AreEqual(new WordSpan(2, 4, "issu"), spans[0]);

        Assert.AreEqual(("🎉 issue", 7), SpellSpans.Replace(line, spans[0], "issue"));
    }

    [TestMethod]
    public void TestLineTextIsOneCharPerRune()
    {
        Assert.AreEqual("a�b", SpellSpans.LineText("a🎉b"));
        Assert.AreEqual("ab", SpellSpans.LineText(new List<Rune> { new Rune('a'), new Rune('b') }));
        Assert.AreEqual("a�b", SpellSpans.LineText(new List<Rune> { new Rune('a'), new Rune(0x1F389), new Rune('b') }));
    }

    [TestMethod]
    public void TestLinesSplitOnEitherNewline()
    {
        CollectionAssert.AreEqual(new[] { "a", "b" }, SpellSpans.Lines("a\nb"));
        CollectionAssert.AreEqual(new[] { "a", "b" }, SpellSpans.Lines("a\r\nb"));
        CollectionAssert.AreEqual(new[] { "" }, SpellSpans.Lines(""));
    }

    [TestMethod]
    public void TestIsSpellKey()
    {
        Assert.IsTrue(SpellSpans.IsSpellKey(Key.F7));
        Assert.IsTrue(SpellSpans.IsSpellKey(Key.G | Key.CtrlMask));
        Assert.IsFalse(SpellSpans.IsSpellKey(Key.G));
        Assert.IsFalse(SpellSpans.IsSpellKey(Key.F5));
    }

    [TestMethod]
    public void TestMenuItemsAreSuggestionsThenAddAndIgnore()
    {
        var checker = new FakeSpellChecker(["resonable"], new() { ["resonable"] = ["reasonable", "resolvable"] });
        string? replaced = null;
        int redraws = 0;

        var items = SpellSpans.MenuItems(checker, "resonable", with => replaced = with, () => redraws++).ToList();

        CollectionAssert.AreEqual(
            new[] { "reasonable", "resolvable", "", "Add 'resonable' to dictionary", "Ignore" },
            items.Select(i => i.Text).ToArray()
        );
        Assert.IsInstanceOfType<MenuSeparator>(items[2]);

        items[0].Action();
        Assert.AreEqual("reasonable", replaced);

        items[3].Action();
        CollectionAssert.AreEqual(new[] { "resonable" }, checker.Added);
        Assert.AreEqual(1, redraws);
    }

    [TestMethod]
    public void TestMenuItemsWithoutSuggestions()
    {
        var checker = new FakeSpellChecker(["brnch"]);

        var items = SpellSpans.MenuItems(checker, "brnch", _ => { }, () => { }).ToList();

        CollectionAssert.AreEqual(
            new[] { "(no suggestions)", "", "Add 'brnch' to dictionary", "Ignore" },
            items.Select(i => i.Text).ToArray()
        );
        Assert.IsFalse(items[0].CanExecute!());
    }
}
