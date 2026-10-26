using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace LabelPrint.Data;

public sealed class MisConnectionFactory
{
    private static readonly string[] Placeholders = ["YOUR_PASSWORD", "YOUR_SQL_HOST", "YOUR_USER", "YOUR_SECRET"];

    private readonly IOptions<MisConnectionOptions> _options;

    public MisConnectionFactory(IOptions<MisConnectionOptions> options)
    {
        _options = options;
    }

    public bool IsConfigured => TryExplain() is null;

    public SqlConnection CreateConnection()
    {
        var problem = TryExplain();
        if (problem is not null)
            throw new MisNotConfiguredException(problem);

        return new SqlConnection(_options.Value.Mis.Trim());
    }

    public async Task CanConnectAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1";
        command.CommandTimeout = 15;
        _ = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
    }

    private string? TryExplain()
    {
        var value = _options.Value.Mis?.Trim() ?? "";
        if (value.Length == 0)
        {
            return "尚未設定 MIS SQL 連線。請用 user secrets、執行檔旁邊的 appsettings.Local.json，或環境變數 ConnectionStrings__Mis 設定 ConnectionStrings:Mis。";
        }

        foreach (var placeholder in Placeholders)
        {
            if (value.Contains(placeholder, StringComparison.OrdinalIgnoreCase))
            {
                return "MIS 連線字串仍然包含佔位符。請改為櫃檯的實際連線資料，不要把密碼提交到版本庫。";
            }
        }

        return null;
    }
}
