using Godot;
using System;
using System.Collections.Generic;

public abstract class Condition
{
    public abstract bool MeetCondition();

    int CountryId;
    int UnitId;
    int CardId;
    Faction Faction;

    public class CountryIsBuildable : Condition
    {
        public CountryIsBuildable(int countryId, Faction faction)
        {
            this.CountryId = countryId;
            this.Faction = faction;
        }

        public override bool MeetCondition()
        {
            return CountryState.ForId(CountryId).CanAttack(Faction);
        }
    }

    public class CountryIsAttackable : Condition
    {
        public CountryIsAttackable(int countryId, Faction faction)
        {
            this.CountryId = countryId;
            this.Faction = faction;

        }
        public override bool MeetCondition()
        {
            return CountryState.ForId(CountryId).CanAttack(Faction);
        }
    }

    public class CountryIsEmpty : Condition
    {
        public CountryIsEmpty(int countryId)
        {
            this.CountryId = countryId;

        }
        public override bool MeetCondition()
        {
            return CountryState.ForId(CountryId).Units.Count == 0;
        }
    }

    public class CountryHasAttackableNeighbor : Condition
    {
        public CountryHasAttackableNeighbor(int countryId)
        {
            this.CountryId = countryId;

        }
        public override bool MeetCondition()
        {
            return CountryState.ForId(CountryId).Units.Count == 0;
        }
    }
    
    public class CardInPlay : Condition
    {
        public CardInPlay(int cardId) { this.CardId = cardId; }
        public CardInPlay(CardState cardState) { this.CardId = cardState.Id; }

        public override bool MeetCondition()
        {
            CardState cardState = CardState.ForId(CardId);
            if (cardState.CardData.CardType != CardType.STATUS || cardState.CardData.CardType != CardType.RESPONSE)
            {
                throw new Exception("Card is not status or response");
            }
            return cardState.CardLogic.IsPlayed;
        }
    }
}
