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
    UserContextService userContext) : ControllerBase
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
        if (ResolveCategoryManagerOrForbidden() is { } forbidden)
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
        if (ResolveCategoryManagerOrForbidden() is { } forbidden)
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
        if (ResolveCommonManagerOrForbidden() is { } forbidden)
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
    public IActionResult AddMaterial([FromBody] MaterialCreateRequest? request)
    {
        var user = ResolveManager();
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
    public IActionResult UpdateMaterial([FromBody] MaterialUpdateRequest? request)
    {
        if (ResolveMaterialManagerOrForbidden() is { } forbidden)
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
        if (ResolveCommonManagerOrForbidden() is { } forbidden)
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
        var user = userContext.Resolve(User.GetEmployeeNo());
        if (user is null)
        {
            return Ok(Material(MaterialResponse.CodeEnum._401Enum, "登录状态无效", null));
        }

        if (!user.IsMaterialReader)
        {
            return Ok(Material(MaterialResponse.CodeEnum._403Enum, "无权访问物料主数据", null));
        }

        return null;
    }

    private IActionResult? ResolveMaterialPageReaderOrForbidden()
    {
        var user = userContext.Resolve(User.GetEmployeeNo());
        if (user is null)
        {
            return Ok(new MaterialPageResponse
            {
                Code = MaterialPageResponse.CodeEnum._401Enum,
                Message = "登录状态无效",
                Data = null!,
            });
        }

        if (!user.IsMaterialReader)
        {
            return Ok(new MaterialPageResponse
            {
                Code = MaterialPageResponse.CodeEnum._403Enum,
                Message = "无权访问物料主数据",
                Data = null!,
            });
        }

        return null;
    }

    private IActionResult? ResolveCategoryPageReaderOrForbidden()
    {
        var user = userContext.Resolve(User.GetEmployeeNo());
        if (user is null)
        {
            return Ok(new MaterialCategoryPageResponse
            {
                Code = MaterialCategoryPageResponse.CodeEnum._401Enum,
                Message = "登录状态无效",
                Data = null!,
            });
        }

        if (!user.IsMaterialReader)
        {
            return Ok(new MaterialCategoryPageResponse
            {
                Code = MaterialCategoryPageResponse.CodeEnum._403Enum,
                Message = "无权访问物料主数据",
                Data = null!,
            });
        }

        return null;
    }

    private IActionResult? ResolveMaterialManagerOrForbidden() => ResolveManager().Response;

    private IActionResult? ResolveCategoryManagerOrForbidden()
    {
        var user = userContext.Resolve(User.GetEmployeeNo());
        if (user is null)
        {
            return Ok(Category(MaterialCategoryResponse.CodeEnum._401Enum, "登录状态无效", null));
        }

        if (!user.IsMaterialManager)
        {
            return Ok(Category(MaterialCategoryResponse.CodeEnum._403Enum, "无权维护物料主数据", null));
        }

        return null;
    }

    private IActionResult? ResolveCommonManagerOrForbidden()
    {
        var user = userContext.Resolve(User.GetEmployeeNo());
        if (user is null)
        {
            return Ok(Common(ApiResponse.CodeEnum._401Enum, "登录状态无效"));
        }

        if (!user.IsMaterialManager)
        {
            return Ok(Common(ApiResponse.CodeEnum._403Enum, "无权维护物料主数据"));
        }

        return null;
    }

    private (CurrentUser? User, IActionResult? Response) ResolveManager()
    {
        var user = userContext.Resolve(User.GetEmployeeNo());
        if (user is null)
        {
            return (null, Ok(Material(MaterialResponse.CodeEnum._401Enum, "登录状态无效", null)));
        }

        if (!user.IsMaterialManager)
        {
            return (null, Ok(Material(MaterialResponse.CodeEnum._403Enum, "无权维护物料主数据", null)));
        }

        return (user, null);
    }

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
