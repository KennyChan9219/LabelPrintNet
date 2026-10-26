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
        return Single(rows, $"member number {leagNo.Trim()}");
    }

    public async Task<LeagerRecord?> FindByOldCardNumberAsync(string oldCardNo, CancellationToken cancellationToken = default)
    {
        var rows = await QueryAsync(LeagerQueries.FindByOldCardNumber(_options.Value.Leager, oldCardNo), cancellationToken)
            .ConfigureAwait(false);
        return Single(rows, $"card number {oldCardNo.Trim()}");
    }

    public async Task<IReadOnlyList<LeagerRecord>> SearchByNameAsync(
        string name,
        int maxRows = 25,
        CancellationToken cancellationToken = default)
    {
        return await QueryAsync(LeagerQueries.SearchByName(_options.Value.Leager, name, maxRows), cancellationToken)
            .ConfigureAwait(false);
    }

    private static LeagerRecord? Single(List<LeagerRecord> rows, string description)
    {
        if (rows.Count > 1)
            throw new InvalidOperationException($"More than one leager row matches {description}.");
        return rows.Count == 0 ? null : rows[0];
    }

    private async Task<List<LeagerRecord>> QueryAsync(ParameterizedQuery query, CancellationToken cancellationToken)
    {
        var timeout = Math.Clamp(_options.Value.CommandTimeoutSeconds, 1, 300);
        await using var connection = _factory.CreateConnection();
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
