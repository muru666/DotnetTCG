using System.Text;
using DotnetTCG.Application.Users;
using FluentValidation;

namespace DotnetTCG.Application.Auth;

public sealed record RecoveryRequest(string Email);
public sealed record RecoveryValidateRequest(string Token);
public sealed record RecoveryResetRequest(string Token, string NewPassword);

public sealed class RecoveryRequestValidator : AbstractValidator<RecoveryRequest>
{
    public RecoveryRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
    }
}

public sealed class RecoveryResetValidator : AbstractValidator<RecoveryResetRequest>
{
    public RecoveryResetValidator()
    {
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.NewPassword).MinimumLength(8)
            .Must(p => Encoding.UTF8.GetByteCount(p) <= UserService.PasswordMaxBytes)
            .WithMessage($"Password must be at most {UserService.PasswordMaxBytes} bytes.");
    }
}
