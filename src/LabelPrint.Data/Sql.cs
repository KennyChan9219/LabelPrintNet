using System.Text;
using System.Text.RegularExpressions;

namespace LabelPrint.Data;

/// <summary>
/// Identifier quoting for names that come from configuration. Values always stay in parameters.
/// </summary>
public static partial class Sql
{
    public static string Bracket(string identifier, string role)
    {
        if (string.IsNullOrWhiteSpace(identifier) || !Identifier().IsMatch(identifier))
        {
            throw new InvalidOperationException(
                $"The configured SQL name for {role} must be a single letter-or-underscore identifier.");
        }

        return "[" + identifier + "]";
    }

    public static string BracketQualified(string name, string role)
    {
        var parts = name.Split('.');
        if (parts.Length is < 1 or > 2)
        {
            throw new InvalidOperationException(
                $"The configured SQL name for {role} must be one identifier, or schema.table.");
        }

        return string.Join(".", parts.Select(part => Bracket(part, role)));
    }

    public static string ContainsPattern(string text)
    {
        var builder = new StringBuilder(text.Length + 8);
        foreach (var character in text)
        {
            if (character is '\\' or '%' or '_' or '[')
                builder.Append('\\');
            builder.Append(character);
        }

        return "%" + builder + "%";
    }

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex Identifier();
}
