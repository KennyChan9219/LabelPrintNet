using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace LabelPrint.Data;

public sealed class LeagerRepository : ILeagerRepository
{
    private readonly MisConnectionFactory _factory;
    private readonly IOptions<LabelPrintOptions> _options;

    public LeagerRepository(MisConnectionFactory factory, IOptions<LabelPrintOptions> options)
    {
        _factory = factory;
        _options = options;
    }

    public async Task<LeagerRecord?> FindByNumberAsync(string leagNo, CancellationToken cancellationToken = default)
    {
        var rows = await QueryAsync(LeagerQueries.FindByNumber(_options.Value.Leager, leagNo), cancellationToken)
            .ConfigureAwait(false);
        return Single(rows, $"會員編號 {leagNo.Trim()}");
    }

    public async Task<LeagerRecord?> FindByOldCardNumberAsync(string oldCardNo, CancellationToken cancellationToken = default)
    {
        var rows = await QueryAsync(LeagerQueries.FindByOldCardNumber(_options.Value.Leager, oldCardNo), cancellationToken)
            .ConfigureAwait(false);
        return Single(rows, $"卡號 {oldCardNo.Trim()}");
    }

    public async Task<IReadOnlyList<LeagerRecord>> SearchByNameAsync(
        string name,
        int maxRows = 25,
        CancellationToken cancellationToken = default)
    {
        return await QueryAsync(LeagerQueries.SearchByName(_options.Value.Leager, name, maxRows), cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> CountActiveByCardAsync(string cardNo, CancellationToken cancellationToken = default)
    {
        var scalar = await ScalarAsync(LeagerQueries.CountActiveByCard(_options.Value.Leager, cardNo), cancellationToken)
            .ConfigureAwait(false);
        return Convert.ToInt32(scalar, System.Globalization.CultureInfo.InvariantCulture);
    }

    public async Task<LeagerRecord?> FindLabelByCardAsync(string cardNo, CancellationToken cancellationToken = default)
    {
        var number = await ScalarAsync(LeagerQueries.FindNumberByCard(_options.Value.Leager, cardNo), cancellationToken)
            .ConfigureAwait(false);
        var leagNo = Convert.ToString(number, System.Globalization.CultureInfo.InvariantCulture)?.Trim() ?? "";
        if (leagNo.Length == 0)
            return null;

        var rows = await QueryLabelAsync(LeagerQueries.FindLabelByNumber(_options.Value.Leager, leagNo), cancellationToken)
            .ConfigureAwait(false);
        return rows.Count == 0 ? null : rows[0];
    }

    public async Task RequestCardTransferAsync(string cardNo, CancellationToken cancellationToken = default)
    {
        var timeout = Math.Clamp(_options.Value.CommandTimeoutSeconds, 1, 300);
        await using var connection = _factory.CreateMemberConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = Bind(connection, LeagerQueries.RequestTransfer(cardNo), timeout);
        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static LeagerRecord? Single(List<LeagerRecord> rows, string description)
    {
        if (rows.Count > 1)
            throw new InvalidOperationException($"多於一筆 leager 記錄符合{description}。");
        return rows.Count == 0 ? null : rows[0];
    }

    private async Task<List<LeagerRecord>> QueryAsync(ParameterizedQuery query, CancellationToken cancellationToken)
    {
        var timeout = Math.Clamp(_options.Value.CommandTimeoutSeconds, 1, 300);
        await using var connection = _factory.CreateMemberConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = Bind(connection, query, timeout);
        var rows = new List<LeagerRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new LeagerRecord
            {
                Number = DbValue.String(reader, "Number"),
                Name = DbValue.String(reader, "Name"),
                OldCardNumber = DbValue.String(reader, "OldCardNumber"),
                ValidDate = DbValue.DateOrString(reader, "ValidDate"),
                MemberCode = DbValue.OptionalString(reader, "MemberCode"),
                TempMember = DbValue.OptionalString(reader, "TempMember")
            });
        }

        return rows;
    }

    private async Task<List<LeagerRecord>> QueryLabelAsync(ParameterizedQuery query, CancellationToken cancellationToken)
    {
        var timeout = Math.Clamp(_options.Value.CommandTimeoutSeconds, 1, 300);
        await using var connection = _factory.CreateMemberConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = Bind(connection, query, timeout);
        var rows = new List<LeagerRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new LeagerRecord
            {
                Number = DbValue.String(reader, "Number"),
                Name = DbValue.String(reader, "Name"),
                OldCardNumber = DbValue.String(reader, "OldCardNumber"),
                ValidDate = DbValue.DateOrString(reader, "ValidDate")
            });
        }

        return rows;
    }

    private async Task<object?> ScalarAsync(ParameterizedQuery query, CancellationToken cancellationToken)
    {
        var timeout = Math.Clamp(_options.Value.CommandTimeoutSeconds, 1, 300);
        await using var connection = _factory.CreateMemberConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = Bind(connection, query, timeout);
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is DBNull ? null : value;
    }

    internal static SqlCommand Bind(SqlConnection connection, ParameterizedQuery query, int timeout)
    {
        var command = connection.CreateCommand();
        command.CommandText = query.CommandText;
        command.CommandTimeout = timeout;
        foreach (var parameter in query.Parameters)
        {
            var sqlParameter = command.Parameters.Add(parameter.Name, parameter.DbType, parameter.Size);
            sqlParameter.Value = parameter.Value ?? DBNull.Value;
        }

        return command;
    }
}
