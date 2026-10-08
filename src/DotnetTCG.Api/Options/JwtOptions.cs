namespace DotnetTCG.Api.Options;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public required string Key { get; init; }
    public string Issuer { get; init; } = "DotnetTCG";
    public string Audience { get; init; } = "DotnetTCG";
    public int AccessTokenMinutes { get; init; } = 60;
}
