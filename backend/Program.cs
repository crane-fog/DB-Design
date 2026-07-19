using System.Text;

using Backend.Services;

using DotNetEnv;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

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

builder.Services.AddControllers().AddNewtonsoftJson();

builder.Services.AddScoped(_ => new AuthService(connString, jwtSecret));
builder.Services.AddScoped<IUserTestService>(_ => new UserTestService(connString));
builder.Services.AddScoped(_ => new UserContextService(connString));
builder.Services.AddScoped(_ => new ProductionOrderService(connString));
builder.Services.AddScoped(_ => new ExternalOrderService(connString));
builder.Services.AddScoped(_ => new QualityTraceService(connString));
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
    });
builder.Services.AddAuthorization();

builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod()));

var app = builder.Build();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run("http://localhost:5000");
