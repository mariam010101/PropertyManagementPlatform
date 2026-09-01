using Microsoft.AspNetCore.Mvc;
using PMP.Shared.Common;

namespace PMP.Api.Infrastructure;

public static class ResultExtensions
{
    public static IActionResult ToActionResult(this Result result)
    {
        return result.Succeeded
            ? new OkResult()
            : new BadRequestObjectResult(new { error = result.Error });
    }

    public static IActionResult ToActionResult<T>(this Result<T> result)
    {
        return result.Succeeded
            ? new OkObjectResult(result.Data)
            : new BadRequestObjectResult(new { error = result.Error });
    }
}
