using DotnetTCG.Api.Infrastructure;
using DotnetTCG.Application.Auth;
using DotnetTCG.Application.Users;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DotnetTCG.Api.Controllers;

[ApiController]
[Route("v1/auth")]
public sealed class AuthController(
    UserService users,
    RecoveryService recovery,
    IValidator<RegisterRequest> registerValidator,
    IValidator<LoginRequest> loginValidator,
    IValidator<RecoveryRequest> recoveryValidator,
    IValidator<RecoveryResetRequest> resetValidator,
    IOptions<JwtOptions> jwt) : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<UserDto>> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var validation = await registerValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return this.Invalid(validation);
        var result = await users.RegisterAsync(request.Username, request.Email, request.Password, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(Me), result.Value)
            : this.ToActionResult(result);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthTokenDto>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var validation = await loginValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return this.Invalid(validation);
        var result = await users.LoginAsync(
            request.Username, request.Password, jwt.Value.AccessTokenMinutes, ct);
        return result.IsSuccess ? Ok(result.Value) : this.ToActionResult(result);
    }

    [HttpGet("/v1/users/me")]
    [Authorize]
    public async Task<ActionResult<UserDto>> Me(CancellationToken ct)
    {
        var userId = CurrentUser.Id(User);
        if (userId is null)
            return Unauthorized();
        return this.ToActionResult(await users.GetProfileAsync(userId.Value, ct));
    }

    [HttpPost("/v1/password-recovery/request")]
    [AllowAnonymous]
    public async Task<ActionResult> RequestRecovery([FromBody] RecoveryRequest request, CancellationToken ct)
    {
        var validation = await recoveryValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return this.Invalid(validation);
        var result = await recovery.RequestAsync(request.Email, ct);
        return result.IsSuccess
            ? Ok(new { message = "If the email exists, a token was sent." })
            : this.ToActionResult(result);
    }

    [HttpPost("/v1/password-recovery/validate")]
    [AllowAnonymous]
    public async Task<ActionResult> ValidateRecovery(
        [FromBody] RecoveryValidateRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            return BadRequest();
        var result = await recovery.ValidateAsync(request.Token, ct);
        return Ok(new { valid = result.Value });
    }

    [HttpPost("/v1/password-recovery/reset")]
    [AllowAnonymous]
    public async Task<ActionResult> ResetRecovery([FromBody] RecoveryResetRequest request, CancellationToken ct)
    {
        var validation = await resetValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return this.Invalid(validation);
        var result = await recovery.ResetAsync(request.Token, request.NewPassword, ct);
        return result.IsSuccess
            ? Ok(new { message = "Password updated." })
            : this.ToActionResult(result);
    }
}
