using Oracle.ManagedDataAccess.Client;

using Org.OpenAPITools.Models;

namespace Backend.Services;

public class UserTestService(string connString) : IUserTestService
{
    public List<UserData> GetLatestUsers()
    {
        var rows = new List<UserData>();

        using var conn = new OracleConnection(connString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT ID, NAME, CREATED_AT
                            FROM USER_TEST
                            ORDER BY ID DESC
                            FETCH FIRST 20 ROWS ONLY";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new UserData
            {
                Id = reader.GetInt64(0),
                Name = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                CreatedAt = reader.IsDBNull(2) ? default : reader.GetDateTime(2)
            });
        }

        return rows;
    }
}
