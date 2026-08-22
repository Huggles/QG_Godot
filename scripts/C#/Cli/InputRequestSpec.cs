using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>What a prompt's options refer to. Also decides which Response* bucket the answer lands in.</summary>
public enum CliOptionKind { Country, Unit, Card, Faction, Option }

/// <summary>One selectable option, with a human label for display and an id for the response.</summary>
public sealed class CliOption
{
    public CliOptionKind Kind;
    public int Id;
    public string Label;

    public override string ToString() => $"[{Id}] {Label}";
}

/// <summary>
/// How a prompt expresses "I decline". These are NOT interchangeable, and getting one wrong
/// corrupts the game loop rather than failing loudly:
///
///  Skip          — WasSkipped = true. BroadCast turns this into StepSkippedException, abandoning
///                  the rest of the card. Correct for the board selections.
///  EmptyResponse — empty Response list, WasSkipped FALSE. This is how "pass on playing a card" and
///                  "no block" are expressed. Setting WasSkipped here would wrongly abort the card —
///                  EventLendLease calls HandCardPlayRequestHandler(...).BroadCast(), so the throw
///                  would escape.
///  EchoTargets   — answer with the original order. ReorderCards' cancel path.
///  NotAllowed    — a mandatory discard. There is no pass.
/// </summary>
public enum PassMode { Skip, EmptyResponse, EchoTargets, NotAllowed }

/// <summary>
/// A uniform description of any <see cref="InputRequest"/>: what may be chosen, how many, how to
/// decline, and where the answer goes.
///
/// This table is what lets one generic resolver serve all 12 request types. The base class already
/// normalises the option space (Target*Ids) and the answer space (Response*Ids); only four scalars
/// vary per type, so a 13th subclass costs one row here and no CLI code at all.
/// </summary>
public sealed class InputRequestSpec
{
    public string Kind;
    public Faction Faction;
    public string Title;
    public List<CliOption> Options = new();
    public int MinSelections;
    public int MaxSelections;
    public PassMode Pass;

    public bool CanPass => Pass != PassMode.NotAllowed;

    // ── Construction ─────────────────────────────────────────────────────────

    public static InputRequestSpec For(InputRequest request)
    {
        InputRequestSpec spec = new()
        {
            Kind = request.GetType().Name.Replace("RequestHandler", ""),
            Faction = request.TargetFaction,
        };

        switch (request)
        {
            case InputRequest.SelectCountryRequestHandler:
                spec.Title = "Select a country";
                spec.Options = Countries(request.TargetCountryIds);
                spec.MinSelections = spec.MaxSelections = 1;
                spec.Pass = PassMode.Skip;
                break;

            // AllowSkip = false is a mandatory selection (freeing a unit for a deploy the pool cannot
            // pay for). Reporting Skip for it would let a script pass on a prompt the GUI gives no Skip
            // button for, so the CLI and the game would disagree about what is answerable.
            case InputRequest.SelectUnitRequestHandler unitRequest:
                spec.Title = unitRequest.AllowSkip ? "Select a unit" : "Select a unit (required)";
                spec.Options = Units(request.TargetUnitIds);
                spec.MinSelections = spec.MaxSelections = 1;
                spec.Pass = unitRequest.AllowSkip ? PassMode.Skip : PassMode.NotAllowed;
                break;

            // The only prompt whose options span two kinds. The chosen option's Kind is what decides
            // whether the answer goes to ResponseCountryIds or ResponseUnitIds — which is exactly how
            // SelectBattleTargetHandler encodes a BattleTarget.
            case InputRequest.SelectBattleTargetRequestHandler:
                spec.Title = "Select a battle target";
                spec.Options = Countries(request.TargetCountryIds).Concat(Units(request.TargetUnitIds)).ToList();
                spec.MinSelections = spec.MaxSelections = 1;
                spec.Pass = PassMode.Skip;
                break;

            // Answer goes into ResponseCardIds holding (int)faction — a real quirk of the modal
            // (PresentationItem.ForFactions uses the enum value as the item id) that EventLendLease
            // reads back as (Faction)ResponseCardIds[0]. Do not "fix" it.
            case InputRequest.SelectFactionRequestHandler:
                spec.Title = "Select a faction";
                spec.Options = (request.TargetFactions ?? new List<Faction>())
                    .Select(f => new CliOption { Kind = CliOptionKind.Faction, Id = (int)f, Label = f.ToString() })
                    .ToList();
                spec.MinSelections = spec.MaxSelections = 1;
                spec.Pass = PassMode.Skip;
                break;

            // Same bucket quirk: the answer is TargetOptionIds[i], not the index.
            case InputRequest.SelectOptionRequestHandler opt:
                spec.Title = opt.ModalTitle ?? "Choose an option";
                spec.Options = (opt.TargetOptionLabels ?? new List<string>())
                    .Select((label, i) => new CliOption
                    {
                        Kind = CliOptionKind.Option,
                        Id = i < opt.TargetOptionIds.Count ? opt.TargetOptionIds[i] : i,
                        Label = label,
                    })
                    .ToList();
                spec.MinSelections = spec.MaxSelections = 1;
                spec.Pass = PassMode.Skip;
                break;

            case InputRequest.ReorderCardsRequestHandler:
                spec.Title = "Reorder the top cards of your draw deck";
                spec.Options = Cards(request.TargetCardIds);
                spec.MinSelections = spec.MaxSelections = spec.Options.Count;
                spec.Pass = PassMode.EchoTargets;
                break;

            case InputRequest.ForceDiscardHandCardsRequestHandler force:
                spec.Title = $"Discard {force.NumberOfCards} card(s)";
                spec.Options = Cards(request.TargetCardIds);
                spec.MinSelections = spec.MaxSelections = force.NumberOfCards;
                spec.Pass = PassMode.NotAllowed;
                break;

            case InputRequest.SelectCardRequestHandler select:
                spec.Title = select.Title;
                spec.Options = Cards(request.TargetCardIds);
                spec.MinSelections = select.Required ? 1 : 0;
                spec.MaxSelections = 1;
                spec.Pass = select.Required ? PassMode.NotAllowed : PassMode.EmptyResponse;
                break;

            // MaxSelections -1 means unlimited in ModalConfig; the CLI clamps both ends, so translate
            // it to the option count rather than passing -1 through as a bound.
            case InputRequest.SelectCardsRequestHandler selectMany:
                spec.Title = selectMany.Title;
                spec.Options = Cards(request.TargetCardIds);
                spec.MinSelections = selectMany.MinSelections;
                spec.MaxSelections = selectMany.MaxSelections < 0
                    ? spec.Options.Count
                    : Math.Min(selectMany.MaxSelections, spec.Options.Count);
                spec.Pass = selectMany.MinSelections > 0 ? PassMode.NotAllowed : PassMode.EmptyResponse;
                break;

            case InputRequest.HandCardsDiscardRequestHandler:
            case InputRequest.CardsRequestHandler:
                spec.Title = "Select cards to discard";
                spec.Options = Cards(request.TargetCardIds);
                spec.MinSelections = 0;
                spec.MaxSelections = spec.Options.Count;
                spec.Pass = PassMode.EmptyResponse;
                break;

            case InputRequest.HandCardPlayRequestHandler:
            case InputRequest.ActivateCardRequestHandler:
            case InputRequest.BlockReactionRequestHandler:
                spec.Options = Cards(request.TargetCardIds);
                spec.Title = request is InputRequest.BlockReactionRequestHandler
                    ? "Play a block reaction, or pass"
                    : "Play a card, or pass";
                // A reaction window with nothing on offer is the always-ask rule at work: a faction
                // holding a face-down Response card is asked every time so that being asked stops
                // proving anything. Say so, or the transcript reads like a bug.
                if (request.IsReactionWindow && spec.Options.Count == 0)
                    spec.Title += " (nothing available — always-ask window)";
                spec.MinSelections = 0;
                spec.MaxSelections = 1;
                spec.Pass = PassMode.EmptyResponse;
                break;

            default:
                // A new subclass with no row here: offer whatever the base fields carry rather than
                // hard-failing, and let the operator decide. Better a degraded prompt than a dead loop.
                spec.Title = $"{spec.Kind} (no CLI spec — using base fields)";
                spec.Options = Countries(request.TargetCountryIds)
                    .Concat(Units(request.TargetUnitIds))
                    .Concat(Cards(request.TargetCardIds))
                    .ToList();
                spec.MinSelections = 0;
                spec.MaxSelections = 1;
                spec.Pass = PassMode.Skip;
                break;
        }

        return spec;
    }

