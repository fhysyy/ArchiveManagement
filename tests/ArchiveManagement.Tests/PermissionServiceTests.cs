using ArchiveManagement.Application.Services;
using Xunit;

namespace ArchiveManagement.Tests;

/// <summary>
/// 五维权限（角色 × 门类 × 操作 × 密级）判定逻辑。
/// Can/CanSee 是纯函数，不依赖数据库，适合单元测试。
/// </summary>
public class PermissionServiceTests
{
    private readonly PermissionService _svc = new(db: null!);

    private static UserPermission Limited(params long[] categories) => new()
    {
        UserId = 9,
        RoleId = 2,
        RoleCode = "USER",
        IsAdmin = false,
        HasAnyPermission = true,
        AllowedCategoryIds = new HashSet<long>(categories),
        OpsByCategory = new()
        {
            [1] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "view", "create" }
        }
    };

    [Fact]
    public void Admin_AlwaysAllowed()
    {
        var p = new UserPermission { IsAdmin = true, HasAnyPermission = false };
        Assert.True(_svc.Can(p, 999, "delete"));
        Assert.True(_svc.CanSee(p, 999, "绝密"));
    }

    [Fact]
    public void RoleWithoutPermissionRows_IsUnrestricted()
    {
        // 种子角色没有权限行，必须保持"不限制"，否则上线即锁死
        var p = new UserPermission { HasAnyPermission = false };
        Assert.True(_svc.Can(p, 1, "view"));
        Assert.True(_svc.Can(p, 12345, "delete"));
    }

    [Fact]
    public void CategoryOutsideAllowed_IsDenied()
    {
        var p = Limited(1);
        Assert.False(_svc.Can(p, 2, "view"));
        Assert.False(_svc.CanSee(p, 2, "公开"));
    }

    [Fact]
    public void OperationMustBeGrantedOnThatCategory()
    {
        var p = Limited(1);
        Assert.True(_svc.Can(p, 1, "view"));
        Assert.True(_svc.Can(p, 1, "create"));
        Assert.False(_svc.Can(p, 1, "delete"));
        Assert.False(_svc.Can(p, 1, "audit"));
    }

    [Fact]
    public void OperationIsCaseInsensitive()
    {
        var p = Limited(1);
        Assert.True(_svc.Can(p, 1, "VIEW"));
    }

    [Fact]
    public void WildcardCategoryZero_GrantsOperationEverywhere()
    {
        var p = new UserPermission
        {
            HasAnyPermission = true,
            AllowedCategoryIds = null,
            OpsByCategory = new() { [0] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "view" } }
        };
        Assert.True(_svc.Can(p, 42, "view"));
        Assert.False(_svc.Can(p, 42, "delete"));
    }

    [Theory]
    [InlineData("公开", 0)]
    [InlineData("内部", 1)]
    [InlineData("秘密", 2)]
    [InlineData("机密", 3)]
    [InlineData("绝密", 4)]
    [InlineData(null, 0)]
    [InlineData("未知密级", 0)]
    public void SecrecyLevelMapping(string? secrecy, int expected)
        => Assert.Equal(expected, PermissionService.LevelOf(secrecy));

    [Fact]
    public void SecrecyAboveMax_IsHidden()
    {
        var p = Limited(1);
        p.MaxSecrecyLevel = 2; // 秘密
        Assert.True(_svc.CanSee(p, 1, "公开"));
        Assert.True(_svc.CanSee(p, 1, "秘密"));
        Assert.False(_svc.CanSee(p, 1, "机密"));
        Assert.False(_svc.CanSee(p, 1, "绝密"));
    }

    [Fact]
    public void NoSecrecyConfigured_DoesNotFilter()
    {
        var p = Limited(1); // MaxSecrecyLevel = 0
        Assert.True(_svc.CanSee(p, 1, "绝密"));
    }

    [Fact]
    public void CanSee_RequiresViewOperation()
    {
        var p = Limited(1);
        p.OpsByCategory[1] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "create" };
        Assert.False(_svc.CanSee(p, 1, "公开")); // 有 create 无 view
    }
}
