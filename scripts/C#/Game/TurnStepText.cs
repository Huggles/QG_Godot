using System.Collections.Generic;

/// <summary>How a turn step reads on the HUD's turn-sequence strip and card, worded as the physical card words it.</summary>
public sealed record TurnStepText(string Name, string Rule, bool Optional = false)
{
    private static readonly Dictionary<TurnStep, TurnStepText> BaseSteps = new()
    {
        [TurnStep.PLAY_CARD]     = new("Play", "Play or discard one card from your hand."),
        [TurnStep.SUPPLY]        = new("Supply", "Remove your unsupplied pieces from the board."),
        [TurnStep.VICTORY_POINT] = new("Victory", "Collect Victory Points."),
        [TurnStep.DISCARD]       = new("Discard", "Discard as many cards as you want from your hand."),
        [TurnStep.DRAW]          = new("Draw", "Draw until your hand has seven cards."),
    };

    /// <summary>The base step's text, else the text the expansion that added it gave, else the bare enum name.</summary>
    public static TurnStepText For(TurnStep step)
        => BaseSteps.TryGetValue(step, out TurnStepText text) ? text
         : GameOptions.Rules.FindTurnStep(step)?.Text ?? new TurnStepText(step.ToString(), "");
}
