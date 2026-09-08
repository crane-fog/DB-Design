using Oracle.ManagedDataAccess.Client;

using Org.OpenAPITools.Models;

namespace Backend.Services;

public enum MaterialCatalogError
{
    BadRequest = 400,
    NotFound = 404,
    Conflict = 409,
}

public sealed record MaterialCatalogResult<T>(
    bool Ok,
    T? Data,
    MaterialCatalogError Error,
    string? ErrorMessage)
{
    public static MaterialCatalogResult<T> Success(T data) =>
        new(true, data, 0, null);

    public static MaterialCatalogResult<T> Fail(MaterialCatalogError error, string message) =>
        new(false, default, error, message);
}

public class MaterialStockIntegrationService(string connString) : IStockReadQuery, IStockInitialization
{
    public IReadOnlyDictionary<long, StockSnapshot> GetSnapshots(IReadOnlyCollection<long> materialIds)
    {
        if (materialIds.Count == 0)
        {
            return new Dictionary<long, StockSnapshot>();
        }

        using var conn = new OracleConnection(connString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        var names = materialIds.Select((_, index) => $":id{index}").ToArray();
        cmd.CommandText = $@"SELECT MATERIAL_ID, AVAILABLE_QTY, LOCKED_QTY, LAST_IN_DATE, LAST_OUT_DATE
                             FROM MATERIAL_STOCK
                             WHERE MATERIAL_ID IN ({string.Join(", ", names)})";

        var index = 0;
        foreach (var materialId in materialIds)
        {
            cmd.Parameters.Add(new OracleParameter($"id{index}", materialId));
            index++;
        }

        var result = new Dictionary<long, StockSnapshot>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var materialId = Convert.ToInt64(reader.GetValue(0));
            result[materialId] = new StockSnapshot(
                materialId,
                reader.GetDecimal(1),
                reader.GetDecimal(2),
                reader.IsDBNull(3) ? null : reader.GetUtcDateTime(3),
                reader.IsDBNull(4) ? null : reader.GetUtcDateTime(4));
        }

        return result;
    }

    public void EnsureStockRecord(OracleConnection connection, OracleTransaction transaction, long materialId)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = @"MERGE INTO MATERIAL_STOCK target
                            USING (SELECT :materialId AS MATERIAL_ID FROM DUAL) source
                            ON (target.MATERIAL_ID = source.MATERIAL_ID)
                            WHEN NOT MATCHED THEN
                                INSERT (MATERIAL_ID, AVAILABLE_QTY, LOCKED_QTY)
                                VALUES (source.MATERIAL_ID, 0, 0)";
        cmd.Parameters.Add(new OracleParameter("materialId", materialId));
        cmd.ExecuteNonQuery();
    }
}

