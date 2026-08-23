namespace Backend.Services;

/// <summary>
/// 输入长度校验助手。
/// 契约尚未给 create/register 请求字段声明 maxLength，生成的模型也没有 MaxLength 验证特性，
/// 超长字符串会直接触发 ORA-12899（值过大）导致 HTTP 500；这里在服务层统一拦截，
/// 返回业务错误信息。字段长度与 database/01_schema_forklift.sql 中的列定义保持一致。
/// </summary>
internal static class InputGuard
{
    /// <summary>
    /// 校验字符串长度。value 为 null 时跳过（required 字段由模型验证保证非空，可空字段允许省略）。
    /// 返回错误信息；通过时返回 null。
    /// </summary>
    public static string? CheckMaxLength(string? value, int maxLength, string fieldName)
    {
        if (value is not null && value.Length > maxLength)
        {
            return $"{fieldName}长度不能超过 {maxLength} 个字符";
        }

        return null;
    }
}
