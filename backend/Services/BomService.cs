using Oracle.ManagedDataAccess.Client;

using Org.OpenAPITools.Models;

namespace Backend.Services;

public class BomService(string connString)
{
    public (List<Bom> Records, int Total) ListBoms(
        int page,
        int pageSize,
        long versionId,
        long? parentMaterialId,
        long? childMaterialId)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();

        var where = BuildBomWhere(versionId, parentMaterialId, childMaterialId);
        var whereClause = " WHERE " + string.Join(" AND ", where);

        void AddFilters(OracleCommand cmd)
        {
            cmd.Parameters.Add(new OracleParameter("versionId", versionId));
            if (parentMaterialId.HasValue)
            {
                cmd.Parameters.Add(new OracleParameter("parentMaterialId", parentMaterialId.Value));
            }

            if (childMaterialId.HasValue)
            {
                cmd.Parameters.Add(new OracleParameter("childMaterialId", childMaterialId.Value));
            }
        }

        int total;
        using (var countCmd = conn.CreateCommand())
        {
            countCmd.CommandText = "SELECT COUNT(*) FROM BOM b" + whereClause;
            AddFilters(countCmd);
            total = Convert.ToInt32(countCmd.ExecuteScalar());
        }

        var records = new List<Bom>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = SelectColumns + whereClause +
                @" ORDER BY b.BOM_ID
                   OFFSET :skip ROWS FETCH NEXT :take ROWS ONLY";
            AddFilters(cmd);
            cmd.Parameters.Add(new OracleParameter("skip", (page - 1) * pageSize));
            cmd.Parameters.Add(new OracleParameter("take", pageSize));

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                records.Add(MapBom(reader));
            }
        }

        return (records, total);
    }

    public Bom? GetBom(long bomId)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();
        return GetBomInternal(conn, bomId);
    }

    public BomBusinessResult<Bom> AddBom(BomCreateRequest request)
    {
        var validation = ValidateBomInput(request.ParentMaterialId, request.ChildMaterialId, request.VersionId, request.Quantity, request.LossRate);
        if (validation is not null)
        {
            return BomBusinessResult<Bom>.Fail(BomBusinessError.BadRequest, validation);
        }

        using var conn = new OracleConnection(connString);
        conn.Open();
        using var transaction = conn.BeginTransaction();

        try
        {
            var businessError = ValidateBomReferences(conn, transaction, request.VersionId, request.ParentMaterialId, request.ChildMaterialId, null);
            if (businessError is not null)
            {
                transaction.Rollback();
                return businessError;
            }

            long newId;
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = @"INSERT INTO BOM
                                    (PARENT_MATERIAL_ID, CHILD_MATERIAL_ID, VERSION_ID, QUANTITY, LOSS_RATE)
                                    VALUES
                                    (:parentMaterialId, :childMaterialId, :versionId, :quantity, :lossRate)
                                    RETURNING BOM_ID INTO :newId";
                AddBomWriteParameters(cmd, request.ParentMaterialId, request.ChildMaterialId, request.VersionId, request.Quantity, request.LossRate);
                var idParam = new OracleParameter("newId", OracleDbType.Int64)
                {
                    Direction = System.Data.ParameterDirection.Output,
                };
                cmd.Parameters.Add(idParam);
                cmd.ExecuteNonQuery();
                newId = Convert.ToInt64(idParam.Value.ToString());
            }

            transaction.Commit();
            return BomBusinessResult<Bom>.Success(GetBomInternal(conn, newId)!);
        }
        catch (OracleException ex)
            when (OracleDomainErrorMapper.TryMap(ex, out int code, out string message))
        {
            transaction.Rollback();
            return BomBusinessResult<Bom>.Fail((BomBusinessError)code, message);
        }
        catch (OracleException ex) when (ex.Number == 1 || ex.Number == 2290 || ex.Number == 2291 || ex.Number == 2292)
        {
            transaction.Rollback();
            return BomBusinessResult<Bom>.Fail(BomBusinessError.Conflict, "BOM 明细关联数据冲突");
        }
    }

    public BomBusinessResult<Bom> UpdateBom(BomUpdateRequest request)
    {
        var validation = ValidateBomInput(request.ParentMaterialId, request.ChildMaterialId, request.VersionId, request.Quantity, request.LossRate);
        if (request.BomId <= 0)
        {
            validation = "BOM 明细编号不能为空";
        }

        if (validation is not null)
        {
            return BomBusinessResult<Bom>.Fail(BomBusinessError.BadRequest, validation);
        }

        using var conn = new OracleConnection(connString);
        conn.Open();
        using var transaction = conn.BeginTransaction();

        try
        {
            if (GetBomInternal(conn, request.BomId, transaction) is null)
            {
                transaction.Rollback();
                return BomBusinessResult<Bom>.Fail(BomBusinessError.NotFound, "BOM 明细不存在");
            }

            var businessError = ValidateBomReferences(conn, transaction, request.VersionId, request.ParentMaterialId, request.ChildMaterialId, request.BomId);
            if (businessError is not null)
            {
                transaction.Rollback();
                return businessError;
            }

            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = @"UPDATE BOM
                                    SET PARENT_MATERIAL_ID = :parentMaterialId,
                                        CHILD_MATERIAL_ID = :childMaterialId,
                                        VERSION_ID = :versionId,
                                        QUANTITY = :quantity,
                                        LOSS_RATE = :lossRate
                                    WHERE BOM_ID = :bomId";
                AddBomWriteParameters(cmd, request.ParentMaterialId, request.ChildMaterialId, request.VersionId, request.Quantity, request.LossRate);
                cmd.Parameters.Add(new OracleParameter("bomId", request.BomId));
                cmd.ExecuteNonQuery();
            }

            transaction.Commit();
            return BomBusinessResult<Bom>.Success(GetBomInternal(conn, request.BomId)!);
        }
        catch (OracleException ex)
            when (OracleDomainErrorMapper.TryMap(ex, out int code, out string message))
        {
            transaction.Rollback();
            return BomBusinessResult<Bom>.Fail((BomBusinessError)code, message);
        }
        catch (OracleException ex) when (ex.Number == 1 || ex.Number == 2290 || ex.Number == 2291 || ex.Number == 2292)
        {
            transaction.Rollback();
            return BomBusinessResult<Bom>.Fail(BomBusinessError.Conflict, "BOM 明细关联数据冲突");
        }
    }

    public BomBusinessResult<object> DeleteBom(long bomId)
    {
        if (bomId <= 0)
        {
            return BomBusinessResult<object>.Fail(BomBusinessError.BadRequest, "BOM 明细编号不能为空");
        }

        using var conn = new OracleConnection(connString);
        conn.Open();

        if (GetBomInternal(conn, bomId) is null)
        {
            return BomBusinessResult<object>.Fail(BomBusinessError.NotFound, "BOM 明细不存在");
        }

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM BOM WHERE BOM_ID = :bomId";
        cmd.Parameters.Add(new OracleParameter("bomId", bomId));
        cmd.ExecuteNonQuery();
        return BomBusinessResult<object>.Success(new object());
    }

    public BomCycleCheckResult CheckCycle(BomCycleCheckRequest request)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();
        using var command = OracleCommandFactory.Create(
            conn,
            "BEGIN PKG_BOM_DOMAIN.CHECK_EDGE_CYCLE(:parentMaterialId, :childMaterialId, :versionId, :hasCycle, :cyclePath); END;");
        command.Parameters.Add("parentMaterialId", OracleDbType.Int64).Value = request.ParentMaterialId;
        command.Parameters.Add("childMaterialId", OracleDbType.Int64).Value = request.ChildMaterialId;
        command.Parameters.Add("versionId", OracleDbType.Int64).Value = request.VersionId;
        var hasCycle = command.Parameters.Add("hasCycle", OracleDbType.Int32);
        hasCycle.Direction = System.Data.ParameterDirection.Output;
        var cyclePath = command.Parameters.Add("cyclePath", OracleDbType.Varchar2, 4000);
        cyclePath.Direction = System.Data.ParameterDirection.Output;
        command.ExecuteNonQuery();

        var found = Convert.ToInt32(hasCycle.Value.ToString()) == 1;
        var path = found && cyclePath.Value is not null
            ? cyclePath.Value.ToString()!
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(value => Convert.ToInt32(value))
                .ToList()
            : [];

        return new BomCycleCheckResult
        {
            HasCycle = found,
            CyclePath = path,
        };
    }

    public BomBusinessResult<List<BomTreeNode>> GetBomTree(long materialId, long versionId)
    {
        if (materialId <= 0 || versionId <= 0)
        {
            return BomBusinessResult<List<BomTreeNode>>.Fail(BomBusinessError.BadRequest, "物料和版本编号不能为空");
        }

        using var conn = new OracleConnection(connString);
        conn.Open();
        try
        {
            using var command = OracleCommandFactory.Create(
                conn,
                "BEGIN PKG_BOM_DOMAIN.OPEN_TREE(:materialId, :versionId, :rootQty, :rows); END;");
            command.Parameters.Add("materialId", OracleDbType.Int64).Value = materialId;
            command.Parameters.Add("versionId", OracleDbType.Int64).Value = versionId;
            command.Parameters.Add("rootQty", OracleDbType.Decimal).Value = 1;
            command.Parameters.Add("rows", OracleDbType.RefCursor).Direction =
                System.Data.ParameterDirection.Output;

            var nodes = new List<BomTreeNode>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                nodes.Add(new BomTreeNode
                {
                    MaterialId = Convert.ToInt32(reader.GetValue(0)),
                    MaterialName = reader.GetString(1),
                    Model = reader.IsDBNull(2) ? null! : reader.GetString(2),
                    MaterialType = reader.GetString(3),
                    Unit = reader.GetString(4),
                    Quantity = decimal.ToDouble(reader.GetDecimal(5)),
                    AccumulatedQuantity = decimal.ToDouble(reader.GetDecimal(6)),
                    Depth = Convert.ToInt32(reader.GetValue(7)),
                    ParentMaterialId = reader.IsDBNull(8)
                        ? null
                        : Convert.ToInt32(reader.GetValue(8)),
                    Path = reader.GetString(9),
                    IsLeaf = Convert.ToInt32(reader.GetValue(10)) == 1,
                });
            }

            return BomBusinessResult<List<BomTreeNode>>.Success(nodes);
        }
        catch (OracleException exception)
            when (OracleDomainErrorMapper.TryMap(exception, out int code, out string message))
        {
            return BomBusinessResult<List<BomTreeNode>>.Fail((BomBusinessError)code, message);
        }
    }

    public BomBusinessResult<List<ReverseTraceItem>> GetReverseTrace(
        long materialId,
        long versionId,
        bool includeHistory)
    {
        if (materialId <= 0 || versionId <= 0)
        {
            return BomBusinessResult<List<ReverseTraceItem>>.Fail(
                BomBusinessError.BadRequest,
                "物料和版本编号不能为空");
        }

        using var conn = new OracleConnection(connString);
        conn.Open();
        try
        {
            using var command = OracleCommandFactory.Create(
                conn,
                "BEGIN PKG_BOM_DOMAIN.OPEN_REVERSE_USAGE(:materialId, :versionId, :includeHistory, :rows); END;");
            command.Parameters.Add("materialId", OracleDbType.Int64).Value = materialId;
            command.Parameters.Add("versionId", OracleDbType.Int64).Value = versionId;
            command.Parameters.Add("includeHistory", OracleDbType.Int32).Value = includeHistory ? 1 : 0;
            command.Parameters.Add("rows", OracleDbType.RefCursor).Direction =
                System.Data.ParameterDirection.Output;

            var items = new List<ReverseTraceItem>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                items.Add(new ReverseTraceItem
                {
                    ProductMaterialId = Convert.ToInt32(reader.GetValue(0)),
                    ProductMaterialName = reader.GetString(1),
                    VersionId = Convert.ToInt32(reader.GetValue(2)),
                    VersionNo = reader.GetString(3),
                    VersionStatus = reader.GetString(4) == "effective"
                        ? ReverseTraceItem.VersionStatusEnum.EffectiveEnum
                        : ReverseTraceItem.VersionStatusEnum.HistoryEnum,
                    Quantity = decimal.ToDouble(reader.GetDecimal(5)),
                    AccumulatedQuantity = decimal.ToDouble(reader.GetDecimal(6)),
                    Depth = Convert.ToInt32(reader.GetValue(7)),
                    Path = reader.GetString(8),
                });
            }

            return BomBusinessResult<List<ReverseTraceItem>>.Success(items);
        }
        catch (OracleException exception)
            when (OracleDomainErrorMapper.TryMap(exception, out int code, out string message))
        {
            return BomBusinessResult<List<ReverseTraceItem>>.Fail((BomBusinessError)code, message);
        }
    }

    private const string SelectColumns = @"
        SELECT b.BOM_ID, b.PARENT_MATERIAL_ID, b.CHILD_MATERIAL_ID,
               b.VERSION_ID, b.QUANTITY, b.LOSS_RATE
        FROM BOM b";

    private static List<string> BuildBomWhere(long versionId, long? parentMaterialId, long? childMaterialId)
    {
        var where = new List<string> { "b.VERSION_ID = :versionId" };
        if (parentMaterialId.HasValue)
        {
            where.Add("b.PARENT_MATERIAL_ID = :parentMaterialId");
        }

        if (childMaterialId.HasValue)
        {
            where.Add("b.CHILD_MATERIAL_ID = :childMaterialId");
        }

        return where;
    }

    private static string? ValidateBomInput(long parentMaterialId, long childMaterialId, long versionId, double quantity, double lossRate)
    {
        if (versionId <= 0 || parentMaterialId <= 0 || childMaterialId <= 0)
        {
            return "版本、父物料和子物料编号不能为空";
        }

        if (parentMaterialId == childMaterialId)
        {
            return "父物料和子物料不能相同";
        }

        if (quantity <= 0)
        {
            return "用量必须大于 0";
        }

        if (lossRate < 0 || lossRate >= 1)
        {
            return "损耗率必须大于等于 0 且小于 1";
        }

        return null;
    }

    private static BomBusinessResult<Bom>? ValidateBomReferences(
        OracleConnection conn,
        OracleTransaction? transaction,
        long versionId,
        long parentMaterialId,
        long childMaterialId,
        long? excludingBomId)
    {
        var versionMaterialId = GetVersionMaterialId(conn, transaction, versionId);
        if (versionMaterialId is null)
        {
            return BomBusinessResult<Bom>.Fail(BomBusinessError.BadRequest, "BOM 版本不存在");
        }

        if (versionMaterialId.Value != parentMaterialId)
        {
            return BomBusinessResult<Bom>.Fail(BomBusinessError.BadRequest, "BOM 版本不属于父物料");
        }

        if (!MaterialExists(conn, transaction, childMaterialId))
        {
            return BomBusinessResult<Bom>.Fail(BomBusinessError.BadRequest, "子物料不存在");
        }

        if (BomEdgeExists(conn, transaction, versionId, parentMaterialId, childMaterialId, excludingBomId))
        {
            return BomBusinessResult<Bom>.Fail(BomBusinessError.Conflict, "同一版本下父子物料关系已存在");
        }

        return null;
    }

    private static void AddBomWriteParameters(
        OracleCommand cmd,
        long parentMaterialId,
        long childMaterialId,
        long versionId,
        double quantity,
        double lossRate)
    {
        cmd.Parameters.Add(new OracleParameter("parentMaterialId", parentMaterialId));
        cmd.Parameters.Add(new OracleParameter("childMaterialId", childMaterialId));
        cmd.Parameters.Add(new OracleParameter("versionId", versionId));
        cmd.Parameters.Add(new OracleParameter("quantity", Convert.ToDecimal(quantity)));
        cmd.Parameters.Add(new OracleParameter("lossRate", Convert.ToDecimal(lossRate)));
    }

    private static Bom? GetBomInternal(OracleConnection conn, long bomId, OracleTransaction? transaction = null)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = SelectColumns + " WHERE b.BOM_ID = :bomId";
        cmd.Parameters.Add(new OracleParameter("bomId", bomId));
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? MapBom(reader) : null;
    }

    private static Bom MapBom(OracleDataReader reader) => new()
    {
        BomId = Convert.ToInt32(reader.GetValue(0)),
        ParentMaterialId = Convert.ToInt32(reader.GetValue(1)),
        ChildMaterialId = Convert.ToInt32(reader.GetValue(2)),
        VersionId = Convert.ToInt32(reader.GetValue(3)),
        Quantity = decimal.ToDouble(reader.GetDecimal(4)),
        LossRate = decimal.ToDouble(reader.GetDecimal(5)),
    };

    private static long? GetVersionMaterialId(OracleConnection conn, OracleTransaction? transaction, long versionId)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = "SELECT MATERIAL_ID FROM BOM_VERSION WHERE VERSION_ID = :versionId";
        cmd.Parameters.Add(new OracleParameter("versionId", versionId));
        var result = cmd.ExecuteScalar();
        return result is null ? null : Convert.ToInt64(result);
    }

    private static bool MaterialExists(OracleConnection conn, OracleTransaction? transaction, long materialId)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = "SELECT COUNT(*) FROM MATERIAL WHERE MATERIAL_ID = :materialId";
        cmd.Parameters.Add(new OracleParameter("materialId", materialId));
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    private static bool BomEdgeExists(
        OracleConnection conn,
        OracleTransaction? transaction,
        long versionId,
        long parentMaterialId,
        long childMaterialId,
        long? excludingBomId)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = @"SELECT COUNT(*) FROM BOM
                            WHERE VERSION_ID = :versionId
                              AND PARENT_MATERIAL_ID = :parentMaterialId
                              AND CHILD_MATERIAL_ID = :childMaterialId
                              AND (:bomId IS NULL OR BOM_ID <> :bomId)";
        cmd.Parameters.Add(new OracleParameter("versionId", versionId));
        cmd.Parameters.Add(new OracleParameter("parentMaterialId", parentMaterialId));
        cmd.Parameters.Add(new OracleParameter("childMaterialId", childMaterialId));
        cmd.Parameters.Add(new OracleParameter("bomId", excludingBomId.HasValue ? excludingBomId.Value : DBNull.Value));
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

}
