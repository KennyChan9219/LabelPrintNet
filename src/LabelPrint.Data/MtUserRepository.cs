using Microsoft.Extensions.Options;

namespace LabelPrint.Data;

public sealed class MtUserRepository : IMtUserRepository
{
    private readonly MisConnectionFactory _factory;
    private readonly IOptions<LabelPrintOptions> _options;

    public MtUserRepository(MisConnectionFactory factory, IOptions<LabelPrintOptions> options)
    {
        _factory = factory;
        _options = options;
    }

    public async Task<EmployeeRecord?> FindByStaffNumberAsync(string staffNo, CancellationToken cancellationToken = default)
    {
        var rows = await QueryAsync(MtUserQueries.FindByStaffNumber(_options.Value.MtUser, staffNo), cancellationToken)
            .ConfigureAwait(false);
        if (rows.Count > 1)
            throw new InvalidOperationException($"多於一筆 MT_USER 記錄符合員工編號 {staffNo.Trim()}。");
        return rows.Count == 0 ? null : rows[0];
    }

    public async Task<IReadOnlyList<EmployeeRecord>> SearchByNameAsync(
        string name,
        int maxRows = 25,
        CancellationToken cancellationToken = default)
    {
        return await QueryAsync(MtUserQueries.SearchByName(_options.Value.MtUser, name, maxRows), cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<EmployeeRecord?> FindByCardIdAsync(string cardId, CancellationToken cancellationToken = default)
    {
        var rows = await QueryAsync(MtUserQueries.FindByCardId(_options.Value.MtUser, cardId), cancellationToken)
            .ConfigureAwait(false);
        return rows.Count == 0 ? null : rows[0];
    }

    private async Task<List<EmployeeRecord>> QueryAsync(ParameterizedQuery query, CancellationToken cancellationToken)
    {
        var timeout = Math.Clamp(_options.Value.CommandTimeoutSeconds, 1, 300);
        await using var connection = _factory.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = LeagerRepository.Bind(connection, query, timeout);
        var rows = new List<EmployeeRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new EmployeeRecord
            {
                StaffNumber = DbValue.String(reader, "StaffNumber"),
                EnglishName = DbValue.String(reader, "EnglishName"),
                ChineseName = DbValue.String(reader, "ChineseName"),
                Id = DbValue.String(reader, "Id")
            });
        }

        return rows;
    }
}
