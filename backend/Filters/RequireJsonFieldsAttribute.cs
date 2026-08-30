using System.Text;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Backend.Filters;

/// <summary>
/// Ensures required JSON value-type properties were present before model binding supplies defaults.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RequireJsonFieldsAttribute(params string[] requiredFields)
    : Attribute, IAsyncResourceFilter
{
    public async Task OnResourceExecutionAsync(
        ResourceExecutingContext context,
        ResourceExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        request.EnableBuffering();

        string body;
        using (var reader = new StreamReader(
            request.Body,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            leaveOpen: true))
        {
            body = await reader.ReadToEndAsync(context.HttpContext.RequestAborted);
        }

        request.Body.Position = 0;
        if (string.IsNullOrWhiteSpace(body))
        {
            await next();
            return;
        }

        JObject payload;
        try
        {
            payload = JObject.Parse(body);
        }
        catch (JsonReaderException)
        {
            await next();
            return;
        }

        var missingFields = requiredFields
            .Where(field => !payload.TryGetValue(field, StringComparison.OrdinalIgnoreCase, out var token)
                || token.Type is JTokenType.Null or JTokenType.Undefined)
            .ToList();
        if (missingFields.Count == 0)
        {
            await next();
            return;
        }

        context.Result = new OkObjectResult(new
        {
            code = 400,
            message = $"缺少必填字段：{string.Join("、", missingFields)}",
            data = (object?)null,
        });
    }
}
