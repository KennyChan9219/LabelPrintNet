using Microsoft.Data.SqlClient;

namespace LabelPrint.Data;

public static class LeagerQueries
{
    public static ParameterizedQuery FindByNumber(LeagerTableOptions options, string leagNo)
    {
        var key = Require(leagNo, 64, "Member number");
        var sql = $"""
            SELECT TOP (2) {SelectList(options)}
            FROM {Sql.BracketQualified(options.Table, "leager table")}
            WHERE {Sql.Bracket(options.NumberColumn, "leager number")} = @leagNo
            """;
        return new ParameterizedQuery(sql, [Text("@leagNo", 64, key)]);
    }

    public static ParameterizedQuery FindByOldCardNumber(LeagerTableOptions options, string oldCardNo)
    {
        var key = Require(oldCardNo, 64, "Card number");
        var sql = $"""
            SELECT TOP (2) {SelectList(options)}
            FROM {Sql.BracketQualified(options.Table, "leager table")}
            WHERE {Sql.Bracket(options.OldCardNumberColumn, "old card number")} = @oldCardNo
            """;
        return new ParameterizedQuery(sql, [Text("@oldCardNo", 64, key)]);
    }

    public static ParameterizedQuery SearchByName(LeagerTableOptions options, string name, int maxRows)
    {
        var key = Require(name, 100, "Member name");
        var take = Math.Clamp(maxRows, 1, 100);
        var sql = $"""
            SELECT TOP (@maxRows) {SelectList(options)}
            FROM {Sql.BracketQualified(options.Table, "leager table")}
            WHERE {Sql.Bracket(options.NameColumn, "leager name")} LIKE @name ESCAPE '\'
            ORDER BY {Sql.Bracket(options.NumberColumn, "leager number")}
            """;
        return new ParameterizedQuery(sql,
        [
            new QueryParameter("@maxRows", SqlDbType.Int, 0, take),
            Text("@name", 210, Sql.ContainsPattern(key))
        ]);
    }

    private static string SelectList(LeagerTableOptions options)
    {
        var parts = new List<string>
        {
            Column(options.NumberColumn, "Number", "leager number"),
            Column(options.NameColumn, "Name", "leager name"),
            Column(options.OldCardNumberColumn, "OldCardNumber", "old card number"),
            Column(options.ValidDateColumn, "ValidDate", "valid date")
        };
        AddOptional(parts, options.MemberCodeColumn, "MemberCode", "member code");
        AddOptional(parts, options.TempMemberColumn, "TempMember", "temp member");
        return string.Join(", ", parts);
    }

    private static void AddOptional(List<string> parts, string column, string alias, string role)
    {
        if (!string.IsNullOrWhiteSpace(column))
            parts.Add(Column(column, alias, role));
    }

    private static string Column(string name, string alias, string role) =>
        $"{Sql.Bracket(name, role)} AS [{alias}]";

    private static QueryParameter Text(string name, int size, string value) =>
        new(name, SqlDbType.NVarChar, size, value);

    private static string Require(string value, int maxLength, string label)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0)
            throw new ArgumentException($"{label} is required.", nameof(value));
        if (trimmed.Length > maxLength)
            throw new ArgumentException($"{label} is too long.", nameof(value));
        return trimmed;
    }
}
