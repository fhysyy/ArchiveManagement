using Microsoft.Extensions.Configuration;

namespace ArchiveManagement.Application.Services;

public interface IWatermarkService
{
    /// <summary>是否启用水印（配置 Storage:Watermark，默认开启）</summary>
    bool Enabled { get; }

    /// <summary>该扩展名能否加水印（目前支持位图类）</summary>
    bool Supported(string? fileName);

    /// <summary>
    /// 在图片上叠加平铺斜向水印（半透明），用于下载/预览时防截屏外传。
    /// 非 Windows 或不支持的类型原样返回。
    /// </summary>
    byte[] Apply(byte[] bytes, string? fileName, IEnumerable<string> lines);
}

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public class WatermarkService : IWatermarkService
{
    private readonly IConfiguration _cfg;
    public WatermarkService(IConfiguration cfg) => _cfg = cfg;

    public bool Enabled => !string.Equals(_cfg["Storage:Watermark"], "false", StringComparison.OrdinalIgnoreCase);

    public bool Supported(string? fileName)
    {
        var ext = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
        return ext is ".png" or ".jpg" or ".jpeg" or ".bmp";
    }

    public byte[] Apply(byte[] bytes, string? fileName, IEnumerable<string> lines)
    {
        // System.Drawing 仅在 Windows 受支持（本项目部署目标为 Windows / 政务云 Windows 主机）
        if (!Enabled || !Supported(fileName) || !OperatingSystem.IsWindows()) return bytes;
        var texts = (lines ?? Array.Empty<string>()).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        if (texts.Count == 0) return bytes;

        try
        {
            using var src = new MemoryStream(bytes);
            using var bmp = new System.Drawing.Bitmap(src);
            using var g = System.Drawing.Graphics.FromImage(bmp);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            float fontSize = Math.Max(14f, Math.Min(bmp.Width, bmp.Height) / 22f);
            using var font = new System.Drawing.Font("Microsoft YaHei", fontSize, System.Drawing.FontStyle.Bold);
            using var brush = new System.Drawing.SolidBrush(
                System.Drawing.Color.FromArgb(72, 255, 255, 255)); // 半透明白，深色底也能看清
            using var shadow = new System.Drawing.SolidBrush(
                System.Drawing.Color.FromArgb(48, 0, 0, 0));

            var text = string.Join("   ", texts);
            var size = g.MeasureString(text, font);
            double stepX = size.Width + Math.Max(60, bmp.Width * 0.12);
            double stepY = size.Height * 3.2;

            for (double y = -bmp.Height * 0.2; y < bmp.Height * 1.2; y += stepY)
            {
                for (double x = -bmp.Width * 0.2; x < bmp.Width * 1.2; x += stepX)
                {
                    g.TranslateTransform((float)x, (float)y);
                    g.RotateTransform(-28);
                    g.DrawString(text, font, shadow, 2f, 2f);
                    g.DrawString(text, font, brush, 0f, 0f);
                    g.RotateTransform(28);
                    g.TranslateTransform((float)-x, (float)-y);
                }
            }

            using var outMs = new MemoryStream();
            var ext = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
            switch (ext)
            {
                case ".png": bmp.Save(outMs, System.Drawing.Imaging.ImageFormat.Png); break;
                case ".bmp": bmp.Save(outMs, System.Drawing.Imaging.ImageFormat.Bmp); break;
                default: bmp.Save(outMs, System.Drawing.Imaging.ImageFormat.Jpeg); break;
            }
            return outMs.ToArray();
        }
        catch
        {
            // 水印失败不能阻断下载，返回原文件
            return bytes;
        }
    }
}
