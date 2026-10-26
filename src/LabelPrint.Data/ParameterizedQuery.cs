using Microsoft.Data.SqlClient;

namespace LabelPrint.Data;

public sealed record QueryParameter(string Name, SqlDbType DbType, int Size, object? Value);

public sealed record ParameterizedQuery(string CommandText, IReadOnlyList<QueryParameter> Parameters);
