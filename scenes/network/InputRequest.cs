using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(SelectCountryRequestHandler),       "SelectCountry")]
[JsonDerivedType(typeof(HandCardPlayRequestHandler),        "RequestHandCardPlay")]
[JsonDerivedType(typeof(ActivateCardRequestHandler),        "ActivateCard")]
[JsonDerivedType(typeof(HandCardsDiscardRequestHandler),    "RequestHandCardsDiscard")]
[JsonDerivedType(typeof(CardsRequestHandler),               "RequestCards")]
[JsonDerivedType(typeof(ForceDiscardHandCardsRequestHandler), "ForceDiscardHandCards")]
[JsonDerivedType(typeof(SelectUnitRequestHandler),           "SelectUnit")]
[JsonDerivedType(typeof(SelectBattleTargetRequestHandler),   "SelectBattleTarget")]
[JsonDerivedType(typeof(SelectFactionRequestHandler),        "SelectFaction")]
[JsonDerivedType(typeof(SelectOptionRequestHandler),         "SelectOption")]
[JsonDerivedType(typeof(ReorderCardsRequestHandler),         "ReorderCards")]
public abstract partial class InputRequest
{   
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public int TargetPeer => PlayerFactionRegistry.GetPeerIdForFaction(TargetFaction); 
    public bool IsForCurrentPeer => TargetPeer == PlayerScene.Current.GetMultiplayerAuthority();
    public Faction TargetFaction { get; set; }

    public List<int> TargetCountryIds { get; set; }
    public List<int> TargetUnitIds { get; set; }
    public List<int> TargetCardIds { get; set; }
    public List<int> TargetStepIds { get; set; }
    public List<Faction> TargetFactions { get; set; }
    public List<string> TargetOptionLabels { get; set; }

    public List<int> ResponseCountryIds { get; set; } = new();
    public List<int> ResponseUnitIds { get; set; } = new();
    public List<int> ResponseCardIds { get; set; } = new();
    public List<Faction> ResponseFactions { get; set; } = new();
    public List<int> ResponseStepIds { get; set; } = new();

    public InputRequest(Faction targetFaction)
    {
        TargetFaction = targetFaction;
    }

    public static InputRequest FromJson(string jsonDto) => JsonSerializer.Deserialize<InputRequest>(jsonDto);
    public string ToJson() => JsonSerializer.Serialize(this);

    public async Task Execute()
    {
        if(IsForCurrentPeer)
        {
            await Handle();
        } 
        else
        {
            PlayerActionLabel.ShowText($"Waiting on {TargetFaction} input...");
        }   
    }

    public async Task<InputRequest> BroadCast()
    {
        DebugUtilities.PrintPeer($"Broadcasting input request {GetType().Name} to {TargetFaction}");
        InputRequest responseDto = await NetworkApi.Instance.SendInputRequest(this);              
        return responseDto;
    }


    public abstract Task Handle();

    public class SelectCountryRequestHandler : InputRequest
    {
        public SelectCountryRequestHandler(Faction targetFaction, List<int> targetCountryIds) : base(targetFaction)
        {
            TargetCountryIds = targetCountryIds;
        }

        public override async Task Handle()
        {
            ResponseCountryIds.Add(await new SelectCountryHandler(TargetCountryIds).Handle());
        }
    }    

    public class HandCardPlayRequestHandler : InputRequest
    {
        public HandCardPlayRequestHandler(Faction targetFaction) : base(targetFaction) {}

        public override async Task Handle()
        {
            PlayerScene.Current.InputManager.SetPlayCardInputActive(TargetFaction, true);            
            Variant[] results = await EventBus.GetSignalAwaiter("CardSelected");
            if (results != null && results.Length > 0)
            {
                ResponseCardIds.Add((int)results[0]);
            }
        }
    }

    public class ActivateCardRequestHandler : InputRequest
    {
        public ActivateCardRequestHandler(Faction targetFaction) : base(targetFaction) {}

        public override async Task Handle()
        {
            PlayerScene.Current.InputManager.SetPlayCardInputActive(TargetFaction, false);
            DebugUtilities.PrintPeer($"ActivateCard: Waiting for player input.");
            Variant[] results = await EventBus.GetSignalAwaiter("CardSelected");
            if (results != null && results.Length > 0)
            {
                ResponseCardIds.Add((int)results[0]);
            }
        }
    }

    public class HandCardsDiscardRequestHandler : InputRequest
    {
        public HandCardsDiscardRequestHandler(Faction targetFaction) : base(targetFaction) {}

        public override async Task Handle()
        {            
            InputHandlerDiscardHand inputHandler = new InputHandlerDiscardHand(TargetFaction);
            List<int> selectedCardIds = await inputHandler.GetSelectedCards();            
            ResponseCardIds = selectedCardIds;
        }
    }

    public class CardsRequestHandler : InputRequest
    {
        public CardsRequestHandler(Faction targetFaction, List<int> targetCardIds) : base(targetFaction)
        {
            TargetCardIds = targetCardIds;
        }

