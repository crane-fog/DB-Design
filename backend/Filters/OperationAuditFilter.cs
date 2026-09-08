using System.Globalization;
using System.Reflection;

using Backend.Services;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Backend.Filters;

/// <summary>集中处理所有由 <see cref="OperationAuditAttribute"/> 标记的操作审计。</summary>
public sealed class OperationAuditFilter(
    OperationLogService operationLogService,
    OperationAuditSnapshotService snapshotService,
    UserContextService userContext,
    ILogger<OperationAuditFilter> logger) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        OperationAuditAttribute? audit = context.ActionDescriptor.EndpointMetadata
            .OfType<OperationAuditAttribute>()
            .LastOrDefault();
        if (audit is null)
        {
            await next();
            return;
        }

        CurrentUser? currentUser = userContext.Resolve(context.HttpContext.User.GetEmployeeNo());
        Dictionary<string, object>? beforeData = null;
        if (audit.SnapshotKind != OperationAuditSnapshotKind.None)
        {
            beforeData = TryCaptureSnapshot(audit, context.ActionArguments);
        }

        ActionExecutedContext executed = await next();
        if (currentUser is null
            || (executed.Exception is not null && !executed.ExceptionHandled)
            || !TryGetSuccessfulResponseData(executed.Result, out object? responseData))
        {
            return;
        }

        Dictionary<string, object>? afterData;
        if (audit.SnapshotKind == OperationAuditSnapshotKind.None)
        {
            afterData = new Dictionary<string, object>
            {
                ["input"] = GetInput(context.ActionArguments)!,
                ["response"] = responseData!,
            };
        }
        else
        {
            afterData = TryCaptureSnapshot(audit, context.ActionArguments);
        }

        try
        {
            operationLogService.Write(
                audit.Module,
                audit.Action,
                checked((int)currentUser.UserId),
                context.HttpContext.Connection.RemoteIpAddress?.ToString(),
                beforeData,
                afterData);
        }
        catch (Exception exception)
        {
            // 审计写入使用独立连接，失败时保留原业务响应。
            logger.LogError(
                exception,
                "写入操作日志失败：{Module}/{Action}",
                audit.Module,
                audit.Action);
        }
    }

    private Dictionary<string, object>? TryCaptureSnapshot(
        OperationAuditAttribute audit,
        IDictionary<string, object?> actionArguments)
    {
        try
        {
            return snapshotService.Capture(audit.SnapshotKind, actionArguments);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "读取操作审计快照失败：{Module}/{Action}/{SnapshotKind}",
                audit.Module,
                audit.Action,
                audit.SnapshotKind);
            return null;
        }
    }

    private static object? GetInput(IDictionary<string, object?> actionArguments)
    {
        if (actionArguments.Count == 1)
        {
            return actionArguments.Values.Single();
        }

        return actionArguments.ToDictionary(pair => pair.Key, pair => pair.Value);
    }

    private static bool TryGetSuccessfulResponseData(IActionResult? result, out object? responseData)
    {
        responseData = null;
        if (result is not ObjectResult { Value: { } response })
        {
            return false;
        }

        PropertyInfo? codeProperty = response.GetType().GetProperty("Code");
        if (codeProperty?.GetValue(response) is not { } codeValue
            || Convert.ToInt32(codeValue, CultureInfo.InvariantCulture) != StatusCodes.Status200OK)
        {
            return false;
        }

        responseData = response.GetType().GetProperty("Data")?.GetValue(response);
        return true;
    }
}