    // ── Applying an answer ───────────────────────────────────────────────────

    /// <summary>Write the chosen options into the request's Response* fields, per option kind.</summary>
    public void Apply(InputRequest request, IEnumerable<CliOption> chosen)
    {
        foreach (CliOption option in chosen)
        {
            switch (option.Kind)
            {
                case CliOptionKind.Country: request.ResponseCountryIds.Add(option.Id); break;
                case CliOptionKind.Unit:    request.ResponseUnitIds.Add(option.Id);    break;
                // Card, Faction and Option all land in ResponseCardIds — see the quirks above.
                default:                    request.ResponseCardIds.Add(option.Id);    break;
            }
        }
    }

    /// <summary>Decline, using this request's own pass idiom.</summary>
    public void ApplyPass(InputRequest request)
    {
        switch (Pass)
        {
            case PassMode.Skip:
                request.WasSkipped = true;
                break;

            case PassMode.EchoTargets:
                request.ResponseCardIds = new List<int>(request.TargetCardIds ?? new List<int>());
                break;

            case PassMode.NotAllowed:
                // Mandatory. Auto-pass still has to answer something or the loop parks forever, so
                // take the first N — the same shape a player forced to discard would produce.
                request.ResponseCardIds = Options.Take(MinSelections).Select(o => o.Id).ToList();
                break;

            case PassMode.EmptyResponse:
            default:
                // Nothing to do: empty Response lists with WasSkipped false already mean "pass".
                break;
        }
    }

    // ── Labels ───────────────────────────────────────────────────────────────

    private static List<CliOption> Countries(List<int> ids) => (ids ?? new List<int>())
        .Select(id => new CliOption
        {
            Kind = CliOptionKind.Country,
            Id = id,
            Label = CountryState.ForId(id)?.Label ?? CountryState.ForId(id)?.Name ?? $"country#{id}",
        }).ToList();

    private static List<CliOption> Units(List<int> ids) => (ids ?? new List<int>())
        .Select(id =>
        {
            UnitState unit = UnitState.ForId(id);
            string where = unit?.CountryState?.Label ?? unit?.CountryState?.Name ?? "reserve";
            return new CliOption
            {
                Kind = CliOptionKind.Unit,
                Id = id,
                Label = unit == null ? $"unit#{id}" : $"{unit.Faction} {unit.Type} in {where}",
            };
        }).ToList();

    private static List<CliOption> Cards(List<int> ids) => (ids ?? new List<int>())
        .Select(id => new CliOption
        {
            Kind = CliOptionKind.Card,
            Id = id,
            Label = CardState.ForId(id)?.CardName ?? $"card#{id}",
        }).ToList();
}
