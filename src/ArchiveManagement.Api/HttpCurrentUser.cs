using ArchiveManagement.Application.Services;
using Microsoft.AspNetCore.Http;

namespace ArchiveManagement.Api;

/// <summary>从 HTTP 上下文解析当前操作人与客户端 IP</summary>
public class HttpCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _http;
    public HttpCurrentUser(IHttpContextAccessor http) => _http = http;

    public string? UserName => _http.HttpContext?.User?.Identity?.Name;

    public string? IpAddress
    {
        get
        {
            var ctx = _http.HttpContext;
            if (ctx == null) return null;
            // 反向代理场景优先取 X-Forwarded-For
            var fwd = ctx.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(fwd)) return fwd.Split(',')[0].Trim();
            return ctx.Connection?.RemoteIpAddress?.ToString();
        }
    }
}
