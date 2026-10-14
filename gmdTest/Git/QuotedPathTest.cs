using gmd.Git.Private;

namespace gmdTest.Git;

[TestClass]
public class QuotedPathTest
{
    // As git writes them: the bytes of a character that is not ASCII as octal escapes, by default,
    // and '"', '\' and the control characters as the escapes of a C string
    [TestMethod]
    [DataRow("plain.txt", "plain.txt")]
    [DataRow("a b.txt", "a b.txt")]
    [DataRow("\"a b.txt\"", "a b.txt")]
    [DataRow(@"""\303\204rende.md""", "Ärende.md")]
    [DataRow(@"""b\303\244se.txt""", "bäse.txt")]
    [DataRow(@"""\360\237\230\200.txt""", "😀.txt")]
    [DataRow(@"""q\""t.txt""", "q\"t.txt")]
    [DataRow(@"""back\\slash""", @"back\slash")]
    [DataRow(@"""tab\tx\nline""", "tab\tx\nline")]
    [DataRow("\"ä\\tö\"", "ä\tö")] // core.quotePath off, which leaves what is not ASCII as it is
    [DataRow("\"\"", "")]
    [DataRow("\"", "\"")] // Not quoted, a lone quote
    public void TestUnquote(string text, string expected)
    {
        Assert.AreEqual(expected, QuotedPath.Unquote(text));
    }

    [TestMethod]
    [DataRow(@"""a"" ""b""", 3)]
    [DataRow(@"""a\"" b"" rest", 7)]
    [DataRow(@"""a\\"" b", 5)]
    [DataRow(@"""no end", 7)]
    public void TestEndOfQuoted(string text, int expected)
    {
        Assert.AreEqual(expected, QuotedPath.EndOfQuoted(text));
    }
}
