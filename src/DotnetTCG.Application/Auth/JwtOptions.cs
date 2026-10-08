namespace DotnetTCG.Application.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Key { get; init; } = string.Empty;
    public string Issuer { get; init; } = "DotnetTCG";
    public string Audience { get; init; } = "DotnetTCG";
    public int AccessTokenMinutes { get; init; } = 60;
}
