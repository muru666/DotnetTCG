using DotnetTCG.Application.Cards;
using FluentValidation;

namespace DotnetTCG.Application.Decks;

public sealed record CreateDeckRequest(string Name);
public sealed record RenameDeckRequest(string Name);
public sealed record AddCardRequest(string CardId, int Quantity);
public sealed record SetQuantityRequest(int Quantity);
public sealed record SearchCardsQuery(string? Name, string? Type, string? Supertype, int Page, int PageSize);

public sealed class CreateDeckValidator : AbstractValidator<CreateDeckRequest>
{
    public CreateDeckValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}

public sealed class RenameDeckValidator : AbstractValidator<RenameDeckRequest>
{
    public RenameDeckValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}

public sealed class AddCardValidator : AbstractValidator<AddCardRequest>
{
    public AddCardValidator()
    {
        RuleFor(x => x.CardId).NotEmpty();
        RuleFor(x => x.Quantity).InclusiveBetween(1, 60);
    }
}

public sealed class SearchCardsValidator : AbstractValidator<SearchCardsQuery>
{
    public SearchCardsValidator()
    {
        // page * pageSize debe caber en int: sin tope, page=30000000 desbordaba
        // y el backend respondía 500 (OFFSET negativo).
        RuleFor(x => x.Page).GreaterThanOrEqualTo(0)
            .LessThanOrEqualTo(int.MaxValue / CardService.MaxPageSize);
        RuleFor(x => x.PageSize).InclusiveBetween(1, CardService.MaxPageSize);
    }
}
