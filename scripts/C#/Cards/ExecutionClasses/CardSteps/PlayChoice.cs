using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// The <see cref="StepChoice"/> of a <see cref="PlayCardStep"/>: which cards the step could play, and
/// how one is picked. <see cref="Candidates"/> lists them without prompting, which is how a card that
/// plays another card (Reallocate Resources, Guards) can be valued before it runs.
/// </summary>
public sealed class PlayChoice
{
    private readonly Func<StepOption?, List<int>> _candidates;
    private readonly string _promptTitle;

    private PlayChoice(Func<StepOption?, List<int>> candidates, string promptTitle)
    {
        _candidates = candidates;
        _promptTitle = promptTitle;
    }

    /// <summary>The player picks one of these cards, through SelectCardRequestHandler.</summary>
    public static PlayChoice From(Func<StepOption?, List<int>> cardIds, string promptTitle) => new(cardIds, promptTitle);

    /// <summary>No prompt: the step plays exactly this card.</summary>
    public static PlayChoice Fixed(Func<StepOption?, int> cardId) => new(previous => new List<int> { cardId(previous) }, null);

    /// <summary>Every card the step could play right now. Pure; may throw when the previous outcome is needed but unknown.</summary>
    public List<int> Candidates(StepOption? previous) => _candidates(previous);

    /// <summary>The real path: the card to play, or -1 for none (an empty answer only a host timeout produces).</summary>
    internal async Task<int> Run(Faction faction, StepOption? previous)
    {
        List<int> candidates = _candidates(previous);
        if (_promptTitle == null) return candidates.Count > 0 ? candidates[0] : -1;

        InputRequest pick = await new InputRequest.SelectCardRequestHandler(faction, candidates, _promptTitle).BroadCast();
        return pick.ResponseCardIds.Count == 0 ? -1 : pick.ResponseCardIds[0];
    }
}
