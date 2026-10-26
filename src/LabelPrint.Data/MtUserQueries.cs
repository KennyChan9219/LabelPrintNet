using Microsoft.Data.SqlClient;

namespace LabelPrint.Data;

public static class MtUserQueries
{
    public static ParameterizedQuery FindByStaffNumber(MtUserTableOptions options, string staffNo)
    {
        var key = Require(staffNo, 64, "Staff number");
        var sql = $"""
            SELECT TOP (2) {SelectList(options)}
            FROM {Sql.BracketQualified(options.Table, "MT_USER table")}
            WHERE {Sql.Bracket(options.StaffNumberColumn, "staff number")} = @staffNo
            """;
        return new ParameterizedQuery(sql, [Text("@staffNo", 64, key)]);
    }

    public static ParameterizedQuery SearchByName(MtUserTableOptions options, string name, int maxRows)
    {
        var key = Require(name, 100, "Employee name");
        var take = Math.Clamp(maxRows, 1, 100);
        var sql = $"""
            SELECT TOP (@maxRows) {SelectList(options)}
            FROM {Sql.BracketQualified(options.Table, "MT_USER table")}
            WHERE {Sql.Bracket(options.EnglishNameColumn, "English name")} LIKE @name ESCAPE '\'
               OR {Sql.Bracket(options.ChineseNameColumn, "Chinese name")} LIKE @name ESCAPE '\'
            ORDER BY {Sql.Bracket(options.StaffNumberColumn, "staff number")}
            """;
        return new ParameterizedQuery(sql,
        [
            new QueryParameter("@maxRows", SqlDbType.Int, 0, take),
            Text("@name", 210, Sql.ContainsPattern(key))
        ]);
    }

    private static string SelectList(MtUserTableOptions options) => string.Join(", ",
    [
        Column(options.StaffNumberColumn, "StaffNumber", "staff number"),
        Column(options.EnglishNameColumn, "EnglishName", "English name"),
        Column(options.ChineseNameColumn, "ChineseName", "Chinese name"),
        Column(options.IdColumn, "Id", "staff id")
    ]);

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