public class MaterialCatalogService(
    string connString,
    IStockReadQuery stockReadQuery,
    IStockInitialization stockInitialization,
    BomGraphValidationService bomGraphValidation)
{
    public (List<MaterialCategory> Records, int Total) ListCategories(int page, int pageSize, string? categoryName)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();

        var where = string.IsNullOrWhiteSpace(categoryName)
            ? string.Empty
            : " WHERE CATEGORY_NAME LIKE :categoryName";

        int total;
        using (var countCmd = conn.CreateCommand())
        {
            countCmd.CommandText = "SELECT COUNT(*) FROM MATERIAL_CATEGORY" + where;
            if (!string.IsNullOrWhiteSpace(categoryName))
            {
                countCmd.Parameters.Add(new OracleParameter("categoryName", $"%{categoryName.Trim()}%"));
            }

            total = Convert.ToInt32(countCmd.ExecuteScalar());
        }

        var records = new List<MaterialCategory>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"SELECT CATEGORY_ID, CATEGORY_NAME
                                FROM MATERIAL_CATEGORY" + where +
                @" ORDER BY CATEGORY_ID
                   OFFSET :skip ROWS FETCH NEXT :take ROWS ONLY";
            if (!string.IsNullOrWhiteSpace(categoryName))
            {
                cmd.Parameters.Add(new OracleParameter("categoryName", $"%{categoryName.Trim()}%"));
            }

            cmd.Parameters.Add(new OracleParameter("skip", (page - 1) * pageSize));
            cmd.Parameters.Add(new OracleParameter("take", pageSize));

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                records.Add(new MaterialCategory
                {
                    CategoryId = Convert.ToInt32(reader.GetValue(0)),
                    CategoryName = reader.GetString(1),
                });
            }
        }

        return (records, total);
    }

    public MaterialCatalogResult<MaterialCategory> AddCategory(MaterialCategoryCreateRequest request)
    {
        var name = request.CategoryName?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return MaterialCatalogResult<MaterialCategory>.Fail(MaterialCatalogError.BadRequest, "分类名称不能为空");
        }

        using var conn = new OracleConnection(connString);
        conn.Open();

        if (CategoryNameExists(conn, name, null))
        {
            return MaterialCatalogResult<MaterialCategory>.Fail(MaterialCatalogError.Conflict, "分类名称已存在");
        }

        long newId;
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"INSERT INTO MATERIAL_CATEGORY (CATEGORY_NAME)
                                VALUES (:categoryName)
                                RETURNING CATEGORY_ID INTO :newId";
            cmd.Parameters.Add(new OracleParameter("categoryName", name));
            var idParam = new OracleParameter("newId", OracleDbType.Int64)
            {
                Direction = System.Data.ParameterDirection.Output,
            };
            cmd.Parameters.Add(idParam);
            cmd.ExecuteNonQuery();
            newId = Convert.ToInt64(idParam.Value.ToString());
        }

        return MaterialCatalogResult<MaterialCategory>.Success(GetCategoryInternal(conn, newId)!);
    }

    public MaterialCatalogResult<MaterialCategory> UpdateCategory(MaterialCategoryUpdateRequest request)
    {
        var name = request.CategoryName?.Trim();
        if (request.CategoryId <= 0 || string.IsNullOrWhiteSpace(name))
        {
            return MaterialCatalogResult<MaterialCategory>.Fail(MaterialCatalogError.BadRequest, "分类编号和名称不能为空");
        }

        using var conn = new OracleConnection(connString);
        conn.Open();

        if (GetCategoryInternal(conn, request.CategoryId) is null)
        {
            return MaterialCatalogResult<MaterialCategory>.Fail(MaterialCatalogError.NotFound, "分类不存在");
        }

        if (CategoryNameExists(conn, name, request.CategoryId))
        {
            return MaterialCatalogResult<MaterialCategory>.Fail(MaterialCatalogError.Conflict, "分类名称已存在");
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"UPDATE MATERIAL_CATEGORY
                                SET CATEGORY_NAME = :categoryName
                                WHERE CATEGORY_ID = :categoryId";
            cmd.Parameters.Add(new OracleParameter("categoryName", name));
            cmd.Parameters.Add(new OracleParameter("categoryId", request.CategoryId));
            cmd.ExecuteNonQuery();
        }

        return MaterialCatalogResult<MaterialCategory>.Success(GetCategoryInternal(conn, request.CategoryId)!);
    }

    public MaterialCatalogResult<object> DeleteCategory(long categoryId)
    {
        if (categoryId <= 0)
        {
            return MaterialCatalogResult<object>.Fail(MaterialCatalogError.BadRequest, "分类编号不能为空");
        }

        using var conn = new OracleConnection(connString);
        conn.Open();

        if (GetCategoryInternal(conn, categoryId) is null)
        {
            return MaterialCatalogResult<object>.Fail(MaterialCatalogError.NotFound, "分类不存在");
        }

        if (ExistsBySql(conn, "SELECT COUNT(*) FROM MATERIAL WHERE CATEGORY_ID = :id", categoryId))
        {
            return MaterialCatalogResult<object>.Fail(MaterialCatalogError.Conflict, "分类已被物料引用，不能删除");
        }

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM MATERIAL_CATEGORY WHERE CATEGORY_ID = :categoryId";
        cmd.Parameters.Add(new OracleParameter("categoryId", categoryId));
        cmd.ExecuteNonQuery();
        return MaterialCatalogResult<object>.Success(new object());
    }

    public (List<MaterialDetail> Records, int Total) ListMaterials(
        int page,
        int pageSize,
        long? materialId,
        string? materialName,
        string? materialType,
        long? categoryId,
        long? defaultSupplierId,
        decimal? minSafetyStock,
        decimal? maxSafetyStock,
        DateTime? createdStartTime,
        DateTime? createdEndTime,
        DateTime? updatedStartTime,
        DateTime? updatedEndTime)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();

        var where = BuildMaterialWhere(
            materialId,
            materialName,
            MaterialTypeMap.ToDbOrNull(materialType),
            categoryId,
            defaultSupplierId,
            minSafetyStock,
            maxSafetyStock,
            createdStartTime,
            createdEndTime,
            updatedStartTime,
            updatedEndTime);
        var whereClause = where.Count > 0 ? " WHERE " + string.Join(" AND ", where) : string.Empty;

        void AddFilters(OracleCommand cmd) => AddMaterialFilterParameters(
            cmd,
            materialId,
            materialName,
            MaterialTypeMap.ToDbOrNull(materialType),
            categoryId,
            defaultSupplierId,
            minSafetyStock,
            maxSafetyStock,
            createdStartTime,
            createdEndTime,
            updatedStartTime,
            updatedEndTime);

        int total;
        using (var countCmd = conn.CreateCommand())
        {
            countCmd.CommandText = "SELECT COUNT(*) FROM MATERIAL m" + whereClause;
            AddFilters(countCmd);
            total = Convert.ToInt32(countCmd.ExecuteScalar());
        }

        var records = new List<MaterialDetail>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = MaterialSelectColumns + whereClause +
                @" ORDER BY m.MATERIAL_ID
                   OFFSET :skip ROWS FETCH NEXT :take ROWS ONLY";
            AddFilters(cmd);
            cmd.Parameters.Add(new OracleParameter("skip", (page - 1) * pageSize));
            cmd.Parameters.Add(new OracleParameter("take", pageSize));

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                records.Add(MapMaterialDetail(reader));
            }
        }

        return (records, total);
    }

    public MaterialDetail? GetMaterial(long materialId)
    {
        using var conn = new OracleConnection(connString);
        conn.Open();
        return GetMaterialInternal(conn, materialId);
    }

    public MaterialCatalogResult<MaterialDetail> AddMaterial(MaterialCreateRequest request, long createdBy)
    {
        if (!MaterialTypeMap.IsDefined(request.MaterialType))
        {
            return MaterialCatalogResult<MaterialDetail>.Fail(MaterialCatalogError.BadRequest, "物料类型不合法");
        }

        var validation = ValidateMaterialInput(
            request.MaterialName,
            request.Unit,
            request.CategoryId,
            request.SafetyStock,
            request.DefaultSupplierId,
            request.CurrentVersionId);
        if (validation is not null)
        {
            return MaterialCatalogResult<MaterialDetail>.Fail(MaterialCatalogError.BadRequest, validation);
        }

        using var conn = new OracleConnection(connString);
        conn.Open();
        using var transaction = conn.BeginTransaction();

        try
        {
            var businessError = ValidateMaterialReferences(
                conn,
                request.CategoryId,
                request.DefaultSupplierId,
                request.CurrentVersionId,
                null);
            if (businessError is not null)
            {
                transaction.Rollback();
                return businessError;
            }

            long newId;
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = @"INSERT INTO MATERIAL
                                    (MATERIAL_NAME, MATERIAL_TYPE, MODEL, UNIT, CATEGORY_ID, SAFETY_STOCK,
                                     DEFAULT_SUPPLIER_ID, CURRENT_VERSION_ID, CREATED_BY, CREATED_TIME, UPDATED_TIME)
                                    VALUES
                                    (:materialName, :materialType, :model, :unit, :categoryId, :safetyStock,
                                     :defaultSupplierId, :currentVersionId, :createdBy, SYS_EXTRACT_UTC(SYSTIMESTAMP), SYS_EXTRACT_UTC(SYSTIMESTAMP))
                                    RETURNING MATERIAL_ID INTO :newId";
                AddMaterialWriteParameters(
                    cmd,
                    request.MaterialName.Trim(),
                    MaterialTypeMap.ToDb(request.MaterialType),
                    request.Model,
                    request.Unit.Trim(),
                    request.CategoryId,
                    Convert.ToDecimal(request.SafetyStock),
                    request.DefaultSupplierId,
                    request.CurrentVersionId);
                cmd.Parameters.Add(new OracleParameter("createdBy", createdBy));
                var idParam = new OracleParameter("newId", OracleDbType.Int64)
                {
                    Direction = System.Data.ParameterDirection.Output,
                };
                cmd.Parameters.Add(idParam);
                cmd.ExecuteNonQuery();
                newId = Convert.ToInt64(idParam.Value.ToString());
            }

            stockInitialization.EnsureStockRecord(conn, transaction, newId);
            transaction.Commit();

            return MaterialCatalogResult<MaterialDetail>.Success(GetMaterialInternal(conn, newId)!);
        }
        catch (OracleException ex) when (ex.Number == 1 || ex.Number == 2291 || ex.Number == 2292)
        {
            transaction.Rollback();
            return MaterialCatalogResult<MaterialDetail>.Fail(MaterialCatalogError.Conflict, "物料关联数据冲突");
        }
    }

    public MaterialCatalogResult<MaterialDetail> UpdateMaterial(MaterialUpdateRequest request)
    {
        if (!MaterialTypeMap.IsDefined(request.MaterialType))
        {
            return MaterialCatalogResult<MaterialDetail>.Fail(MaterialCatalogError.BadRequest, "物料类型不合法");
        }

        var validation = ValidateMaterialInput(
            request.MaterialName,
            request.Unit,
            request.CategoryId,
            request.SafetyStock,
            request.DefaultSupplierId,
            request.CurrentVersionId);
        if (request.MaterialId <= 0)
        {
            validation = "物料编号不能为空";
        }

        if (validation is not null)
        {
            return MaterialCatalogResult<MaterialDetail>.Fail(MaterialCatalogError.BadRequest, validation);
        }

        using var conn = new OracleConnection(connString);
        conn.Open();

        var existing = GetMaterialInternal(conn, request.MaterialId);
        if (existing is null)
        {
            return MaterialCatalogResult<MaterialDetail>.Fail(MaterialCatalogError.NotFound, "物料不存在");
        }

        var businessError = ValidateMaterialReferences(
            conn,
            request.CategoryId,
            request.DefaultSupplierId,
            request.CurrentVersionId,
            request.MaterialId);
        if (businessError is not null)
        {
            return businessError;
        }

        using var transaction = conn.BeginTransaction();
        try
        {
            if (request.CurrentVersionId.HasValue
                && existing.CurrentVersionId != request.CurrentVersionId)
            {
                var graphValidation = bomGraphValidation.ValidateActivation(
                    conn,
                    transaction,
                    request.MaterialId,
                    request.CurrentVersionId.Value);
                if (graphValidation.HasCycle)
                {
                    transaction.Rollback();
                    return MaterialCatalogResult<MaterialDetail>.Fail(
                        MaterialCatalogError.Conflict,
                        $"发布该 BOM 版本会形成循环依赖：{string.Join(" -> ", graphValidation.CyclePath)}");
                }
            }

            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = @"UPDATE MATERIAL
                                    SET MATERIAL_NAME = :materialName,
                                        MATERIAL_TYPE = :materialType,
                                        MODEL = :model,
                                        UNIT = :unit,
                                        CATEGORY_ID = :categoryId,
                                        SAFETY_STOCK = :safetyStock,
                                        DEFAULT_SUPPLIER_ID = :defaultSupplierId,
                                        CURRENT_VERSION_ID = :currentVersionId,
                                        UPDATED_TIME = SYS_EXTRACT_UTC(SYSTIMESTAMP)
                                    WHERE MATERIAL_ID = :materialId";
                AddMaterialWriteParameters(
                    cmd,
                    request.MaterialName.Trim(),
                    MaterialTypeMap.ToDb(request.MaterialType),
                    request.Model,
                    request.Unit.Trim(),
                    request.CategoryId,
                    Convert.ToDecimal(request.SafetyStock),
                    request.DefaultSupplierId,
                    request.CurrentVersionId);
                cmd.Parameters.Add(new OracleParameter("materialId", request.MaterialId));
                cmd.ExecuteNonQuery();
            }

            transaction.Commit();
        }
        catch (OracleException ex) when (ex.Number == 1 || ex.Number == 2290 || ex.Number == 2291 || ex.Number == 2292)
        {
            transaction.Rollback();
            return MaterialCatalogResult<MaterialDetail>.Fail(MaterialCatalogError.Conflict, "物料关联数据冲突");
        }

        return MaterialCatalogResult<MaterialDetail>.Success(GetMaterialInternal(conn, request.MaterialId)!);
    }

    public MaterialCatalogResult<object> DeleteMaterial(long materialId)
    {
        if (materialId <= 0)
        {
            return MaterialCatalogResult<object>.Fail(MaterialCatalogError.BadRequest, "物料编号不能为空");
        }

        using var conn = new OracleConnection(connString);
        conn.Open();

        if (GetMaterialInternal(conn, materialId) is null)
        {
            return MaterialCatalogResult<object>.Fail(MaterialCatalogError.NotFound, "物料不存在");
        }

        if (HasMaterialReferences(conn, materialId))
        {
            return MaterialCatalogResult<object>.Fail(MaterialCatalogError.Conflict, "物料已被业务数据引用，不能删除");
        }

        using var transaction = conn.BeginTransaction();
        try
        {
            using (var stockCmd = conn.CreateCommand())
            {
                stockCmd.Transaction = transaction;
                stockCmd.CommandText = @"DELETE FROM MATERIAL_STOCK
                                         WHERE MATERIAL_ID = :materialId
                                           AND AVAILABLE_QTY = 0
                                           AND LOCKED_QTY = 0";
                stockCmd.Parameters.Add(new OracleParameter("materialId", materialId));
                stockCmd.ExecuteNonQuery();
            }

            using (var materialCmd = conn.CreateCommand())
            {
                materialCmd.Transaction = transaction;
                materialCmd.CommandText = "DELETE FROM MATERIAL WHERE MATERIAL_ID = :materialId";
                materialCmd.Parameters.Add(new OracleParameter("materialId", materialId));
                materialCmd.ExecuteNonQuery();
            }

            transaction.Commit();
            return MaterialCatalogResult<object>.Success(new object());
        }
        catch (OracleException ex) when (ex.Number == 2292)
        {
            transaction.Rollback();
            return MaterialCatalogResult<object>.Fail(MaterialCatalogError.Conflict, "物料已被业务数据引用，不能删除");
        }
    }

    private const string MaterialSelectColumns = @"
        SELECT m.MATERIAL_ID, m.MATERIAL_NAME, m.MATERIAL_TYPE, m.MODEL, m.UNIT,
               m.CATEGORY_ID, c.CATEGORY_NAME, m.SAFETY_STOCK, m.DEFAULT_SUPPLIER_ID,
               s.SUPPLIER_NAME, bv.VERSION_ID, bv.VERSION_NO, m.CREATED_BY,
               m.CREATED_TIME, m.UPDATED_TIME, ms.AVAILABLE_QTY, ms.LOCKED_QTY,
               ms.LAST_IN_DATE, ms.LAST_OUT_DATE
        FROM MATERIAL m
        JOIN MATERIAL_CATEGORY c ON c.CATEGORY_ID = m.CATEGORY_ID
        LEFT JOIN SUPPLIER s ON s.SUPPLIER_ID = m.DEFAULT_SUPPLIER_ID
        LEFT JOIN BOM_VERSION bv ON bv.VERSION_ID = m.CURRENT_VERSION_ID
                                AND bv.EFFECTIVE_DATE <= TRUNC(CAST(SYSTIMESTAMP AT TIME ZONE 'Asia/Shanghai' AS DATE))
                                AND (bv.EXPIRE_DATE IS NULL OR bv.EXPIRE_DATE >= TRUNC(CAST(SYSTIMESTAMP AT TIME ZONE 'Asia/Shanghai' AS DATE)))
        LEFT JOIN MATERIAL_STOCK ms ON ms.MATERIAL_ID = m.MATERIAL_ID";

    private static List<string> BuildMaterialWhere(
        long? materialId,
        string? materialName,
        string? materialType,
        long? categoryId,
        long? defaultSupplierId,
        decimal? minSafetyStock,
        decimal? maxSafetyStock,
        DateTime? createdStartTime,
        DateTime? createdEndTime,
        DateTime? updatedStartTime,
        DateTime? updatedEndTime)
    {
        var where = new List<string>();
        if (materialId.HasValue)
        {
            where.Add("m.MATERIAL_ID = :materialId");
        }

        if (!string.IsNullOrWhiteSpace(materialName))
        {
            where.Add("m.MATERIAL_NAME LIKE :materialName");
        }

        if (!string.IsNullOrWhiteSpace(materialType))
        {
            where.Add("m.MATERIAL_TYPE = :materialType");
        }

        if (categoryId.HasValue)
        {
            where.Add("m.CATEGORY_ID = :categoryId");
        }

        if (defaultSupplierId.HasValue)
        {
            where.Add("m.DEFAULT_SUPPLIER_ID = :defaultSupplierId");
        }

        if (minSafetyStock.HasValue)
        {
            where.Add("m.SAFETY_STOCK >= :minSafetyStock");
        }

        if (maxSafetyStock.HasValue)
        {
            where.Add("m.SAFETY_STOCK <= :maxSafetyStock");
        }

        if (createdStartTime.HasValue)
        {
            where.Add("m.CREATED_TIME >= :createdStartTime");
        }

        if (createdEndTime.HasValue)
        {
            where.Add("m.CREATED_TIME <= :createdEndTime");
        }

        if (updatedStartTime.HasValue)
        {
            where.Add("m.UPDATED_TIME >= :updatedStartTime");
        }

        if (updatedEndTime.HasValue)
        {
            where.Add("m.UPDATED_TIME <= :updatedEndTime");
        }

        return where;
    }

    private static void AddMaterialFilterParameters(
        OracleCommand cmd,
        long? materialId,
        string? materialName,
        string? materialType,
        long? categoryId,
        long? defaultSupplierId,
        decimal? minSafetyStock,
        decimal? maxSafetyStock,
        DateTime? createdStartTime,
        DateTime? createdEndTime,
        DateTime? updatedStartTime,
        DateTime? updatedEndTime)
    {
        if (materialId.HasValue)
        {
            cmd.Parameters.Add(new OracleParameter("materialId", materialId.Value));
        }

        if (!string.IsNullOrWhiteSpace(materialName))
        {
            cmd.Parameters.Add(new OracleParameter("materialName", $"%{materialName.Trim()}%"));
        }

        if (!string.IsNullOrWhiteSpace(materialType))
        {
            cmd.Parameters.Add(new OracleParameter("materialType", materialType));
        }

        if (categoryId.HasValue)
        {
            cmd.Parameters.Add(new OracleParameter("categoryId", categoryId.Value));
        }

        if (defaultSupplierId.HasValue)
        {
            cmd.Parameters.Add(new OracleParameter("defaultSupplierId", defaultSupplierId.Value));
        }

        if (minSafetyStock.HasValue)
        {
            cmd.Parameters.Add(new OracleParameter("minSafetyStock", minSafetyStock.Value));
        }

        if (maxSafetyStock.HasValue)
        {
            cmd.Parameters.Add(new OracleParameter("maxSafetyStock", maxSafetyStock.Value));
        }

        if (createdStartTime.HasValue)
        {
            cmd.Parameters.Add(new OracleParameter("createdStartTime", createdStartTime.Value));
        }

        if (createdEndTime.HasValue)
        {
            cmd.Parameters.Add(new OracleParameter("createdEndTime", createdEndTime.Value));
        }

        if (updatedStartTime.HasValue)
        {
            cmd.Parameters.Add(new OracleParameter("updatedStartTime", updatedStartTime.Value));
        }

        if (updatedEndTime.HasValue)
        {
            cmd.Parameters.Add(new OracleParameter("updatedEndTime", updatedEndTime.Value));
        }
    }

    private static void AddMaterialWriteParameters(
        OracleCommand cmd,
        string materialName,
        string materialType,
        string? model,
        string unit,
        long categoryId,
        decimal safetyStock,
        long? defaultSupplierId,
        long? currentVersionId)
    {
        cmd.Parameters.Add(new OracleParameter("materialName", materialName));
        cmd.Parameters.Add(new OracleParameter("materialType", materialType));
        cmd.Parameters.Add(new OracleParameter("model", string.IsNullOrWhiteSpace(model) ? DBNull.Value : model.Trim()));
        cmd.Parameters.Add(new OracleParameter("unit", unit));
        cmd.Parameters.Add(new OracleParameter("categoryId", categoryId));
        cmd.Parameters.Add(new OracleParameter("safetyStock", safetyStock));
        cmd.Parameters.Add(new OracleParameter("defaultSupplierId", defaultSupplierId.HasValue ? defaultSupplierId.Value : DBNull.Value));
        cmd.Parameters.Add(new OracleParameter("currentVersionId", currentVersionId.HasValue ? currentVersionId.Value : DBNull.Value));
    }

    private static string? ValidateMaterialInput(
        string? materialName,
        string? unit,
        long categoryId,
        double safetyStock,
        long? defaultSupplierId,
        long? currentVersionId)
    {
        if (string.IsNullOrWhiteSpace(materialName))
        {
            return "物料名称不能为空";
        }

        if (string.IsNullOrWhiteSpace(unit))
        {
            return "计量单位不能为空";
        }

        if (categoryId <= 0)
        {
            return "物料分类不能为空";
        }

        if (safetyStock < 0)
        {
            return "安全库存不能为负数";
        }

        if (defaultSupplierId is <= 0 || currentVersionId is <= 0)
        {
            return "关联编号必须大于 0";
        }

        return null;
    }

    private static MaterialCatalogResult<MaterialDetail>? ValidateMaterialReferences(
        OracleConnection conn,
        long categoryId,
        long? defaultSupplierId,
        long? currentVersionId,
        long? materialId)
    {
        if (!ExistsBySql(conn, "SELECT COUNT(*) FROM MATERIAL_CATEGORY WHERE CATEGORY_ID = :id", categoryId))
        {
            return MaterialCatalogResult<MaterialDetail>.Fail(MaterialCatalogError.BadRequest, "物料分类不存在");
        }

        if (defaultSupplierId.HasValue
            && !ExistsBySql(conn, "SELECT COUNT(*) FROM SUPPLIER WHERE SUPPLIER_ID = :id", defaultSupplierId.Value))
        {
            return MaterialCatalogResult<MaterialDetail>.Fail(MaterialCatalogError.BadRequest, "默认供应商不存在");
        }

        if (currentVersionId.HasValue)
        {
            if (!materialId.HasValue)
            {
                return MaterialCatalogResult<MaterialDetail>.Fail(MaterialCatalogError.BadRequest, "新增物料时不能直接绑定当前 BOM 版本");
            }

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT MATERIAL_ID,
                                       CASE
                                           WHEN EFFECTIVE_DATE <= TRUNC(CAST(SYSTIMESTAMP AT TIME ZONE 'Asia/Shanghai' AS DATE))
                                            AND (EXPIRE_DATE IS NULL OR EXPIRE_DATE >= TRUNC(CAST(SYSTIMESTAMP AT TIME ZONE 'Asia/Shanghai' AS DATE)))
                                           THEN 1 ELSE 0
                                       END AS IS_EFFECTIVE
                                FROM BOM_VERSION
                                WHERE VERSION_ID = :versionId";
            cmd.Parameters.Add(new OracleParameter("versionId", currentVersionId.Value));
            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                return MaterialCatalogResult<MaterialDetail>.Fail(MaterialCatalogError.BadRequest, "当前 BOM 版本不存在");
            }

            if (Convert.ToInt64(reader.GetValue(0)) != materialId.Value)
            {
                return MaterialCatalogResult<MaterialDetail>.Fail(MaterialCatalogError.BadRequest, "当前 BOM 版本不属于该物料");
            }

            if (Convert.ToInt32(reader.GetValue(1)) != 1)
            {
                return MaterialCatalogResult<MaterialDetail>.Fail(MaterialCatalogError.Conflict, "只能发布当前处于有效期内的 BOM 版本");
            }
        }

        return null;
    }

    private static bool CategoryNameExists(OracleConnection conn, string categoryName, long? excludingCategoryId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT COUNT(*) FROM MATERIAL_CATEGORY
                            WHERE CATEGORY_NAME = :categoryName
                              AND (:categoryId IS NULL OR CATEGORY_ID <> :categoryId)";
        cmd.Parameters.Add(new OracleParameter("categoryName", categoryName));
        cmd.Parameters.Add(new OracleParameter("categoryId", excludingCategoryId.HasValue ? excludingCategoryId.Value : DBNull.Value));
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    private static MaterialCategory? GetCategoryInternal(OracleConnection conn, long categoryId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT CATEGORY_ID, CATEGORY_NAME FROM MATERIAL_CATEGORY WHERE CATEGORY_ID = :categoryId";
        cmd.Parameters.Add(new OracleParameter("categoryId", categoryId));
        using var reader = cmd.ExecuteReader();
        return reader.Read()
            ? new MaterialCategory
            {
                CategoryId = Convert.ToInt32(reader.GetValue(0)),
                CategoryName = reader.GetString(1),
            }
            : null;
    }

    private MaterialDetail? GetMaterialInternal(OracleConnection conn, long materialId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = MaterialSelectColumns + " WHERE m.MATERIAL_ID = :materialId";
        cmd.Parameters.Add(new OracleParameter("materialId", materialId));
        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        var detail = MapMaterialDetail(reader);
        if (stockReadQuery.GetSnapshots([materialId]).TryGetValue(materialId, out var snapshot))
        {
            detail.AvailableQty = decimal.ToDouble(snapshot.AvailableQty);
            detail.LockedQty = decimal.ToDouble(snapshot.LockedQty);
            detail.LastInDate = snapshot.LastInDate;
            detail.LastOutDate = snapshot.LastOutDate;
        }

        return detail;
    }

    private static MaterialDetail MapMaterialDetail(OracleDataReader reader) => new()
    {
        MaterialId = Convert.ToInt32(reader.GetValue(0)),
        MaterialName = reader.GetString(1),
        MaterialType = MaterialTypeMap.FromDb(reader.GetString(2)),
        Model = reader.IsDBNull(3) ? null! : reader.GetString(3),
        Unit = reader.GetString(4),
        CategoryId = Convert.ToInt32(reader.GetValue(5)),
        CategoryName = reader.GetString(6),
        SafetyStock = decimal.ToDouble(reader.GetDecimal(7)),
        DefaultSupplierId = reader.IsDBNull(8) ? null : Convert.ToInt32(reader.GetValue(8)),
        SupplierName = reader.IsDBNull(9) ? null! : reader.GetString(9),
        CurrentVersionId = reader.IsDBNull(10) ? null : Convert.ToInt32(reader.GetValue(10)),
        CurrentVersionNo = reader.IsDBNull(11) ? null! : reader.GetString(11),
        CreatedBy = Convert.ToInt32(reader.GetValue(12)),
        CreatedTime = reader.GetUtcDateTime(13),
        UpdatedTime = reader.GetUtcDateTime(14),
        AvailableQty = reader.IsDBNull(15) ? 0 : decimal.ToDouble(reader.GetDecimal(15)),
        LockedQty = reader.IsDBNull(16) ? 0 : decimal.ToDouble(reader.GetDecimal(16)),
        LastInDate = reader.IsDBNull(17) ? null : reader.GetUtcDateTime(17),
        LastOutDate = reader.IsDBNull(18) ? null : reader.GetUtcDateTime(18),
    };

    private static bool HasMaterialReferences(OracleConnection conn, long materialId) =>
        ExistsBySql(conn, "SELECT COUNT(*) FROM BOM_VERSION WHERE MATERIAL_ID = :id", materialId)
        || ExistsBySql(conn, "SELECT COUNT(*) FROM BOM WHERE PARENT_MATERIAL_ID = :id OR CHILD_MATERIAL_ID = :id", materialId)
        || ExistsBySql(conn, "SELECT COUNT(*) FROM PRODUCTION_ORDER WHERE MATERIAL_ID = :id", materialId)
        || ExistsBySql(conn, "SELECT COUNT(*) FROM EXTERNAL_ORDER WHERE MATERIAL_ID = :id", materialId)
        || ExistsBySql(conn, "SELECT COUNT(*) FROM MATERIAL_STOCK WHERE MATERIAL_ID = :id AND (AVAILABLE_QTY <> 0 OR LOCKED_QTY <> 0)", materialId);

    private static bool ExistsBySql(OracleConnection conn, string sql, long id)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.Add(new OracleParameter("id", id));
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }
}

