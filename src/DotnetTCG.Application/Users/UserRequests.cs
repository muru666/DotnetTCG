using FluentValidation;

namespace DotnetTCG.Application.Users;

public sealed record RegisterRequest(string Username, string Email, string Password);
public sealed record LoginRequest(string Username, string Password);

public sealed class RegisterValidator : AbstractValidator<RegisterRequest>
{
    public RegisterValidator()
    {
        RuleFor(x => x.Username).NotEmpty().Length(3, 50);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.Password).MinimumLength(8).MaximumLength(200);
    }
}

public sealed class LoginValidator : AbstractValidator<LoginRequest>
{
    public LoginValidator()
    {
        RuleFor(x => x.Username).NotEmpty();
        RuleFor(x => x.Password).NotEmpty();
    }
}
