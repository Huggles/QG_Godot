using Godot;
using System;
using System.Collections.Generic;


public partial class InputHandlerPlayCard : IInputHandler
{
    private static readonly Key[] HAND_CARD_KEYS = {
        Key.Key0, Key.Key1, Key.Key2, Key.Key3, Key.Key4, Key.Key5, Key.Key6
    };

    private GameFlow gameFlow => GameSession.Instance.GameFlow;
    private GameState gameState => GameSession.Instance.GameState;

    private void Init()
    {
        Faction currentFaction = gameFlow.CurrentFaction;
        string factionColorString = gameState.FactionStates[currentFaction].FactionData.ColorString;
            

        List<string> textLines = new List<string>();
        textLines.Add($"[color={factionColorString}][b]{Enum.GetName(typeof(Faction), currentFaction)}: Select a card to play:[/b][/color]");

        int index = 0;
        foreach (CardState cardState in DeckState.ForFaction(currentFaction).HandCardStates)
        {
            if (cardState.CanPlayCard())
            {
                textLines.Add($"{index} - {cardState.CardData.UniqueName}");
            }
            else
            {
                textLines.Add($"[color=red]{index} - {cardState.CardData.UniqueName}[/color]");
            }
            index++;
        }

        InputMessageLabel.ShowText(string.Join("\n", textLines));
        PlayerActionLabel.ShowText("Choose a card", currentFaction);
    }

    public void OnKeyClicked(InputEventKey keyEvent)
    {
        for (int i = 0; i < HAND_CARD_KEYS.Length; i++)
        {
            if (keyEvent.Keycode == HAND_CARD_KEYS[i])
            {
                var cardId = gameFlow.CurrentFactionDeckState.HandCardStates[i].Id;
                var activationOption = new CardActivationOption(cardId, "play");
                EventBus.Emit("card_selected",activationOption);
                break;
            }
        }
    }
}
