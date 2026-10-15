using System.Collections.Generic;
using System.Linq;

public partial class EWOilEmbargo : EWCardLogic
{
    private const int CHOICE_DISCARD = 0;
    private const int CHOICE_REMOVE  = 1;

    public override List<CardStep> OnActivate() => new()
    {
        MakeFactionStep(Faction.GERMANY),
        MakeFactionStep(Faction.ITALY),
        MakeFactionStep(Faction.JAPAN)
    };

    /// <summary>One step per Axis country. The target picks its own penalty, so the step returns whichever event that is, as EWMaltaSubmarines does.</summary>
    private CardStep MakeFactionStep(Faction target)
    {
        return new ResultStep(this, async () => {
            List<int> pieces = TotalWarCards.Pieces(BoardState.Live, target).Select(unit => unit.Id).ToList();
            List<string> labels = new() { "Discard the top 2 cards of your draw deck" };
            List<int> ids = new() { CHOICE_DISCARD };
            if (pieces.Count > 0)
            {
                labels.Add("Remove a piece from the board");
                ids.Add(CHOICE_REMOVE);
            }

            var choice = await new InputRequest.SelectOptionRequestHandler(target, labels, ids, $"Oil Embargo: {target.Label()} must choose").BroadCast();
            if (choice.ResponseCardIds[0] == CHOICE_REMOVE)
            {
                int unitId = (await new InputRequest.SelectUnitRequestHandler(target, pieces, allowSkip: false).BroadCast()).ResponseUnitIds[0];
                return new RemoveUnitChangeEvent(Faction, unitId, UnitRemovalReason.ELIMINATE);
            }
            return new ForceDiscardCardsChangeEvent(Faction, target, 2);
        })
        .WithGuidance($"Make {target.Label()} remove a piece or discard 2 cards");
    }
}
