using ArchiveManagement.Application.Services;
using ArchiveManagement.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddScoped<IAuthService, AuthService>();

// JWT 认证
var jwtKey = builder.Configuration["Jwt:Key"] ?? "dev-secret-key-change-me-which-is-long-enough";
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

builder.Services.AddControllers();
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
