using System;
using System.Threading.Tasks;

/// <summary>
/// Creates the BulletinCardState that presents an <see cref="ActivatableMutator"/> to TargetFaction as
/// a card. Emitted once per eligible faction by GameModeMultiplayerDefault.RegisterMutators during
/// scenario setup, with IsTrigger = false.
///
/// Routed through a ChangeEvent — like SetStartingScoreChangeEvent and the setup PlayCardChangeEvents —
/// because clients never read the scenario file: RegisterMutators is host-only, so this is the only
/// thing that makes the Bulletins exist on the client side at all. CardId is allocated by the host and
/// carried on the wire rather than recomputed, so ids agree on every peer without depending on
/// client-side determinism.
/// </summary>
public partial class RegisterBulletinCardChangeEvent : ChangeEvent
{
    public int CardId { get; set; }
    public string MutatorClassName { get; set; }
    public int FromRound { get; set; }
    public int ToRound { get; set; }

    public RegisterBulletinCardChangeEvent(Faction targetFaction, int cardId, string mutatorClassName, int fromRound, int toRound)
        : base(Faction.NONE)
    {
        TargetFaction = targetFaction;
        CardId = cardId;
        MutatorClassName = mutatorClassName;
        FromRound = fromRound;
        ToRound = toRound;
    }

    // Setup bookkeeping, not a game action.
    public override bool ToHistoryItem => false;

    public override ChangeEventDto ToDto()
    {
        RegisterBulletinCardChangeEventDto dto = ChangeEventDto.Build<RegisterBulletinCardChangeEventDto>(this, Id);
        dto.CardId = CardId;
        dto.MutatorClassName = MutatorClassName;
        dto.FromRound = FromRound;
        dto.ToRound = ToRound;
        return dto;
    }

    protected override async Task<bool> ExecuteAsync()
    {
        Type mutatorType = Type.GetType(MutatorClassName);
        if (mutatorType == null || !typeof(ActivatableMutator).IsAssignableFrom(mutatorType))
            throw new Exception($"ActivatableMutator '{MutatorClassName}' was not found or does not extend ActivatableMutator.");

        ActivatableMutator mutator = (ActivatableMutator)Activator.CreateInstance(mutatorType);
        mutator.FromRound = FromRound;
        mutator.ToRound = ToRound;

        // STATUS is what keeps a Bulletin on the table: ActivateReactionChangeEvent only discards
        // RESPONSE cards, and CardLogic.OnNewTurnStarted re-arms a STATUS card's steps every turn. It
        // does not select the art — BulletinCardState overrides FrontTexture with the shared Bulletin
        // face, so no per-faction Bulletin frame is needed.
        CardData cardData = new CardData
        {
            UniqueName = MutatorClassName,
            Number = string.Empty,
            Label = mutator.Label,
            Text = mutator.Text,
            Type = "STATUS",
            ExecutionClass = MutatorClassName,
            MultipleActivationsPerTurn = mutator.MultipleActivationsPerTurn
        };

        BulletinCardState cardState = new BulletinCardState(cardData, mutator)
        {
            Id = CardId,
            Faction = TargetFaction
        };

        // CardState's reflecting constructor does this itself; the pre-built-logic constructor leaves
        // it to the caller.
        mutator.CardState = cardState;
        mutator.CardSteps = mutator.OnActivate();

        GameSession.Current.GameState.CardStates.Add(cardState);
        DebugUtilities.PrintPeer($"Registered Bulletin {MutatorClassName} for {TargetFaction} as card {CardId}");

        await Task.CompletedTask;
        return true;
    }

    public override string SummaryText() => $"{TargetFaction.WithPlayer()} gains the {MutatorClassName} Bulletin";
}
