using Backend.Filters;
using Backend.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Org.OpenAPITools.Models;

namespace Backend.Controllers;

[ApiController]
[Authorize]
[Route("/api")]
public class MaterialController(
    MaterialCatalogService materialCatalogService,
    AuthorizationService authorization) : ControllerBase
{
    [HttpGet]
    [Produces("application/json")]
    [Route("listMaterialCategoryData")]
    public IActionResult ListCategories(
        [FromQuery(Name = "page")] int? page,
        [FromQuery(Name = "page_size")] int? pageSize,
        [FromQuery(Name = "category_name")] string? categoryName)
    {
        if (ResolveCategoryPageReaderOrForbidden() is { } forbidden)
        {
            return forbidden;
        }

        var (currentPage, size) = Paging.Normalize(page, pageSize);
        var (records, total) = materialCatalogService.ListCategories(currentPage, size, categoryName);
        return Ok(new MaterialCategoryPageResponse
        {
            Code = MaterialCategoryPageResponse.CodeEnum._200Enum,
            Message = "查询成功",
            Data = new PageResult
            {
                Total = total,
                Page = currentPage,
                PageSize = size,
                Records = records.Cast<object>().ToList(),
            },
        });
    }

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("addMaterialCategoryData")]
    public IActionResult AddCategory([FromBody] MaterialCategoryCreateRequest? request)
    {
        if (ResolveCategoryManagerOrForbidden(PermissionCode.MaterialCategoryCreateEnum) is { } forbidden)
        {
            return forbidden;
        }

        if (request is null)
        {
            return Ok(Category(MaterialCategoryResponse.CodeEnum._400Enum, "请求体不能为空", null));
        }

        return FromCategoryResult(materialCatalogService.AddCategory(request), "创建成功");
    }

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("updateMaterialCategoryData")]
    public IActionResult UpdateCategory([FromBody] MaterialCategoryUpdateRequest? request)
    {
        if (ResolveCategoryManagerOrForbidden(PermissionCode.MaterialCategoryUpdateEnum) is { } forbidden)
        {
            return forbidden;
        }

        if (request is null)
        {
            return Ok(Category(MaterialCategoryResponse.CodeEnum._400Enum, "请求体不能为空", null));
        }

        return FromCategoryResult(materialCatalogService.UpdateCategory(request), "修改成功");
    }

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("deleteMaterialCategoryData")]
    public IActionResult DeleteCategory([FromBody] MaterialCategoryDeleteRequest? request)
    {
        if (ResolveCommonManagerOrForbidden(PermissionCode.MaterialCategoryDeleteEnum) is { } forbidden)
        {
            return forbidden;
        }

        if (request is null)
        {
            return Ok(Common(ApiResponse.CodeEnum._400Enum, "请求体不能为空"));
        }

        var result = materialCatalogService.DeleteCategory(request.CategoryId);
        return result.Ok
            ? Ok(Common(ApiResponse.CodeEnum._200Enum, "删除成功"))
            : Ok(Common((ApiResponse.CodeEnum)(int)result.Error, result.ErrorMessage ?? "删除失败"));
    }

    [HttpGet]
    [Produces("application/json")]
    [Route("listMaterialData")]
    public IActionResult ListMaterials(
        [FromQuery(Name = "page")] int? page,
        [FromQuery(Name = "page_size")] int? pageSize,
        [FromQuery(Name = "material_id")] long? materialId,
        [FromQuery(Name = "material_name")] string? materialName,
        [FromQuery(Name = "material_type")] string? materialType,
        [FromQuery(Name = "category_id")] long? categoryId,
        [FromQuery(Name = "default_supplier_id")] long? defaultSupplierId,
        [FromQuery(Name = "min_safety_stock")] decimal? minSafetyStock,
        [FromQuery(Name = "max_safety_stock")] decimal? maxSafetyStock,
        [FromQuery(Name = "created_start_time")] DateTime? createdStartTime,
        [FromQuery(Name = "created_end_time")] DateTime? createdEndTime,
        [FromQuery(Name = "updated_start_time")] DateTime? updatedStartTime,
        [FromQuery(Name = "updated_end_time")] DateTime? updatedEndTime)
    {
        if (ResolveMaterialPageReaderOrForbidden() is { } forbidden)
        {
            return forbidden;
        }

        var (currentPage, size) = Paging.Normalize(page, pageSize);
        var (records, total) = materialCatalogService.ListMaterials(
            currentPage,
            size,
            materialId,
            materialName,
            materialType,
            categoryId,
            defaultSupplierId,
            minSafetyStock,
            maxSafetyStock,
            createdStartTime,
            createdEndTime,
            updatedStartTime,
            updatedEndTime);

        return Ok(new MaterialPageResponse
        {
            Code = MaterialPageResponse.CodeEnum._200Enum,
            Message = "查询成功",
            Data = new PageResult
            {
                Total = total,
                Page = currentPage,
                PageSize = size,
                Records = records.Cast<object>().ToList(),
            },
        });
    }

    [HttpGet]
    [Produces("application/json")]
    [Route("getMaterialData")]
    public IActionResult GetMaterial([FromQuery(Name = "material_id")] long materialId)
    {
        if (ResolveMaterialReaderOrForbidden() is { } forbidden)
        {
            return forbidden;
        }

        var material = materialCatalogService.GetMaterial(materialId);
        return material is null
            ? Ok(Material(MaterialResponse.CodeEnum._404Enum, "物料不存在", null))
            : Ok(Material(MaterialResponse.CodeEnum._200Enum, "查询成功", material));
    }

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("addMaterialData")]
    [RequireJsonFields("material_type")]
    public IActionResult AddMaterial([FromBody] MaterialCreateRequest? request)
    {
        var user = ResolveManager(PermissionCode.MaterialItemCreateEnum);
        if (user.Response is not null)
        {
            return user.Response;
        }

        if (request is null)
        {
            return Ok(Material(MaterialResponse.CodeEnum._400Enum, "请求体不能为空", null));
        }

        return FromMaterialResult(materialCatalogService.AddMaterial(request, user.User!.UserId), "创建成功");
    }

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("updateMaterialData")]
    [RequireJsonFields("material_type")]
    public IActionResult UpdateMaterial([FromBody] MaterialUpdateRequest? request)
    {
        if (ResolveMaterialManagerOrForbidden(PermissionCode.MaterialItemUpdateEnum) is { } forbidden)
        {
            return forbidden;
        }

        if (request is null)
        {
            return Ok(Material(MaterialResponse.CodeEnum._400Enum, "请求体不能为空", null));
        }

        return FromMaterialResult(materialCatalogService.UpdateMaterial(request), "修改成功");
    }

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("deleteMaterialData")]
    public IActionResult DeleteMaterial([FromBody] MaterialDeleteRequest? request)
    {
        if (ResolveCommonManagerOrForbidden(PermissionCode.MaterialItemDeleteEnum) is { } forbidden)
        {
            return forbidden;
        }

        if (request is null)
        {
            return Ok(Common(ApiResponse.CodeEnum._400Enum, "请求体不能为空"));
        }

        var result = materialCatalogService.DeleteMaterial(request.MaterialId);
        return result.Ok
            ? Ok(Common(ApiResponse.CodeEnum._200Enum, "删除成功"))
            : Ok(Common((ApiResponse.CodeEnum)(int)result.Error, result.ErrorMessage ?? "删除失败"));
    }

    private IActionResult? ResolveMaterialReaderOrForbidden()
    {
        AuthResult result = Authorize(PermissionCode.MaterialItemViewEnum);
        return result.Ok
            ? null
            : Ok(Material((MaterialResponse.CodeEnum)result.Code, result.Message ?? "无权访问物料主数据", null));
    }

    private IActionResult? ResolveMaterialPageReaderOrForbidden()
    {
        AuthResult result = Authorize(PermissionCode.MaterialItemViewEnum);
        return result.Ok
            ? null
            : Ok(new MaterialPageResponse
            {
                Code = (MaterialPageResponse.CodeEnum)result.Code,
                Message = result.Message ?? "无权访问物料主数据",
                Data = null!,
            });
    }

    private IActionResult? ResolveCategoryPageReaderOrForbidden()
    {
        AuthResult result = Authorize(PermissionCode.MaterialCategoryViewEnum);
        return result.Ok
            ? null
            : Ok(new MaterialCategoryPageResponse
            {
                Code = (MaterialCategoryPageResponse.CodeEnum)result.Code,
                Message = result.Message ?? "无权访问物料分类",
                Data = null!,
            });
    }

    private IActionResult? ResolveMaterialManagerOrForbidden(PermissionCode permissionCode) =>
        ResolveManager(permissionCode).Response;

    private IActionResult? ResolveCategoryManagerOrForbidden(PermissionCode permissionCode)
    {
        AuthResult result = Authorize(permissionCode);
        return result.Ok
            ? null
            : Ok(Category((MaterialCategoryResponse.CodeEnum)result.Code, result.Message ?? "无权维护物料分类", null));
    }

    private IActionResult? ResolveCommonManagerOrForbidden(PermissionCode permissionCode)
    {
        AuthResult result = Authorize(permissionCode);
        return result.Ok
            ? null
            : Ok(Common((ApiResponse.CodeEnum)result.Code, result.Message ?? "无权维护物料主数据"));
    }

    private (CurrentUser? User, IActionResult? Response) ResolveManager(PermissionCode permissionCode)
    {
        AuthResult result = Authorize(permissionCode);
        return result.Ok
            ? (result.User, null)
            : (null, Ok(Material((MaterialResponse.CodeEnum)result.Code, result.Message ?? "无权维护物料主数据", null)));
    }

    private AuthResult Authorize(PermissionCode permissionCode) =>
        authorization.RequirePermission(User.GetEmployeeNo(), permissionCode);

    private static IActionResult FromCategoryResult(MaterialCatalogResult<MaterialCategory> result, string successMessage)
    {
        if (result.Ok)
        {
            return new OkObjectResult(Category(MaterialCategoryResponse.CodeEnum._200Enum, successMessage, result.Data));
        }

        return new OkObjectResult(Category(
            (MaterialCategoryResponse.CodeEnum)(int)result.Error,
            result.ErrorMessage ?? "操作失败",
            null));
    }

    private static IActionResult FromMaterialResult(MaterialCatalogResult<MaterialDetail> result, string successMessage)
    {
        if (result.Ok)
        {
            return new OkObjectResult(Material(MaterialResponse.CodeEnum._200Enum, successMessage, result.Data));
        }

        return new OkObjectResult(Material(
            (MaterialResponse.CodeEnum)(int)result.Error,
            result.ErrorMessage ?? "操作失败",
            null));
    }

    private static MaterialCategoryResponse Category(
        MaterialCategoryResponse.CodeEnum code,
        string message,
        MaterialCategory? data) => new()
        {
            Code = code,
            Message = message,
            Data = data!,
        };

    private static MaterialResponse Material(
        MaterialResponse.CodeEnum code,
        string message,
        MaterialDetail? data) => new()
        {
            Code = code,
            Message = message,
            Data = data!,
        };

    private static ApiResponse Common(ApiResponse.CodeEnum code, string message) => new()
    {
        Code = code,
        Message = message,
        Data = null!,
    };
}
