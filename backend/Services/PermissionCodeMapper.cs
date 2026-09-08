using System.Reflection;
using System.Runtime.Serialization;

using Org.OpenAPITools.Models;

namespace Backend.Services;

/// <summary>在数据库权限码和 OpenAPI 生成的权限枚举之间转换。</summary>
public static class PermissionCodeMapper
{
    private static readonly IReadOnlyDictionary<string, PermissionCode> ValuesByCode =
        Enum.GetValues<PermissionCode>().ToDictionary(
            ToContractValue,
            value => value,
            StringComparer.Ordinal);

    public static bool TryParse(string code, out PermissionCode permissionCode) =>
        ValuesByCode.TryGetValue(code, out permissionCode);

    public static string ToContractValue(this PermissionCode permissionCode)
    {
        MemberInfo member = typeof(PermissionCode).GetMember(permissionCode.ToString()).Single();
        return member.GetCustomAttribute<EnumMemberAttribute>()?.Value
            ?? throw new InvalidOperationException($"权限枚举 {permissionCode} 缺少 EnumMember 值");
    }
}
