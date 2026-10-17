using ArchiveManagement.Api;
using ArchiveManagement.Application.Services;
using ArchiveManagement.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Events;
using System;
using System.IO;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// ===== 结构化日志：控制台 + 按天滚动文件（等保要求日志留存）=====
var logDir = builder.Configuration["Logging:FileDir"]
    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "ArchiveManagement", "logs");
try { Directory.CreateDirectory(logDir); } catch { logDir = Path.Combine(Path.GetTempPath(), "ArchiveManagement", "logs"); Directory.CreateDirectory(logDir); }
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(Path.Combine(logDir, "app-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 90,
        encoding: System.Text.Encoding.UTF8)
    .CreateLogger();
builder.Host.UseSerilog();
Log.Information("日志文件目录：{LogDir}", logDir);

// FreeSql + 对象存储
builder.Services.AddArchiveDb(builder.Configuration);

// 应用服务
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IArchiveService, ArchiveService>();
builder.Services.AddScoped<IFileItemService, FileItemService>();
builder.Services.AddScoped<IAttachmentService, AttachmentService>();
builder.Services.AddScoped<ISearchService, SearchService>();
builder.Services.AddScoped<IBorrowService, BorrowService>();
builder.Services.AddScoped<IAppraisalService, AppraisalService>();
builder.Services.AddScoped<IStatsService, StatsService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<ISystemService, SystemService>();
builder.Services.AddScoped<IFondsService, FondsService>();
// System.Drawing 水印仅 Windows 可用（部署目标为 Windows）；非 Windows 时服务内部自动跳过水印
#pragma warning disable CA1416
builder.Services.AddScoped<IWatermarkService, WatermarkService>();
#pragma warning restore CA1416
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<IAuditLogger, AuditLogger>();
// 审计日志需要取当前操作人与 IP（Application 层抽象，Api 层提供 HTTP 实现）
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddScoped<IAuthService, AuthService>();

// JWT 认证：密钥优先取环境变量 Jwt__Key，禁止在生产沿用默认/弱密钥
var jwtKey = builder.Configuration["Jwt:Key"] ?? string.Empty;
var weakKey = string.IsNullOrWhiteSpace(jwtKey)
              || jwtKey.Length < 32
              || jwtKey.Contains("change-me", StringComparison.OrdinalIgnoreCase)
              || jwtKey.Contains("dev-secret", StringComparison.OrdinalIgnoreCase);
if (weakKey)
{
    var msg = "Jwt:Key 缺失或为弱密钥（长度需 ≥32 且不能是示例值）。请通过环境变量 Jwt__Key 或配置注入强随机密钥。";
    if (builder.Environment.IsDevelopment() || builder.Environment.IsStaging())
        Log.Logger.Warning("[安全] {Msg} 当前为 {Env} 环境，已降级启动但禁止用于生产。", msg, builder.Environment.EnvironmentName);
    else
        throw new InvalidOperationException($"[安全] {msg} 生产环境已阻止启动。");
    jwtKey = string.IsNullOrWhiteSpace(jwtKey) ? "dev-secret-key-change-me-which-is-long-enough" : jwtKey;
}
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });
builder.Services.AddAuthorization();

// 枚举统一以字符串输出（Draft/Approved/...），与前端类型定义一致；
// 反序列化时同样接受字符串，保证前后端契约对称。
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "档案管理系统 API",
        Version = "v1",
        Description = "档案管理（门类/档案/文件/附件）Web API"
    });
});

// 开发期允许前端(5173)跨域
builder.Services.AddCors(o => o.AddPolicy("dev",
    p => p.WithOrigins("http://localhost:5173").AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

var app = builder.Build();

// 全局异常捕获：开发期把真实异常（含堆栈）直接返回给前端/控制台，避免只看到无信息的 500
app.Use(async (ctx, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[GlobalException] {ex}");
        Console.ResetColor();
        if (!ctx.Response.HasStarted)
        {
            ctx.Response.StatusCode = 500;
            ctx.Response.ContentType = "application/json; charset=utf-8";
            var detail = app.Environment.IsDevelopment()
                ? $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"
                : ex.Message;
            await ctx.Response.WriteAsJsonAsync(new { code = 500, msg = detail });
        }
    }
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("dev");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
