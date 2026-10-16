using ArchiveManagement.Domain.Entities;
using FreeSql;

namespace ArchiveManagement.Infrastructure;

/// <summary>首次启动种子数据：全宗、3 门类模板、默认角色、演示用户</summary>
public static class DbSeeder
{
    public static void Seed(IFreeSql db)
    {
        if (db.Select<Fonds>().Count() == 0)
            db.Insert(new Fonds { Code = "F1", Name = "示例全宗" }).ExecuteAffrows();

        if (db.Select<ArchiveCategory>().Count() == 0)
        {
            var cats = new[]
            {
                new ArchiveCategory
                {
                    Code = "DZ", Name = "党政",
                    // 案卷级
                    TemplateJson = "[{\"key\":\"filing_org\",\"label\":\"立卷单位\",\"type\":\"text\"},{\"key\":\"date_range\",\"label\":\"起止日期\",\"type\":\"text\"},{\"key\":\"item_count\",\"label\":\"卷内件数\",\"type\":\"number\"}]",
                    // 卷内文件级
                    FileTemplateJson = "[{\"key\":\"file_no\",\"label\":\"文号\",\"type\":\"text\"},{\"key\":\"send_org\",\"label\":\"发文机关\",\"type\":\"text\"},{\"key\":\"main_send\",\"label\":\"主送\",\"type\":\"text\"}]",
                    FkRule = "{fonds}-DZ-{year}-{seq}", DefaultRetention = "30年"
                },
                new ArchiveCategory
                {
                    Code = "KY", Name = "科研",
                    // 案卷级
                    TemplateJson = "[{\"key\":\"project_no\",\"label\":\"项目编号\",\"type\":\"text\",\"required\":true},{\"key\":\"project_name\",\"label\":\"课题名称\",\"type\":\"text\",\"required\":true},{\"key\":\"leader\",\"label\":\"负责人\",\"type\":\"text\"},{\"key\":\"result_type\",\"label\":\"成果形式\",\"type\":\"select\",\"enum\":[\"论文\",\"专利\",\"软件\",\"报告\"]}]",
                    // 卷内文件级
                    FileTemplateJson = "[{\"key\":\"doc_name\",\"label\":\"文件题名\",\"type\":\"text\",\"required\":true},{\"key\":\"author\",\"label\":\"责任者\",\"type\":\"text\"},{\"key\":\"publish_date\",\"label\":\"形成日期\",\"type\":\"date\"},{\"key\":\"page_no\",\"label\":\"页号\",\"type\":\"text\"}]",
                    FkRule = "{fonds}-KY-{year}-{seq}", DefaultRetention = "30年"
                },
                new ArchiveCategory
                {
                    Code = "JJ", Name = "基建",
                    // 案卷级
                    TemplateJson = "[{\"key\":\"project\",\"label\":\"工程项目\",\"type\":\"text\"},{\"key\":\"building\",\"label\":\"单体\",\"type\":\"text\"},{\"key\":\"stage\",\"label\":\"阶段\",\"type\":\"select\",\"enum\":[\"前期\",\"施工\",\"竣工\"]}]",
                    // 卷内文件级
                    FileTemplateJson = "[{\"key\":\"drawing_no\",\"label\":\"图号\",\"type\":\"text\"},{\"key\":\"specialty\",\"label\":\"专业\",\"type\":\"select\",\"enum\":[\"建筑\",\"结构\",\"给排水\",\"电气\",\"暖通\"]},{\"key\":\"prepared_by\",\"label\":\"编制单位\",\"type\":\"text\"}]",
                    FkRule = "{fonds}-JJ-{year}-{seq}", DefaultRetention = "永久"
                }
            };
            db.Insert(cats).ExecuteAffrows();
        }

        // 兼容老库：新增「文件级模板」列后，为空的门类默认沿用其案卷级模板作起点（幂等，可随时在门类配置里改）
        var noFileTpl = db.Select<ArchiveCategory>()
            .Where(c => c.FileTemplateJson == null || c.FileTemplateJson == "" || c.FileTemplateJson == "[]")
            .ToList();
        if (noFileTpl.Count > 0)
        {
            foreach (var c in noFileTpl)
            {
                c.FileTemplateJson = string.IsNullOrWhiteSpace(c.TemplateJson) ? "[]" : c.TemplateJson;
                db.Update<ArchiveCategory>().SetSource(c).ExecuteAffrows();
            }
        }

        if (db.Select<SysRole>().Count() == 0)
        {
            db.Insert(new[]
            {
                new SysRole { Code = "SYS_ADMIN", Name = "系统管理员" },
                new SysRole { Code = "ARCHIVIST", Name = "档案管理员" },
                new SysRole { Code = "USER", Name = "利用者" }
            }).ExecuteAffrows();

            SecurityHelper.HashPassword("admin123", out var h, out var s);
            db.Insert(new SysUser { Username = "admin", Name = "系统管理员", PasswordHash = h, Salt = s }).ExecuteAffrows();

            var admin = db.Select<SysUser>().Where(u => u.Username == "admin").First();
            var role = db.Select<SysRole>().Where(r => r.Code == "SYS_ADMIN").First();
            if (admin != null && role != null)
                db.Insert(new SysUserRole { UserId = admin.Id, RoleId = role.Id }).ExecuteAffrows();
        }
    }
}
