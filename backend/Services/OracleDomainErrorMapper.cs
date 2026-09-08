using Oracle.ManagedDataAccess.Client;

namespace Backend.Services;

/// <summary>Maps stable application errors raised by the Oracle domain packages.</summary>
internal static class OracleDomainErrorMapper
{
    public static bool TryMap(OracleException exception, out int statusCode, out string message)
    {
        statusCode = exception.Number switch
        {
            20001 => 400,
            20101 => 404,
            >= 20201 and <= 20205 => 409,
            _ => 500,
        };

        if (statusCode == 500)
        {
            message = "";
            return false;
        }

        message = ExtractMessage(exception.Message);
        return true;
    }

    private static string ExtractMessage(string databaseMessage)
    {
        string firstLine = databaseMessage.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault() ?? databaseMessage;
        int separator = firstLine.IndexOf(':');
        return separator >= 0 ? firstLine[(separator + 1)..].Trim() : firstLine.Trim();
    }
}