        public override async Task Handle()
        {            
            InputHandlerDiscard inputHandler = new InputHandlerDiscard(TargetFaction, TargetCardIds, 0, false);
            List<int> selectedCardIds = await inputHandler.GetSelectedCards();            
            ResponseCardIds = selectedCardIds;
        }
    }

    /// <summary>
    /// Sent to the peer controlling <see cref="InputRequest.TargetFaction"/>.
    /// Only that client's Handle() runs; all others wait.
    /// </summary>
    public class ForceDiscardHandCardsRequestHandler : InputRequest
    {
        public int NumberOfCards { get; set; }

        public ForceDiscardHandCardsRequestHandler(Faction targetFaction, int numberOfCards) : base(targetFaction)
        {
            NumberOfCards = numberOfCards;
        }

        public override async Task Handle()
        {
            List<int> handCardIds = DeckState.ForFaction(TargetFaction).HandCardIds;
            InputHandlerDiscard inputHandler = new InputHandlerDiscard(TargetFaction, handCardIds, NumberOfCards, true);
            List<int> selectedCardIds = await inputHandler.GetSelectedCards();
            ResponseCardIds = selectedCardIds;
        }
    }

    public class SelectUnitRequestHandler : InputRequest
    {
        public SelectUnitRequestHandler(Faction targetFaction, List<int> targetUnitIds) : base(targetFaction)
        {
            TargetUnitIds = targetUnitIds;
        }

        public override async Task Handle()
        {
            ResponseUnitIds.Add(await new SelectUnitHandler(TargetUnitIds).Handle());
        }
    }

    public class SelectBattleTargetRequestHandler : InputRequest
    {
        [JsonConstructor]
        public SelectBattleTargetRequestHandler(Faction targetFaction, List<int> targetCountryIds, List<int> targetUnitIds) : base(targetFaction)
        {
            TargetCountryIds = targetCountryIds;
            TargetUnitIds = targetUnitIds;
        }

        public SelectBattleTargetRequestHandler(Faction targetFaction, List<BattleTarget> targets) : base(targetFaction)
        {
            TargetCountryIds = targets.Where(bt => bt.Type == TargetType.COUNTRY).Select(bt => bt.Id).ToList();
            TargetUnitIds = targets.Where(bt => bt.Type == TargetType.UNIT).Select(bt => bt.Id).ToList();
        }

        public override async Task Handle()
        {
            SelectBattleTargetHandler handler = new SelectBattleTargetHandler(TargetCountryIds, TargetUnitIds);
            BattleTarget result = await handler.Handle();
            if (result.Type == TargetType.COUNTRY)
                ResponseCountryIds.Add(result.Id);
            else
                ResponseUnitIds.Add(result.Id);
        }
    }

    public class SelectFactionRequestHandler : InputRequest
    {
        [JsonConstructor]
        public SelectFactionRequestHandler(Faction targetFaction, List<Faction> targetFactions) : base(targetFaction)
        {
            TargetFactions = targetFactions;
        }

        public override async Task Handle()
        {
            var items = PresentationItem.ForFactions(TargetFactions);
            ModalResult result = await PresentationModal.Current.Show(
                ModalConfig.SelectOne("Select a faction", items));
            ResponseCardIds.Add(result.WasCancelled ? -1 : result.SelectedItems[0]);
        }
    }

    public class SelectOptionRequestHandler : InputRequest
    {
        public List<int> TargetOptionIds { get; set; } = new();
        public string ModalTitle { get; set; }

        [JsonConstructor]
        public SelectOptionRequestHandler(Faction targetFaction, List<string> targetOptionLabels, List<int> targetOptionIds, string modalTitle) : base(targetFaction)
        {
            TargetOptionLabels = targetOptionLabels;
            TargetOptionIds = targetOptionIds;
            ModalTitle = modalTitle;
        }

        // Convenience: auto 0-based identifiers
        public SelectOptionRequestHandler(Faction targetFaction, List<string> targetOptionLabels, string modalTitle)
            : this(targetFaction, targetOptionLabels,
                   Enumerable.Range(0, targetOptionLabels.Count).ToList(), modalTitle) { }

        public override async Task Handle()
        {
            var items = TargetOptionLabels
                .Select((label, i) => (PresentationItem)new PresentationItemImageButton(TargetOptionIds[i], label, true))
                .ToList();
            ModalResult result = await PresentationModal.Current.Show(ModalConfig.SelectOne(ModalTitle, items));
            ResponseCardIds.Add(result.WasCancelled ? -1 : result.SelectedItems[0]);
        }
    }

    public class ReorderCardsRequestHandler : InputRequest
    {
        public ReorderCardsRequestHandler(Faction targetFaction, List<int> targetCardIds) : base(targetFaction)
        {
            TargetCardIds = targetCardIds;
        }

        public override async Task Handle()
        {
            var items = PresentationItemCard.FromCardIds(TargetCardIds, true);
            ModalResult result = await PresentationModal.Current.Show(
                ModalConfig.Reorder("Reorder the top cards of your draw deck", items));
            ResponseCardIds = result.WasCancelled ? new List<int>(TargetCardIds) : result.SelectedItems;
        }
    }


    
}
