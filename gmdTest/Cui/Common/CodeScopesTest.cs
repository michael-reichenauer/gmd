using gmd.Cui.Common;

namespace gmdTest.Cui.Common;

// The code color of a token by its TextMate scopes, outermost first as TextMate lists them
[TestClass]
public class CodeScopesTest
{
    static ColorName? ColorOf(params string[] scopes) => CodeScopes.ColorOf(scopes);

    [TestMethod]
    public void TestTheInnermostScopeARuleIsForDecides()
    {
        Assert.AreEqual(ColorName.CodeKeyword, ColorOf("source.cs", "storage.modifier.public.cs"));
        Assert.AreEqual(ColorName.CodeString, ColorOf("source.cs", "string.quoted.double.cs"));
        // A string's quote is punctuation, which no rule is for, so the string outside it decides
        Assert.AreEqual(
            ColorName.CodeString,
            ColorOf("source.cs", "string.quoted.double.cs", "punctuation.definition.string.begin.cs")
        );
        // An escape in a string is not the string's color
        Assert.AreEqual(
            ColorName.CodeEscape,
            ColorOf("source.cs", "string.quoted.double.cs", "constant.character.escape.cs")
        );
    }

    [TestMethod]
    public void TestATokenNoRuleIsForIsTheTextsColor()
    {
        Assert.IsNull(ColorOf("source.cs"));
        Assert.IsNull(ColorOf("source.cs", "punctuation.terminator.statement.cs"));
        Assert.IsNull(ColorOf());
    }

    // A rule of no color stops the scopes outside it: an operator is no keyword, though 'keyword' is
    // above it, nor code in a string's interpolation a string
    [TestMethod]
    public void TestARuleOfNoColorStopsTheScopesOutsideIt()
    {
        Assert.IsNull(ColorOf("source.cs", "keyword.operator.assignment.cs"));
        Assert.AreEqual(ColorName.CodeKeyword, ColorOf("source.cs", "keyword.operator.expression.new.cs"));
        Assert.IsNull(ColorOf("source.ts", "string.template.ts", "meta.template.expression.ts"));
        Assert.AreEqual(
            ColorName.CodeVariable,
            ColorOf("source.ts", "string.template.ts", "meta.template.expression.ts", "variable.other.readwrite.ts")
        );
    }

    // 'string' is for 'string.quoted', not for 'strings'
    [TestMethod]
    public void TestARuleIsForWholeParts()
    {
        Assert.IsNull(ColorOf("source.x", "strings.x"));
        Assert.AreEqual(ColorName.CodeString, ColorOf("source.x", "string"));
    }

    // The more specific rule goes first: a JSON key is a property name, not a type
    [TestMethod]
    public void TestAMoreSpecificRuleWins()
    {
        Assert.AreEqual(
            ColorName.CodeVariable,
            ColorOf("source.json", "string.json", "support.type.property-name.json")
        );
        Assert.AreEqual(ColorName.CodeType, ColorOf("source.cs", "support.type.cs"));
    }
}
