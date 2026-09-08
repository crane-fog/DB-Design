using Oracle.ManagedDataAccess.Client;

using Org.OpenAPITools.Models;

namespace Backend.Services;

public enum BomBusinessError
{
    BadRequest = 400,
    NotFound = 404,
    Conflict = 409,
}

public sealed record BomBusinessResult<T>(
    bool Ok,
    T? Data,
    BomBusinessError Error,
    string? ErrorMessage)
{
    public static BomBusinessResult<T> Success(T data) => new(true, data, 0, null);

    public static BomBusinessResult<T> Fail(BomBusinessError error, string message) => new(false, default, error, message);
}

public class BomVersionService(string connString)
{
    public (List<BomVersion> Records, int Total) ListVersions(
        int page,
        int pageSize,
        long? materialId,
        string? versionNo,
        bool? effectiveOnly)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();

        var where = BuildWhere(materialId, versionNo, effectiveOnly);
        var whereClause = where.Count > 0 ? " WHERE " + string.Join(" AND ", where) : string.Empty;

        void AddFilters(OracleCommand cmd)
        {
            if (materialId.HasValue)
            {
                cmd.Parameters.Add(new OracleParameter("materialId", materialId.Value));
            }

            if (!string.IsNullOrWhiteSpace(versionNo))
            {
                cmd.Parameters.Add(new OracleParameter("versionNo", $"%{versionNo.Trim()}%"));
            }
        }

        int total;
        using (var countCmd = conn.CreateCommand())
        {
            countCmd.CommandText = "SELECT COUNT(*) FROM BOM_VERSION bv" + whereClause;
            AddFilters(countCmd);
            total = Convert.ToInt32(countCmd.ExecuteScalar());
        }

