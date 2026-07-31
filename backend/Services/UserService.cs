using Oracle.ManagedDataAccess.Client;

using Org.OpenAPITools.Models;

namespace Backend.Services;

/// <summary>系统管理 — 用户增删改查Service。</summary>
public class UserService(string connString)
{
    private const string SelectColumns = @"
        SELECT USER_ID, EMPLOYEE_NO, USER_NAME, PHONE, EMAIL, STATUS,
               CREATED_TIME, LAST_LOGIN_TIME, PWD_UPDATE_TIME
        FROM SYS_USER";

    private static User ReadUser(OracleDataReader reader)
    {
        var user = new User
        {
            UserId = Convert.ToInt32(reader.GetValue(0)),
            EmployeeNo = reader.GetString(1),
            UserName = reader.GetString(2),
            Phone = reader.IsDBNull(3) ? null! : reader.GetString(3),
            Status = User.StatusEnum.ValidEnum,
        };

        if (!reader.IsDBNull(4)) user.Email = reader.GetString(4);
        var status = reader.GetString(5);
        user.Status = status == "disabled" ? User.StatusEnum.DisabledEnum : User.StatusEnum.ValidEnum;

        if (!reader.IsDBNull(6)) user.CreatedTime = reader.GetDateTime(6);
        if (!reader.IsDBNull(7)) user.LastLoginTime = reader.GetDateTime(7);
        if (!reader.IsDBNull(8)) user.PwdUpdateTime = reader.GetDateTime(8);

        return user;
    }

    public (List<User> Records, int Total) List(
        int page, int pageSize, int? userId, string? employeeNo, string? userName, string? status)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();

        var conditions = new List<string>();
        var filters = new List<SqlFilter>();

        if (userId.HasValue)
        {
            conditions.Add("USER_ID = :userId");
            filters.Add(new SqlFilter("userId", userId.Value));
        }
        if (!string.IsNullOrWhiteSpace(employeeNo))
        {
            conditions.Add("EMPLOYEE_NO LIKE :employeeNo");
            filters.Add(new SqlFilter("employeeNo", $"%{employeeNo.Trim()}%"));
        }
        if (!string.IsNullOrWhiteSpace(userName))
        {
            conditions.Add("USER_NAME LIKE :userName");
            filters.Add(new SqlFilter("userName", $"%{userName.Trim()}%"));
        }
        if (!string.IsNullOrWhiteSpace(status))
        {
            conditions.Add("STATUS = :status");
            filters.Add(new SqlFilter("status", status.Trim()));
        }

        var where = conditions.Count > 0 ? $"WHERE {string.Join(" AND ", conditions)}" : "";

        // 总数（每次绑定创建新的 OracleParameter 实例，避免跨命令复用触发 ORA-50030）
        using var countCmd = conn.CreateCommand();
        countCmd.CommandText = $"SELECT COUNT(*) FROM SYS_USER {where}";
        OracleSql.AddFilters(countCmd, filters);
        var total = Convert.ToInt32(countCmd.ExecuteScalar()!);

        // 分页数据（long 计算 offset，防止超大 page 溢出为负值）
        var offset = (long)(page - 1) * pageSize;
        using var dataCmd = conn.CreateCommand();
        dataCmd.CommandText = $"{SelectColumns} {where} ORDER BY USER_ID OFFSET :offset ROWS FETCH NEXT :limit ROWS ONLY";
        dataCmd.Parameters.Add(new OracleParameter("offset", offset));
        dataCmd.Parameters.Add(new OracleParameter("limit", pageSize));
        OracleSql.AddFilters(dataCmd, filters);

        var records = new List<User>();
        using var reader = dataCmd.ExecuteReader();
        while (reader.Read()) records.Add(ReadUser(reader));

