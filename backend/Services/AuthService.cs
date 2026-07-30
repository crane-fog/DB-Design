using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

using Microsoft.IdentityModel.Tokens;

using Oracle.ManagedDataAccess.Client;

namespace Backend.Services;

public sealed record AuthenticatedUser(long UserId, string EmployeeNo, string UserName, string RawStatus);
public sealed record RegisteredUser(
    long UserId,
    string EmployeeNo,
    string UserName,
    string Phone,
    string? Email,
    string Status);

public sealed record RegisterResult(RegisteredUser? User, string? ErrorMessage);

public class AuthService(string connString, string jwtSecret, LoginLogService loginLogService)
{
    public AuthenticatedUser? Authenticate(string employeeNo, string inputPasswordHash, string ipAddress)
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
                // 工号不存在，记录失败日志
                loginLogService.Write(0, ipAddress, false, "工号不存在");
                return null;
            }

            userId = Convert.ToInt64(reader.GetValue(0));
            storedEmployeeNo = reader.GetString(1);
            storedPasswordHash = reader.GetString(2);
            userName = reader.GetString(3);
            status = reader.GetString(4);
        }

        if (!CanLogin(status))
        {
            loginLogService.Write(userId, ipAddress, false, "账号已停用");
            return null;
        }

        if (!VerifyPasswordHash(inputPasswordHash, storedPasswordHash))
        {
            loginLogService.Write(userId, ipAddress, false, "密码错误");
            return null;
        }

        UpdateLastLoginTime(conn, userId);
        loginLogService.Write(userId, ipAddress, true, null);

        return new AuthenticatedUser(userId, storedEmployeeNo, userName, status);
    }

    public RegisterResult Register(
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
                            (:employeeNo, :passwordHash, :userName, :phone, :email, 'valid', SYSTIMESTAMP)
                            RETURNING USER_ID INTO :userId";
        cmd.Parameters.Add(new OracleParameter("employeeNo", employeeNo));
        cmd.Parameters.Add(new OracleParameter("passwordHash", passwordHash));
        cmd.Parameters.Add(new OracleParameter("userName", userName));
        cmd.Parameters.Add(new OracleParameter("phone", phone));
        var emailValue = string.IsNullOrWhiteSpace(email) ? (object)DBNull.Value : email;
        cmd.Parameters.Add(new OracleParameter("email", emailValue));
        var userIdParameter = new OracleParameter("userId", OracleDbType.Int64)
        {
            Direction = System.Data.ParameterDirection.Output
        };
        cmd.Parameters.Add(userIdParameter);

        try
        {
            cmd.ExecuteNonQuery();
            var userId = long.Parse(userIdParameter.Value.ToString()!, CultureInfo.InvariantCulture);
            var user = new RegisteredUser(userId, employeeNo, userName, phone, email, "valid");
            return new RegisterResult(user, null);
        }
        catch (OracleException ex) when (ex.Number == 1)
        {
            return new RegisterResult(null, "工号已存在");
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
