using System.Data;
using System.Globalization;

namespace LabelPrint.Data;

internal static class DbValue
{
    public static string String(IDataRecord reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        if (reader.IsDBNull(ordinal))
            return "";

        return Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture)?.Trim() ?? "";
    }

    public static string DateOrString(IDataRecord reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        if (reader.IsDBNull(ordinal))
            return "";

        return reader.GetValue(ordinal) switch
        {
            DateTime date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateTimeOffset date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            var other => Convert.ToString(other, CultureInfo.InvariantCulture)?.Trim() ?? ""
        };
    }

    public static string? OptionalString(IDataRecord reader, string name)
    {
        if (!HasColumn(reader, name))
            return null;
        return String(reader, name);
    }

    private static bool HasColumn(IDataRecord reader, string name)
    {
        for (var i = 0; i < reader.FieldCount; i++)
        {
            if (string.Equals(reader.GetName(i), name, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
