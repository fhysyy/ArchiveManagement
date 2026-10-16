namespace ArchiveManagement.Application.Dtos;

public class CategoryDto
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Status { get; set; }
    public string TemplateJson { get; set; } = "[]";
    public string FileTemplateJson { get; set; } = "[]";
    public string? FkRule { get; set; }
    public string? DefaultRetention { get; set; }
}

public class CreateCategoryDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string TemplateJson { get; set; } = "[]";
    public string FileTemplateJson { get; set; } = "[]";
    public string? FkRule { get; set; }
    public string? DefaultRetention { get; set; }
}
