using Microsoft.Data.SqlClient;

namespace LabelPrint.Data;

public static class MtUserQueries
{
    public static ParameterizedQuery FindByStaffNumber(MtUserTableOptions options, string staffNo)
    {
        var key = Require(staffNo, 64, "員工編號");
        var sql = $"""
            SELECT TOP (2) {SelectList(options)}
            FROM {Sql.BracketQualified(options.Table, "MT_USER 資料表")}
            WHERE {Sql.Bracket(options.StaffNumberColumn, "員工編號")} = @staffNo
            """;
        return new ParameterizedQuery(sql, [Text("@staffNo", 64, key)]);
    }

    public static ParameterizedQuery SearchByName(MtUserTableOptions options, string name, int maxRows)
    {
        var key = Require(name, 100, "員工姓名");
        var take = Math.Clamp(maxRows, 1, 100);
        var sql = $"""
            SELECT TOP (@maxRows) {SelectList(options)}
            FROM {Sql.BracketQualified(options.Table, "MT_USER 資料表")}
            WHERE {Sql.Bracket(options.EnglishNameColumn, "英文姓名")} LIKE @name ESCAPE '\'
               OR {Sql.Bracket(options.ChineseNameColumn, "中文姓名")} LIKE @name ESCAPE '\'
            ORDER BY {Sql.Bracket(options.StaffNumberColumn, "員工編號")}
            """;
        return new ParameterizedQuery(sql,
        [
            new QueryParameter("@maxRows", SqlDbType.Int, 0, take),
            Text("@name", 210, Sql.ContainsPattern(key))
        ]);
    }

    private static string SelectList(MtUserTableOptions options) => string.Join(", ",
    [
        Column(options.StaffNumberColumn, "StaffNumber", "員工編號"),
        Column(options.EnglishNameColumn, "EnglishName", "英文姓名"),
        Column(options.ChineseNameColumn, "ChineseName", "中文姓名"),
        Column(options.IdColumn, "Id", "員工識別")
    ]);

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
