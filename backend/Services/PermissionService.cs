using Oracle.ManagedDataAccess.Client;

using Org.OpenAPITools.Models;

namespace Backend.Services;

/// <summary>系统管理 — 权限 CRUD Service。</summary>
public class PermissionService(string connString)
{
    private const string SelectColumns = @"
        SELECT PERMISSION_ID, ""resource"", ACTION
        FROM SYS_PERMISSION";

    private static PermissionBrief ReadPermission(OracleDataReader reader)
    {
        return new PermissionBrief
        {
            PermissionId = Convert.ToInt32(reader.GetValue(0)),
            Resource = reader.GetString(1),
            Action = reader.GetString(2),
        };
    }

    public (List<PermissionBrief> Records, int Total) List(
        int page, int pageSize, int? permissionId, string? resource, string? action)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();

        var conditions = new List<string>();
        var parameters = new List<OracleParameter>();

        if (permissionId.HasValue)
        {
            conditions.Add("PERMISSION_ID = :permissionId");
            parameters.Add(new OracleParameter("permissionId", permissionId.Value));
        }
        if (!string.IsNullOrWhiteSpace(resource))
        {
            conditions.Add("\"resource\" LIKE :resource");
            parameters.Add(new OracleParameter("resource", $"%{resource.Trim()}%"));
        }
        if (!string.IsNullOrWhiteSpace(action))
        {
            conditions.Add("ACTION LIKE :action");
            parameters.Add(new OracleParameter("action", $"%{action.Trim()}%"));
        }

        var where = conditions.Count > 0 ? $"WHERE {string.Join(" AND ", conditions)}" : "";

        using var countCmd = conn.CreateCommand();
        countCmd.CommandText = $"SELECT COUNT(*) FROM SYS_PERMISSION {where}";
        foreach (var p in parameters) countCmd.Parameters.Add(p);
        var total = Convert.ToInt32(countCmd.ExecuteScalar()!);

        var offset = (page - 1) * pageSize;
        using var dataCmd = conn.CreateCommand();
        dataCmd.CommandText = $"{SelectColumns} {where} ORDER BY PERMISSION_ID OFFSET :offset ROWS FETCH NEXT :limit ROWS ONLY";
        dataCmd.Parameters.Add(new OracleParameter("offset", offset));
        dataCmd.Parameters.Add(new OracleParameter("limit", pageSize));
        foreach (var p in parameters) dataCmd.Parameters.Add(p);

        var records = new List<PermissionBrief>();
        using var reader = dataCmd.ExecuteReader();
        while (reader.Read()) records.Add(ReadPermission(reader));

        return (records, total);
    }

    public PermissionBrief? Get(int permissionId)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"{SelectColumns} WHERE PERMISSION_ID = :permissionId";
        cmd.Parameters.Add(new OracleParameter("permissionId", permissionId));

        using var reader = cmd.ExecuteReader();
        return reader.Read() ? ReadPermission(reader) : null;
    }

    public (PermissionBrief? Permission, string? ErrorMessage) Create(PermissionCreateRequest request)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"INSERT INTO SYS_PERMISSION (""resource"", ACTION)
                            VALUES (:resource, :action)
                            RETURNING PERMISSION_ID INTO :permissionId";
        cmd.Parameters.Add(new OracleParameter("resource", request.Resource.Trim()));
        cmd.Parameters.Add(new OracleParameter("action", request.Action.Trim()));
        var permissionIdOut = new OracleParameter("permissionId", OracleDbType.Int64) { Direction = System.Data.ParameterDirection.Output };
        cmd.Parameters.Add(permissionIdOut);

        try
        {
            cmd.ExecuteNonQuery();
            var id = Convert.ToInt32(permissionIdOut.Value.ToString());
            return (Get(id), null);
        }
        catch (OracleException ex) when (ex.Number == 1)
        {
            return (null, "权限已存在（资源+操作重复）");
        }
    }

    public (PermissionBrief? Permission, string? ErrorMessage) Update(PermissionUpdateRequest request)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();

        var existing = Get(request.PermissionId);
        if (existing is null) return (null, "权限不存在");

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"UPDATE SYS_PERMISSION
                            SET ""resource"" = :resource, ACTION = :action
                            WHERE PERMISSION_ID = :permissionId";
        cmd.Parameters.Add(new OracleParameter("resource", request.Resource.Trim()));
        cmd.Parameters.Add(new OracleParameter("action", request.Action.Trim()));
        cmd.Parameters.Add(new OracleParameter("permissionId", request.PermissionId));

        try
        {
            cmd.ExecuteNonQuery();
            return (Get(request.PermissionId), null);
        }
        catch (OracleException ex) when (ex.Number == 1)
        {
            return (null, "权限已存在（资源+操作重复）");
        }
    }

    public bool Delete(int permissionId)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM SYS_PERMISSION WHERE PERMISSION_ID = :permissionId";
        cmd.Parameters.Add(new OracleParameter("permissionId", permissionId));

        return cmd.ExecuteNonQuery() > 0;
    }
}
