namespace DotnetTCG.Domain.Game;

public sealed record ChoiceOption(string OptionId, string Label, string? ImageUrl = null);

public sealed record PendingChoice(
    string ChoiceId,
    int PlayerIndex,
    string? AbilityName,
    string Prompt,
    IReadOnlyList<ChoiceOption> Options,
    string? Context = null);
