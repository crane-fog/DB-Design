using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

using Microsoft.IdentityModel.Tokens;

using Oracle.ManagedDataAccess.Client;

namespace Backend.Services;

public sealed record AuthenticatedUser(long UserId, string EmployeeNo, string UserName);

public class AuthService(string connString, string jwtSecret)
{
    public AuthenticatedUser? Authenticate(string employeeNo, string inputPasswordHash)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT USER_ID, EMPLOYEE_NO, PASSWORD_HASH, USER_NAME, STATUS
                            FROM SYS_USER
                            WHERE EMPLOYEE_NO = :employeeNo";
        cmd.Parameters.Add(new OracleParameter("employeeNo", employeeNo));

        long userId;
        string storedEmployeeNo;
        string storedPasswordHash;
        string userName;
        string status;

        using (var reader = cmd.ExecuteReader())
        {
            if (!reader.Read())
            {
                return null;
            }

            userId = Convert.ToInt64(reader.GetValue(0));
            storedEmployeeNo = reader.GetString(1);
            storedPasswordHash = reader.GetString(2);
            userName = reader.GetString(3);
            status = reader.GetString(4);
        }

        if (!CanLogin(status) || !VerifyPasswordHash(inputPasswordHash, storedPasswordHash))
        {
            return null;
        }

        UpdateLastLoginTime(conn, userId);

        return new AuthenticatedUser(userId, storedEmployeeNo, userName);
    }

    public string? Register(
        string employeeNo,
        string passwordHash,
        string userName,
        string phone,
        string? email)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"INSERT INTO SYS_USER
                            (EMPLOYEE_NO, PASSWORD_HASH, USER_NAME, PHONE, EMAIL, STATUS, PWD_UPDATE_TIME)
                            VALUES
                            (:employeeNo, :passwordHash, :userName, :phone, :email, 'valid', SYSTIMESTAMP)";
        cmd.Parameters.Add(new OracleParameter("employeeNo", employeeNo));
        cmd.Parameters.Add(new OracleParameter("passwordHash", passwordHash));
        cmd.Parameters.Add(new OracleParameter("userName", userName));
        cmd.Parameters.Add(new OracleParameter("phone", phone));
        var emailValue = string.IsNullOrWhiteSpace(email) ? (object)DBNull.Value : email;
        cmd.Parameters.Add(new OracleParameter("email", emailValue));

        try
        {
            cmd.ExecuteNonQuery();
            return null;
        }
        catch (OracleException ex) when (ex.Number == 1)
        {
            return "账号已存在";
        }
    }

    public string CreateToken(string employeeNo, DateTimeOffset expiresAt)
    {
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            claims: [new Claim("employee_no", employeeNo)],
            expires: expiresAt.UtcDateTime,
            notBefore: DateTime.UtcNow,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static bool CanLogin(string status)
    {
        return !string.Equals(status, "disabled", StringComparison.OrdinalIgnoreCase)
            && status != "0";
    }

    private static bool VerifyPasswordHash(string inputPasswordHash, string storedPasswordHash)
    {
        return string.Equals(inputPasswordHash, storedPasswordHash, StringComparison.OrdinalIgnoreCase);
    }

    private static void UpdateLastLoginTime(OracleConnection conn, long userId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"UPDATE SYS_USER
                            SET LAST_LOGIN_TIME = SYSTIMESTAMP
                            WHERE USER_ID = :userId";
        cmd.Parameters.Add(new OracleParameter("userId", userId));
        cmd.ExecuteNonQuery();
    }
}
