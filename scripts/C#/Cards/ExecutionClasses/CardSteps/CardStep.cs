using Godot;
using System;
using System.Threading.Tasks;

public abstract partial class CardStep : GodotObject
{
    protected CardLogic CardLogic;
    protected Faction TriggeringFaction { get { return CardLogic.Faction; } }
    
    public CardStep(CardLogic cardLogic)
    {
        this.CardLogic = cardLogic;
    }
    public abstract Task<ChangeEvent> Execute();

    public T BuildChangeEvent<T>(T changeEvent) where T : ChangeEvent
    {        
        changeEvent.SourceCardId = CardLogic.CardState.Id;
        changeEvent.TriggeringFaction = CardLogic.CardState.Faction;
        return changeEvent;
    }
}
