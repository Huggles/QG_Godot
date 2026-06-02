using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public abstract class GameAnimation : IGameQueueItem
{
    public abstract Task Execute();

    public class ShowCardsAnimation : GameAnimation
    {
        List<int> _cardIds;
        string _text;
        public ShowCardsAnimation(List<int> cardIds, string text)
        {
            this._cardIds = cardIds;
            this._text = text;
        }
        public async override Task Execute()
        {
            List<PresentationItem> presentationItems = PresentationItemCard.FromCardIds(_cardIds, false);
            await PresentationModal.Current.ShowModal(presentationItems, _text);       
        }
    }
}
