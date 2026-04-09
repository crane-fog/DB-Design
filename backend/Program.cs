using Backend.Services;

using DotNetEnv;

Env.Load();
var connString = Env.GetString("ORACLE_CONN");

if (string.IsNullOrWhiteSpace(connString))
{
    throw new InvalidOperationException("错误：未在 .env 文件中找到 'ORACLE_CONN'。");
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddScoped<IUserTestService>(_ => new UserTestService(connString));

builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod()));

var app = builder.Build();

app.UseCors();

app.MapControllers();

app.Run("http://localhost:5000");
