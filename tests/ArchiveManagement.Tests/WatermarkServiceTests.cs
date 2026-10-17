using ArchiveManagement.Application.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ArchiveManagement.Tests;

/// <summary>服务端图片水印：类型白名单、开关、以及"确实改写像素"的冒烟验证。</summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public class WatermarkServiceTests
{
    private static WatermarkService Create(string? enabled = null)
    {
        var dict = new Dictionary<string, string?>();
        if (enabled != null) dict["Storage:Watermark"] = enabled;
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
        return new WatermarkService(cfg);
    }

    [Theory]
    [InlineData("a.png", true)]
    [InlineData("a.JPG", true)]
    [InlineData("a.jpeg", true)]
    [InlineData("a.bmp", true)]
    [InlineData("a.pdf", false)]
    [InlineData("a.docx", false)]
    [InlineData(null, false)]
    public void SupportedTypes(string? name, bool expected)
        => Assert.Equal(expected, Create().Supported(name));

    [Fact]
    public void EnabledByDefault_AndCanBeTurnedOff()
    {
        Assert.True(Create().Enabled);
        Assert.False(Create("false").Enabled);
    }

    [Fact]
    public void Disabled_ReturnsOriginalBytes()
    {
        var bytes = TinyPng();
        var result = Create("false").Apply(bytes, "x.png", new[] { "tester" });
        Assert.Same(bytes, result);
    }

    [Fact]
    public void UnsupportedType_ReturnsOriginalBytes()
    {
        var bytes = new byte[] { 1, 2, 3 };
        Assert.Same(bytes, Create().Apply(bytes, "x.pdf", new[] { "tester" }));
    }

    [Fact]
    public void Png_IsRewrittenWithWatermark()
    {
        var bytes = TinyPng();
        var result = Create().Apply(bytes, "x.png", new[] { "tester", "2026-01-01 10:00" });
        Assert.NotEqual(bytes.Length, result.Length);
        // PNG 魔数保持不变，说明仍是可解码的图片
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, result.Take(4).ToArray());
    }

    /// <summary>1x1 透明 PNG（最小合法图片）</summary>
    private static byte[] TinyPng() => Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");
}