public static class MaterialTypeMap
{
    private static readonly Dictionary<string, MaterialDetail.MaterialTypeEnum> DbToDetail = new()
    {
        ["原材料"] = MaterialDetail.MaterialTypeEnum.RawMaterialEnum,
        ["半成品"] = MaterialDetail.MaterialTypeEnum.SemiFinishedEnum,
        ["成品"] = MaterialDetail.MaterialTypeEnum.FinishedEnum,
        ["辅料"] = MaterialDetail.MaterialTypeEnum.AuxiliaryEnum,
    };

    private static readonly Dictionary<MaterialCreateRequest.MaterialTypeEnum, string> CreateToDb = new()
    {
        [MaterialCreateRequest.MaterialTypeEnum.RawMaterialEnum] = "原材料",
        [MaterialCreateRequest.MaterialTypeEnum.SemiFinishedEnum] = "半成品",
        [MaterialCreateRequest.MaterialTypeEnum.FinishedEnum] = "成品",
        [MaterialCreateRequest.MaterialTypeEnum.AuxiliaryEnum] = "辅料",
    };

    private static readonly Dictionary<MaterialUpdateRequest.MaterialTypeEnum, string> UpdateToDb = new()
    {
        [MaterialUpdateRequest.MaterialTypeEnum.RawMaterialEnum] = "原材料",
        [MaterialUpdateRequest.MaterialTypeEnum.SemiFinishedEnum] = "半成品",
        [MaterialUpdateRequest.MaterialTypeEnum.FinishedEnum] = "成品",
        [MaterialUpdateRequest.MaterialTypeEnum.AuxiliaryEnum] = "辅料",
    };

