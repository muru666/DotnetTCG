using DotnetTCG.Api.Infrastructure;
using DotnetTCG.Application.Cards;
using DotnetTCG.Application.Decks;
using DotnetTCG.Domain.Enums;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DotnetTCG.Api.Controllers;

[ApiController]
[Route("v1/cards")]
[Authorize]
public sealed class CardsController(
    CardService cards,
    IValidator<SearchCardsQuery> searchValidator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<CardDto>>> Search(
        [FromQuery] string? name, [FromQuery] string? type, [FromQuery] string? supertype,
        [FromQuery] int page = 0, [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new SearchCardsQuery(name, type, supertype, page, pageSize);
        var validation = await searchValidator.ValidateAsync(query, ct);
        if (!validation.IsValid)
            return this.Invalid(validation);

        PokemonType? pokemonType = null;
        if (type is not null)
        {
            if (!Enum.TryParse<PokemonType>(type, ignoreCase: true, out var parsed))
                return BadRequest(new { message = $"Unknown type '{type}'." });
            pokemonType = parsed;
        }

        SuperType? superType = null;
        if (supertype is not null)
        {
            if (!Enum.TryParse<SuperType>(supertype, ignoreCase: true, out var parsedSuper))
                return BadRequest(new { message = $"Unknown supertype '{supertype}'." });
            superType = parsedSuper;
        }

        return Ok(await cards.SearchAsync(name, pokemonType, superType, page, pageSize, ct));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<CardDto>> GetById(string id, CancellationToken ct)
        => this.ToActionResult(await cards.GetByIdAsync(id, ct));

    [HttpPost("import")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> Import([FromQuery] string setId = "xy1", CancellationToken ct = default)
    {
        var result = await cards.ImportSetAsync(setId, ct);
        return result.IsSuccess
            ? Ok(new { imported = result.Value })
            : this.ToActionResult(result);
    }
}

[ApiController]
[Route("v1/decks")]
[Authorize]
public sealed class DecksController(DeckService decks) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<DeckDto>> Create([FromBody] CreateDeckRequest request, CancellationToken ct)
    {
        var userId = CurrentUser.Id(User);
        if (userId is null)
            return Unauthorized();
        var result = await decks.CreateAsync(userId.Value, request.Name, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value)
            : this.ToActionResult(result);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DeckDto>>> List(CancellationToken ct)
    {
        var userId = CurrentUser.Id(User);
        if (userId is null)
            return Unauthorized();
        return Ok(await decks.ListAsync(userId.Value, ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DeckDto>> GetById(Guid id, CancellationToken ct)
    {
        var userId = CurrentUser.Id(User);
        if (userId is null)
            return Unauthorized();
        return this.ToActionResult(await decks.GetAsync(id, userId.Value, ct));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<DeckDto>> Rename(
        Guid id, [FromBody] RenameDeckRequest request, CancellationToken ct)
    {
        var userId = CurrentUser.Id(User);
        if (userId is null)
            return Unauthorized();
        return this.ToActionResult(await decks.RenameAsync(id, userId.Value, request.Name, ct));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = CurrentUser.Id(User);
        if (userId is null)
            return Unauthorized();
        var result = await decks.DeleteAsync(id, userId.Value, ct);
        return result.IsSuccess ? NoContent() : this.ToActionResult(result);
    }

    [HttpPost("{id:guid}/cards")]
    public async Task<ActionResult<DeckDto>> AddCard(
        Guid id, [FromBody] AddCardRequest request, CancellationToken ct)
    {
        var userId = CurrentUser.Id(User);
        if (userId is null)
            return Unauthorized();
        return this.ToActionResult(
            await decks.AddCardAsync(id, userId.Value, request.CardId, request.Quantity, ct));
    }

    [HttpDelete("{id:guid}/cards/{cardId}")]
    public async Task<ActionResult<DeckDto>> RemoveCard(Guid id, string cardId, CancellationToken ct)
    {
        var userId = CurrentUser.Id(User);
        if (userId is null)
            return Unauthorized();
        return this.ToActionResult(await decks.RemoveCardAsync(id, userId.Value, cardId, ct));
    }

    [HttpPut("{id:guid}/cards/{cardId}")]
    public async Task<ActionResult<DeckDto>> SetQuantity(
        Guid id, string cardId, [FromBody] SetQuantityRequest request, CancellationToken ct)
    {
        var userId = CurrentUser.Id(User);
        if (userId is null)
            return Unauthorized();
        return this.ToActionResult(
            await decks.SetQuantityAsync(id, userId.Value, cardId, request.Quantity, ct));
    }

    [HttpPost("{id:guid}/validate")]
    public async Task<ActionResult<DeckValidationResult>> Validate(Guid id, CancellationToken ct)
    {
        var userId = CurrentUser.Id(User);
        if (userId is null)
            return Unauthorized();
        return this.ToActionResult(await decks.ValidateAsync(id, userId.Value, ct));
    }
}
