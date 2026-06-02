using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class DiscardHandCardsChangeEvent : ChangeEvent
{    
    public List<int> CardIds { get; set; }

    public DiscardHandCardsChangeEvent(Faction triggeringFaction, Faction targetFaction, List<int> cardIds) : base(triggeringFaction)
    {
        TriggeringFaction = triggeringFaction;
        TargetFaction = targetFaction;
        CardIds = cardIds;
    }

    public override ChangeEventDto ToDto()
    {
        DiscardHandCardsChangeEventDto dto = ChangeEventDto.Build<DiscardHandCardsChangeEventDto>(this, Id);
        dto.CardIds = CardIds;
        return dto;
    }

    protected override async Task<bool> ExecuteAsync()
    {
        await GameAPI.DiscardHandCards(TargetFaction, CardIds);
        await Task.Delay(GameSettings.PauseDuration);
        return true;
    }
}