        var records = new List<BomVersion>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = SelectColumns + whereClause +
                @" ORDER BY bv.MATERIAL_ID, bv.EFFECTIVE_DATE DESC, bv.VERSION_ID
                   OFFSET :skip ROWS FETCH NEXT :take ROWS ONLY";
            AddFilters(cmd);
            cmd.Parameters.Add(new OracleParameter("skip", (page - 1) * pageSize));
            cmd.Parameters.Add(new OracleParameter("take", pageSize));

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                records.Add(MapVersion(reader));
            }
        }

        return (records, total);
    }

    public BomVersion? GetVersion(long versionId)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();
        return GetVersionInternal(conn, versionId);
    }

    public BomBusinessResult<BomVersion> AddVersion(BomVersionCreateRequest request, long createdBy)
    {
        var validation = ValidateVersionInput(request.MaterialId, request.VersionNo, request.EffectiveDate, request.ExpireDate);
        if (validation is not null)
        {
            return BomBusinessResult<BomVersion>.Fail(BomBusinessError.BadRequest, validation);
        }

        using var conn = new OracleConnection(connString);
        conn.Open();
        using var transaction = conn.BeginTransaction();

        try
        {
            if (!MaterialExists(conn, transaction, request.MaterialId))
            {
                transaction.Rollback();
                return BomBusinessResult<BomVersion>.Fail(BomBusinessError.BadRequest, "物料不存在");
            }

            if (VersionNoExists(conn, transaction, request.MaterialId, request.VersionNo.Trim(), null))
            {
                transaction.Rollback();
                return BomBusinessResult<BomVersion>.Fail(BomBusinessError.Conflict, "同一物料下 BOM 版本号已存在");
            }

            long newId;
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = @"INSERT INTO BOM_VERSION
                                    (MATERIAL_ID, VERSION_NO, EFFECTIVE_DATE, EXPIRE_DATE, CHANGE_REASON, CREATED_BY)
                                    VALUES
                                    (:materialId, :versionNo, :effectiveDate, :expireDate, :changeReason, :createdBy)
                                    RETURNING VERSION_ID INTO :newId";
                AddWriteParameters(cmd, request.MaterialId, request.VersionNo.Trim(), request.EffectiveDate, request.ExpireDate, request.ChangeReason);
                cmd.Parameters.Add(new OracleParameter("createdBy", createdBy));
                var idParam = new OracleParameter("newId", OracleDbType.Int64)
                {
                    Direction = System.Data.ParameterDirection.Output,
                };
                cmd.Parameters.Add(idParam);
                cmd.ExecuteNonQuery();
                newId = Convert.ToInt64(idParam.Value.ToString());
            }

            // 版本创建与发布分离：BOM 明细完成后，由物料 current_version_id 显式发布。
            transaction.Commit();
            return BomBusinessResult<BomVersion>.Success(GetVersionInternal(conn, newId)!);
        }
        catch (OracleException ex) when (ex.Number == 1 || ex.Number == 2291 || ex.Number == 2292)
        {
            transaction.Rollback();
            return BomBusinessResult<BomVersion>.Fail(BomBusinessError.Conflict, "BOM 版本关联数据冲突");
        }
    }

    public BomBusinessResult<BomVersion> UpdateVersion(BomVersionUpdateRequest request)
    {
        var validation = ValidateVersionInput(request.MaterialId, request.VersionNo, request.EffectiveDate, request.ExpireDate);
        if (request.VersionId <= 0)
        {
            validation = "版本编号不能为空";
        }

        if (validation is not null)
        {
            return BomBusinessResult<BomVersion>.Fail(BomBusinessError.BadRequest, validation);
        }

        using var conn = new OracleConnection(connString);
        conn.Open();
        using var transaction = conn.BeginTransaction();

        try
        {
            var existing = GetVersionInternal(conn, request.VersionId, transaction);
            if (existing is null)
            {
                transaction.Rollback();
                return BomBusinessResult<BomVersion>.Fail(BomBusinessError.NotFound, "BOM 版本不存在");
            }

            if (!MaterialExists(conn, transaction, request.MaterialId))
            {
                transaction.Rollback();
                return BomBusinessResult<BomVersion>.Fail(BomBusinessError.BadRequest, "物料不存在");
            }

            if (existing.MaterialId != request.MaterialId)
            {
                transaction.Rollback();
                return BomBusinessResult<BomVersion>.Fail(BomBusinessError.Conflict, "BOM 版本所属物料不能修改");
            }

            if (IsCurrentVersion(conn, transaction, request.VersionId)
                && !IsEffectiveOnDatabaseDate(conn, transaction, request.EffectiveDate, request.ExpireDate))
            {
                transaction.Rollback();
                return BomBusinessResult<BomVersion>.Fail(BomBusinessError.Conflict, "当前发布版本必须处于有效期内，请先取消发布");
            }

            if (VersionNoExists(conn, transaction, request.MaterialId, request.VersionNo.Trim(), request.VersionId))
            {
                transaction.Rollback();
                return BomBusinessResult<BomVersion>.Fail(BomBusinessError.Conflict, "同一物料下 BOM 版本号已存在");
            }

            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = @"UPDATE BOM_VERSION
                                    SET MATERIAL_ID = :materialId,
                                        VERSION_NO = :versionNo,
                                        EFFECTIVE_DATE = :effectiveDate,
                                        EXPIRE_DATE = :expireDate,
                                        CHANGE_REASON = :changeReason
                                    WHERE VERSION_ID = :versionId";
                AddWriteParameters(cmd, request.MaterialId, request.VersionNo.Trim(), request.EffectiveDate, request.ExpireDate, request.ChangeReason);
                cmd.Parameters.Add(new OracleParameter("versionId", request.VersionId));
                cmd.ExecuteNonQuery();
            }

            transaction.Commit();
            return BomBusinessResult<BomVersion>.Success(GetVersionInternal(conn, request.VersionId)!);
        }
        catch (OracleException ex) when (ex.Number == 1 || ex.Number == 2291 || ex.Number == 2292)
        {
            transaction.Rollback();
            return BomBusinessResult<BomVersion>.Fail(BomBusinessError.Conflict, "BOM 版本关联数据冲突");
        }
    }

    public BomBusinessResult<object> DeleteVersion(long versionId)
    {
        if (versionId <= 0)
        {
            return BomBusinessResult<object>.Fail(BomBusinessError.BadRequest, "版本编号不能为空");
        }

        using var conn = new OracleConnection(connString);
        conn.Open();

        if (GetVersionInternal(conn, versionId) is null)
        {
            return BomBusinessResult<object>.Fail(BomBusinessError.NotFound, "BOM 版本不存在");
        }

        if (ExistsBySql(conn, "SELECT COUNT(*) FROM MATERIAL WHERE CURRENT_VERSION_ID = :id", versionId))
        {
            return BomBusinessResult<object>.Fail(BomBusinessError.Conflict, "当前 BOM 版本不能删除");
        }

        if (ExistsBySql(conn, "SELECT COUNT(*) FROM BOM WHERE VERSION_ID = :id", versionId)
            || ExistsBySql(conn, "SELECT COUNT(*) FROM PRODUCTION_ORDER WHERE VERSION_ID = :id", versionId)
            || ExistsBySql(conn, "SELECT COUNT(*) FROM FINISH_INBOUND WHERE VERSION_ID = :id", versionId))
        {
            return BomBusinessResult<object>.Fail(BomBusinessError.Conflict, "BOM 版本已被业务数据引用，不能删除");
        }

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM BOM_VERSION WHERE VERSION_ID = :versionId";
        cmd.Parameters.Add(new OracleParameter("versionId", versionId));
        cmd.ExecuteNonQuery();
        return BomBusinessResult<object>.Success(new object());
    }

    private const string SelectColumns = @"
        SELECT bv.VERSION_ID, bv.MATERIAL_ID, bv.VERSION_NO, bv.EFFECTIVE_DATE,
               bv.EXPIRE_DATE, bv.CHANGE_REASON, bv.CREATED_BY
        FROM BOM_VERSION bv";

    private static List<string> BuildWhere(long? materialId, string? versionNo, bool? effectiveOnly)
    {
        var where = new List<string>();
        if (materialId.HasValue)
        {
            where.Add("bv.MATERIAL_ID = :materialId");
        }

        if (!string.IsNullOrWhiteSpace(versionNo))
        {
            where.Add("bv.VERSION_NO LIKE :versionNo");
        }

        if (effectiveOnly == true)
        {
            where.Add("bv.EFFECTIVE_DATE <= TRUNC(CAST(SYSTIMESTAMP AT TIME ZONE 'Asia/Shanghai' AS DATE))");
            where.Add("(bv.EXPIRE_DATE IS NULL OR bv.EXPIRE_DATE >= TRUNC(CAST(SYSTIMESTAMP AT TIME ZONE 'Asia/Shanghai' AS DATE)))");
        }

        return where;
    }

    private static string? ValidateVersionInput(long materialId, string? versionNo, DateOnly effectiveDate, DateOnly? expireDate)
    {
        if (materialId <= 0)
        {
            return "所属物料不能为空";
        }

        if (string.IsNullOrWhiteSpace(versionNo))
        {
            return "版本号不能为空";
        }

        if (effectiveDate == default)
        {
            return "生效日期不能为空";
        }

        if (expireDate.HasValue && expireDate.Value < effectiveDate)
        {
            return "失效日期不能早于生效日期";
        }

        return null;
    }

    private static void AddWriteParameters(
        OracleCommand cmd,
        long materialId,
        string versionNo,
        DateOnly effectiveDate,
        DateOnly? expireDate,
        string? changeReason)
    {
        cmd.Parameters.Add(new OracleParameter("materialId", materialId));
        cmd.Parameters.Add(new OracleParameter("versionNo", versionNo));
        cmd.Parameters.Add(new OracleParameter("effectiveDate", effectiveDate.ToDateTime(TimeOnly.MinValue)));
        cmd.Parameters.Add(new OracleParameter("expireDate", expireDate.HasValue ? expireDate.Value.ToDateTime(TimeOnly.MinValue) : DBNull.Value));
        cmd.Parameters.Add(new OracleParameter("changeReason", string.IsNullOrWhiteSpace(changeReason) ? DBNull.Value : changeReason.Trim()));
    }

    private static BomVersion? GetVersionInternal(OracleConnection conn, long versionId, OracleTransaction? transaction = null)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = SelectColumns + " WHERE bv.VERSION_ID = :versionId";
        cmd.Parameters.Add(new OracleParameter("versionId", versionId));
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? MapVersion(reader) : null;
    }

    private static BomVersion MapVersion(OracleDataReader reader) => new()
    {
        VersionId = Convert.ToInt32(reader.GetValue(0)),
        MaterialId = Convert.ToInt32(reader.GetValue(1)),
        VersionNo = reader.GetString(2),
        EffectiveDate = DateOnly.FromDateTime(reader.GetDateTime(3)),
        ExpireDate = reader.IsDBNull(4) ? null : DateOnly.FromDateTime(reader.GetDateTime(4)),
        ChangeReason = reader.IsDBNull(5) ? null! : reader.GetString(5),
        CreatedBy = Convert.ToInt32(reader.GetValue(6)),
    };

    private static bool MaterialExists(OracleConnection conn, OracleTransaction transaction, long materialId)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = "SELECT COUNT(*) FROM MATERIAL WHERE MATERIAL_ID = :materialId";
        cmd.Parameters.Add(new OracleParameter("materialId", materialId));
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    private static bool VersionNoExists(
        OracleConnection conn,
        OracleTransaction transaction,
        long materialId,
        string versionNo,
        long? excludingVersionId)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = @"SELECT COUNT(*) FROM BOM_VERSION
                            WHERE MATERIAL_ID = :materialId
                              AND VERSION_NO = :versionNo
                              AND (:versionId IS NULL OR VERSION_ID <> :versionId)";
        cmd.Parameters.Add(new OracleParameter("materialId", materialId));
        cmd.Parameters.Add(new OracleParameter("versionNo", versionNo));
        cmd.Parameters.Add(new OracleParameter("versionId", excludingVersionId.HasValue ? excludingVersionId.Value : DBNull.Value));
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    private static bool IsCurrentVersion(OracleConnection conn, OracleTransaction transaction, long versionId)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = "SELECT COUNT(*) FROM MATERIAL WHERE CURRENT_VERSION_ID = :versionId";
        cmd.Parameters.Add(new OracleParameter("versionId", versionId));
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    private static bool IsEffectiveOnDatabaseDate(
        OracleConnection conn,
        OracleTransaction transaction,
        DateOnly effectiveDate,
        DateOnly? expireDate)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = "SELECT TRUNC(CAST(SYSTIMESTAMP AT TIME ZONE 'Asia/Shanghai' AS DATE)) FROM DUAL";
        var databaseDate = DateOnly.FromDateTime(Convert.ToDateTime(cmd.ExecuteScalar()));
        return effectiveDate <= databaseDate
            && (!expireDate.HasValue || expireDate.Value >= databaseDate);
    }

    private static bool ExistsBySql(OracleConnection conn, string sql, long id)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.Add(new OracleParameter("id", id));
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }
}