        return (records, total);
    }

    public User? Get(int userId)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"{SelectColumns} WHERE USER_ID = :userId";
        cmd.Parameters.Add(new OracleParameter("userId", userId));

        using var reader = cmd.ExecuteReader();
        return reader.Read() ? ReadUser(reader) : null;
    }

    public (User? User, string? ErrorMessage) Create(UserCreateRequest request)
    {
        // 契约未声明 maxLength，超长输入会触发 ORA-12899（值过大）；
        // 服务层统一拦截（列定义见 database/01_schema_forklift.sql）。
        // 注意：直接传模型属性（CheckMaxLength 内部判空），不要对属性使用 ?.Trim()，
        // 否则编译器会将属性标记为可能为 null，导致后续解引用触发 CS8602。
        var lengthError = InputGuard.CheckMaxLength(request.EmployeeNo, 20, "工号")
            ?? InputGuard.CheckMaxLength(request.Password, 128, "密码")
            ?? InputGuard.CheckMaxLength(request.UserName, 50, "姓名")
            ?? InputGuard.CheckMaxLength(request.Phone, 20, "电话")
            ?? InputGuard.CheckMaxLength(request.Email, 100, "邮箱");
        if (lengthError is not null)
        {
            return (null, lengthError);
        }

        using var conn = new OracleConnection(connString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"INSERT INTO SYS_USER
                            (EMPLOYEE_NO, PASSWORD_HASH, USER_NAME, PHONE, EMAIL, STATUS, PWD_UPDATE_TIME)
                            VALUES
                            (:employeeNo, :passwordHash, :userName, :phone, :email, :status, SYSTIMESTAMP)
                            RETURNING USER_ID INTO :userId";
        cmd.Parameters.Add(new OracleParameter("employeeNo", request.EmployeeNo.Trim()));
        cmd.Parameters.Add(new OracleParameter("passwordHash", request.Password));
        cmd.Parameters.Add(new OracleParameter("userName", request.UserName.Trim()));
        cmd.Parameters.Add(new OracleParameter("phone", request.Phone.Trim()));
        cmd.Parameters.Add(new OracleParameter("email", string.IsNullOrWhiteSpace(request.Email) ? (object)DBNull.Value : request.Email.Trim()));
        var status = request.Status == UserCreateRequest.StatusEnum.DisabledEnum ? "disabled" : "valid";
        cmd.Parameters.Add(new OracleParameter("status", status));
        var userIdOut = new OracleParameter("userId", OracleDbType.Int64) { Direction = System.Data.ParameterDirection.Output };
        cmd.Parameters.Add(userIdOut);

        try
        {
            cmd.ExecuteNonQuery();
            var userId = Convert.ToInt32(userIdOut.Value.ToString());
            var user = Get(userId);
            return (user, null);
        }
        catch (OracleException ex) when (ex.Number == 1)
        {
            return (null, "工号已存在");
        }
    }

    public (User? User, string? ErrorMessage) Update(UserUpdateRequest request)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();

        // 先检查用户存在
        var existing = Get(request.UserId);
        if (existing is null) return (null, "用户不存在");

        var sets = new List<string>();
        var parameters = new List<OracleParameter> { new("userId", request.UserId) };

        if (!string.IsNullOrWhiteSpace(request.EmployeeNo))
        {
            sets.Add("EMPLOYEE_NO = :employeeNo");
            parameters.Add(new OracleParameter("employeeNo", request.EmployeeNo.Trim()));
        }
        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            sets.Add("PASSWORD_HASH = :passwordHash");
            parameters.Add(new OracleParameter("passwordHash", request.Password));
            sets.Add("PWD_UPDATE_TIME = SYSTIMESTAMP");
        }
        if (!string.IsNullOrWhiteSpace(request.UserName))
        {
            sets.Add("USER_NAME = :userName");
            parameters.Add(new OracleParameter("userName", request.UserName.Trim()));
        }
        if (!string.IsNullOrWhiteSpace(request.Phone))
        {
            sets.Add("PHONE = :phone");
            parameters.Add(new OracleParameter("phone", request.Phone.Trim()));
        }
        if (request.Email is not null)
        {
            sets.Add("EMAIL = :email");
            parameters.Add(new OracleParameter("email", string.IsNullOrWhiteSpace(request.Email) ? (object)DBNull.Value : request.Email.Trim()));
        }

        // Status 是非可空枚举，未传入时 C# 默认值为 0（非 ValidEnum/DisabledEnum）。
        // 仅在枚举值为有效成员时才更新状态字段。
        if ((int)request.Status == 1 || (int)request.Status == 2)
        {
            var statusVal = request.Status == UserUpdateRequest.StatusEnum.DisabledEnum ? "disabled" : "valid";
            sets.Add("STATUS = :status");
            parameters.Add(new OracleParameter("status", statusVal));
        }

        using var cmd = conn.CreateCommand();

        if (sets.Count == 0) return (existing, null); // 无变更

        cmd.CommandText = $"UPDATE SYS_USER SET {string.Join(", ", sets)} WHERE USER_ID = :userId";
        foreach (var p in parameters) cmd.Parameters.Add(p);

        try
        {
            cmd.ExecuteNonQuery();
        }
        catch (OracleException ex) when (ex.Number == 1)
        {
            // ORA-00001：唯一约束冲突，工号已被其他用户使用
            return (null, "工号已存在");
        }

        var updated = Get(request.UserId);
        return (updated, null);
    }

    public (bool Ok, string? ErrorMessage) Delete(int userId)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM SYS_USER WHERE USER_ID = :userId";
        cmd.Parameters.Add(new OracleParameter("userId", userId));

        try
        {
            return (cmd.ExecuteNonQuery() > 0, null);
        }
        catch (OracleException ex) when (ex.Number == 2292)
        {
            // ORA-02292：子记录存在（角色关系、登录日志、操作日志、业务单据等），转换为业务错误
            return (false, "该用户存在关联数据（角色、日志或业务单据），无法删除；建议改为停用");
        }
    }
}
