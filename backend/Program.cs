using System.Text;

using Backend.Filters;
using Backend.Services;
using Backend.Services.Interfaces;

using DotNetEnv;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

using Oracle.ManagedDataAccess.Client;

// 在创建任何数据库连接前统一启用按名称绑定。
OracleConfiguration.BindByName = true;

Env.Load();
var connString = Env.GetString("ORACLE_CONN");
var jwtSecret = Env.GetString("JWT_SECRET");

if (string.IsNullOrWhiteSpace(connString))
{
    throw new InvalidOperationException("错误：未在 .env 文件中找到 'ORACLE_CONN'。");
}

if (string.IsNullOrWhiteSpace(jwtSecret))
{
    jwtSecret = "db-design-local-jwt-secret-change-me-32-bytes";
}

var builder = WebApplication.CreateBuilder(args);
var jwtSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));

builder.Services
    .AddControllers(options => options.Filters.AddService<OperationAuditFilter>())
    .AddNewtonsoftJson(options =>
    {
        options.SerializerSettings.DateTimeZoneHandling = Newtonsoft.Json.DateTimeZoneHandling.Utc;
    });

// 契约约定：HTTP 固定 200，业务状态通过响应体 code 表达。
// [ApiController] 自动模型验证失败（缺 required 字段、枚举值非法等）默认返回 HTTP 400
// ProblemDetails，响应体结构与 {code, message, data} 不一致；这里统一改写为业务响应体。
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var message = string.Join("; ", context.ModelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage));
        if (string.IsNullOrWhiteSpace(message))
        {
            message = "请求参数不合法";
        }

        return new OkObjectResult(new
        {
            code = 400,
            message,
            data = (object?)null,
        });
    };
});

builder.Services.AddScoped(sp => new AuthService(connString, jwtSecret, sp.GetRequiredService<LoginLogService>()));
builder.Services.AddScoped<IUserTestService>(_ => new UserTestService(connString));
builder.Services.AddScoped(_ => new UserContextService(connString));
builder.Services.AddScoped(_ => new ProductionOrderMaterialLockService(connString));
builder.Services.AddScoped(sp => new ProductionOrderService(
    connString,
    sp.GetRequiredService<ProductionOrderMaterialLockService>()));
builder.Services.AddScoped(sp => new ProductionCompletionService(
    connString,
    sp.GetRequiredService<ILogger<ProductionCompletionService>>()));
builder.Services.AddScoped(sp => new ExternalOrderService(connString, sp.GetRequiredService<ILogger<ExternalOrderService>>()));
builder.Services.AddScoped(_ => new QualityTraceService(connString));
builder.Services.AddScoped<MaterialRequirementNettingService>();
builder.Services.AddScoped(sp => new InventoryService(
    connString,
    sp.GetRequiredService<MaterialRequirementNettingService>()));
builder.Services.AddScoped(sp => new PurchaseService(connString, sp.GetRequiredService<ILogger<PurchaseService>>()));
builder.Services.AddScoped<IStockOperationService>(sp => sp.GetRequiredService<InventoryService>());
builder.Services.AddScoped<IStockReadQuery>(_ => new StockQueryService(connString));
builder.Services.AddScoped<IStockInitialization>(_ => new StockQueryService(connString));
builder.Services.AddScoped<IPriceQuery>(_ => new PriceQueryService(connString));
builder.Services.AddScoped<AuthorizationService>();
builder.Services.AddScoped(_ => new UserService(connString));
builder.Services.AddScoped(_ => new RoleService(connString));
builder.Services.AddScoped(_ => new PermissionService(connString));
builder.Services.AddScoped(_ => new UserRoleService(connString));
builder.Services.AddScoped(_ => new RolePermissionService(connString));
builder.Services.AddScoped(_ => new LoginLogService(connString));
builder.Services.AddScoped(_ => new OperationLogService(connString));
builder.Services.AddScoped(_ => new OperationAuditSnapshotService(connString));
builder.Services.AddScoped<OperationAuditFilter>();
builder.Services.AddScoped(sp => new MaterialCatalogService(
    connString,
    sp.GetRequiredService<IStockReadQuery>(),
    sp.GetRequiredService<IStockInitialization>()));
builder.Services.AddScoped(_ => new BomVersionService(connString));
builder.Services.AddScoped(_ => new BomService(connString));
builder.Services.AddScoped<SupplierPriceIntegrationService>(_ => new SupplierPriceIntegrationService(connString));
builder.Services.AddScoped<IPriceQuery>(sp => sp.GetRequiredService<SupplierPriceIntegrationService>());
builder.Services.AddScoped<DemandAnalysisService>(sp => new DemandAnalysisService(
    connString,
    sp.GetRequiredService<IPriceQuery>()));
builder.Services.AddScoped<IBomExpansionQuery>(sp => sp.GetRequiredService<DemandAnalysisService>());
builder.Services.AddScoped(sp => new CapacityEstimationDataSource(
    connString, sp.GetRequiredService<MaterialRequirementNettingService>()));
builder.Services.AddScoped<IProductionLineService>(_ => new ProductionLineService(connString));
builder.Services.AddScoped<ICapacityService>(services => new CapacityService(
    connString,
    services.GetRequiredService<CapacityEstimationDataSource>()));
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ClockSkew = TimeSpan.Zero,
            IssuerSigningKey = jwtSigningKey,
            ValidateAudience = false,
            ValidateIssuer = false,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true
        };
        // 契约约定：HTTP 固定 200，业务状态通过响应体 code 表达。
        // 未携带/无效 JWT 或权限不足时，默认中间件会直接返回 HTTP 401/403，
        // 与契约不一致；这里统一改写为 HTTP 200 + 业务响应体。
        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status200OK;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new
                {
                    code = 401,
                    message = "未登录或登录已失效",
                    data = (object?)null,
                });
            },
            OnForbidden = async context =>
            {
                context.Response.StatusCode = StatusCodes.Status200OK;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new
                {
                    code = 403,
                    message = "没有权限访问该接口",
                    data = (object?)null,
                });
            },
        };
    });
builder.Services.AddAuthorization();

// Nginx 与后端部署在同一主机时，默认可信代理范围包含 IPv4/IPv6 回环地址。
// 处理转发头后，RemoteIpAddress 会恢复为发起请求的客户端地址。
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod()));

var app = builder.Build();

app.UseForwardedHeaders();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run("http://localhost:5000");
