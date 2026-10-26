using Microsoft.Data.SqlClient;

namespace LabelPrint.Data;

public static class LeagerQueries
{
    public static ParameterizedQuery FindByNumber(LeagerTableOptions options, string leagNo)
    {
        var key = Require(leagNo, 64, "會員編號");
        var sql = $"""
            SELECT TOP (2) {SelectList(options)}
            FROM {Sql.BracketQualified(options.Table, "leager 資料表")}
            WHERE {Sql.Bracket(options.NumberColumn, "leager 編號")} = @leagNo
            """;
        return new ParameterizedQuery(sql, [Text("@leagNo", 64, key)]);
    }

    public static ParameterizedQuery FindByOldCardNumber(LeagerTableOptions options, string oldCardNo)
    {
        var key = Require(oldCardNo, 64, "卡號");
        var sql = $"""
            SELECT TOP (2) {SelectList(options)}
            FROM {Sql.BracketQualified(options.Table, "leager 資料表")}
            WHERE {Sql.Bracket(options.OldCardNumberColumn, "舊卡號")} = @oldCardNo
            """;
        return new ParameterizedQuery(sql, [Text("@oldCardNo", 64, key)]);
    }

    public static ParameterizedQuery SearchByName(LeagerTableOptions options, string name, int maxRows)
    {
        var key = Require(name, 100, "會員姓名");
        var take = Math.Clamp(maxRows, 1, 100);
        var sql = $"""
            SELECT TOP (@maxRows) {SelectList(options)}
            FROM {Sql.BracketQualified(options.Table, "leager 資料表")}
            WHERE {Sql.Bracket(options.NameColumn, "leager 姓名")} LIKE @name ESCAPE '\'
            ORDER BY {Sql.Bracket(options.NumberColumn, "leager 編號")}
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
            Column(options.NumberColumn, "Number", "leager 編號"),
            Column(options.NameColumn, "Name", "leager 姓名"),
            Column(options.OldCardNumberColumn, "OldCardNumber", "舊卡號"),
            Column(options.ValidDateColumn, "ValidDate", "有效日期")
        };
        AddOptional(parts, options.MemberCodeColumn, "MemberCode", "會員代碼");
        AddOptional(parts, options.TempMemberColumn, "TempMember", "臨時會員");
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
            throw new ArgumentException($"{label}必須填寫。", nameof(value));
        if (trimmed.Length > maxLength)
            throw new ArgumentException($"{label}太長。", nameof(value));
        return trimmed;
    }
}
