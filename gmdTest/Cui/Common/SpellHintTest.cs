using gmd.Cui.Common;
using gmdTest.Fixtures;

namespace gmdTest.Cui.Common;

[TestClass]
public class SpellHintTest
{
    [TestMethod]
    public void TestNothingMisspelledIsThePlainEdge()
    {
        var edge = SpellHint.Edge(0, 12, Color.Dark);

        Assert.AreEqual("└──────────┘", edge.ToString());
        Assert.AreEqual("DDDDDDDDDDDD", TextColors.Of(edge));
    }

    [TestMethod]
    public void TestTheHintIsLetIntoTheEdgeWithTheCountInRed()
    {
        var edge = SpellHint.Edge(1, 72, Color.Dark);

        Assert.AreEqual("└─ 1 misspelled word, F7 or right-click for suggestions ───────────────┘", edge.ToString());
        Assert.AreEqual(72, edge.Length);
        Assert.AreEqual(
            "DD r rrrrrrrrrr rrrrD DD DD DDDDDDDDDDD DDD DDDDDDDDDDD DDDDDDDDDDDDDDDD",
            TextColors.Of(edge),
            "The count is red, the rest is the frame's color"
        );

        Assert.AreEqual(
            "└─ 2 misspelled words, F7 or right-click for suggestions ──────────────┘",
            SpellHint.Edge(2, 72, Color.Dark).ToString()
        );
    }

    [TestMethod]
    public void TestANarrowFrameGetsTheCountAloneAndANarrowerThePlainEdge()
    {
        Assert.AreEqual("└─ 3 misspelled words ──┘", SpellHint.Edge(3, 25, Color.Dark).ToString());
        Assert.AreEqual("└───────────────┘", SpellHint.Edge(3, 17, Color.Dark).ToString());
    }
}
