namespace gmd.Cui.Common;

// The code color of a token by its TextMate scopes, e.g. 'source.cs', 'comment.line.double-slash.cs',
// as VS Code's Dark+ and Light+ themes color them, in gmd's names for the colors, which each palette
// gives its RGB. The innermost scope a rule is for decides, so a string in a comment is a comment,
// and a token no rule is for is drawn as the text around it, as most of a line is.
static class CodeScopes
{
    // A rule is for a scope and every scope below it: 'string' is for 'string.quoted.double.cs'.
    // A more specific rule goes before a less specific one it is below, since the first decides. A
    // rule of no color stops the scopes outside it from coloring the token: an operator is not a
    // keyword, nor code in a string's interpolation a string.
    static readonly (string Scope, ColorName? Color)[] Rules =
    [
        ("comment", ColorName.CodeComment),
        ("punctuation.definition.comment", ColorName.CodeComment),
        ("constant.character.escape", ColorName.CodeEscape),
        ("punctuation.definition.interpolation", ColorName.CodeKeyword),
        ("punctuation.section.embedded", ColorName.CodeKeyword),
        ("meta.interpolation", null),
        ("meta.embedded", null),
        ("meta.template.expression", null),
        ("string", ColorName.CodeString),
        ("constant.numeric", ColorName.CodeNumber),
        ("constant.language", ColorName.CodeKeyword),
        ("constant.character", ColorName.CodeKeyword),
        ("constant.regexp", ColorName.CodeString),
        ("variable.other.enummember", ColorName.CodeConstant),
        ("variable.other.constant", ColorName.CodeConstant),
        ("support.constant", ColorName.CodeConstant),
        ("variable.language", ColorName.CodeKeyword),
        ("keyword.control", ColorName.CodeControl),
        ("keyword.operator.new", ColorName.CodeKeyword),
        ("keyword.operator.expression", ColorName.CodeKeyword),
        ("keyword.operator.cast", ColorName.CodeKeyword),
        ("keyword.operator.sizeof", ColorName.CodeKeyword),
        ("keyword.operator.typeof", ColorName.CodeKeyword),
        ("keyword.operator.instanceof", ColorName.CodeKeyword),
        ("keyword.operator.logical.python", ColorName.CodeKeyword),
        ("keyword.operator.wordlike", ColorName.CodeKeyword),
        ("keyword.operator", null),
        ("keyword", ColorName.CodeKeyword),
        ("storage", ColorName.CodeKeyword),
        ("meta.preprocessor", ColorName.CodeKeyword),
        ("entity.name.function", ColorName.CodeFunction),
        ("support.function", ColorName.CodeFunction),
        ("support.type.property-name", ColorName.CodeVariable),
        ("entity.name.type", ColorName.CodeType),
        ("entity.name.class", ColorName.CodeType),
        ("entity.name.namespace", ColorName.CodeType),
        ("entity.other.inherited-class", ColorName.CodeType),
        ("support.type", ColorName.CodeType),
        ("support.class", ColorName.CodeType),
        ("entity.name.tag", ColorName.CodeTag),
        ("entity.other.attribute-name", ColorName.CodeAttribute),
        ("variable", ColorName.CodeVariable),
        ("entity.name.variable", ColorName.CodeVariable),
        ("support.variable", ColorName.CodeVariable),
        ("markup.heading", ColorName.CodeKeyword),
        ("markup.inline.raw", ColorName.CodeString),
        ("markup.fenced_code", ColorName.CodeString),
        ("markup.raw", ColorName.CodeString),
    ];

    // The color of a token by its scopes, outermost first as TextMate lists them, or null for a token
    // drawn as the text around it
    public static ColorName? ColorOf(IReadOnlyList<string> scopes)
    {
        for (var i = scopes.Count - 1; i >= 0; i--)
        {
            foreach (var (scope, color) in Rules)
            {
                if (IsWithin(scopes[i], scope))
                    return color;
            }
        }
        return null;
    }

    // Whether a scope is the rule's or below it, e.g. 'string.quoted' of 'string', not 'strings'
    static bool IsWithin(string scope, string rule) =>
        scope.StartsWith(rule, StringComparison.Ordinal) && (scope.Length == rule.Length || scope[rule.Length] == '.');
}
