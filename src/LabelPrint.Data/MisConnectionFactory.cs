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
            return "MIS SQL connection is not configured. Set ConnectionStrings:Mis with user secrets, appsettings.Local.json beside the exe, or the ConnectionStrings__Mis environment variable.";
        }

        foreach (var placeholder in Placeholders)
        {
            if (value.Contains(placeholder, StringComparison.OrdinalIgnoreCase))
            {
                return "The MIS connection string still contains a placeholder. Replace it with the desk credentials. Do not commit the password.";
            }
        }

        return null;
    }
}
