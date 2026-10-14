using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace DotnetTCG.Api.Infrastructure;

public static class ErrorMapping
{
    public static ActionResult ToActionResult<T>(this ControllerBase controller, Domain.Common.Result<T> result)
    {
        if (result.IsSuccess)
            return controller.Ok(result.Value);
        return controller.StatusCode(StatusCode(result.Error.Code),
            new ProblemDetails
            {
                Status = StatusCode(result.Error.Code),
                Title = result.Error.Code,
                Detail = result.Error.Message,
            });
    }

    public static ActionResult Invalid(
        this ControllerBase controller, FluentValidation.Results.ValidationResult validation)
        => controller.BadRequest(new ValidationProblemDetails(validation.ToDictionary()));

    public static int StatusCode(string code)
    {
        if (code.EndsWith("Unauthorized", StringComparison.Ordinal))
            return StatusCodes.Status401Unauthorized;
        if (code.EndsWith("Forbidden", StringComparison.Ordinal) || code == "Game.WrongPassword")
            return StatusCodes.Status403Forbidden;
        if (code.EndsWith("NotFound", StringComparison.Ordinal))
            return StatusCodes.Status404NotFound;
        if (code is "Game.Conflict" or "Deck.DuplicateName" or "User.DuplicateUsername" or "User.DuplicateEmail")
            return StatusCodes.Status409Conflict;
        return StatusCodes.Status400BadRequest;
    }
}

public static class CurrentUser
{
    public static Guid? Id(ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