    private static readonly Dictionary<string, string> ApiToDb = new(StringComparer.OrdinalIgnoreCase)
    {
        ["raw_material"] = "原材料",
        ["semi_finished"] = "半成品",
        ["finished"] = "成品",
        ["auxiliary"] = "辅料",
        ["原材料"] = "原材料",
        ["半成品"] = "半成品",
        ["成品"] = "成品",
        ["辅料"] = "辅料",
    };

    public static MaterialDetail.MaterialTypeEnum FromDb(string dbType) =>
        DbToDetail.TryGetValue(dbType, out var value) ? value : MaterialDetail.MaterialTypeEnum.RawMaterialEnum;

    public static string ToDb(MaterialCreateRequest.MaterialTypeEnum type) => CreateToDb[type];

    public static string ToDb(MaterialUpdateRequest.MaterialTypeEnum type) => UpdateToDb[type];

    public static bool IsDefined(MaterialCreateRequest.MaterialTypeEnum type) => CreateToDb.ContainsKey(type);

    public static bool IsDefined(MaterialUpdateRequest.MaterialTypeEnum type) => UpdateToDb.ContainsKey(type);

    public static string? ToDbOrNull(string? apiType) =>
        !string.IsNullOrWhiteSpace(apiType) && ApiToDb.TryGetValue(apiType.Trim(), out var dbType) ? dbType : null;
}
