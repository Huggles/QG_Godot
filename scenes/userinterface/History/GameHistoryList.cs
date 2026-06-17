using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class GameHistoryList : VBoxContainer
{    
    public static GameHistoryList Current { get; private set; }


    private ScrollContainer ScrollContainer => GetParent<ScrollContainer>();
    private List<GameHistoryRow> Rows = new();
    private List<int> DisplayedChangeEventIds = new(); // to prevent duplicates when joining mid-game
    

    public override void _Ready()
    {
        GetChildren().ToList().ForEach(item => item.QueueFree());
        if(Multiplayer.GetUniqueId() == GetMultiplayerAuthority())
        {
            Current = this;
            AddUnprocessedChangeEvents();
            EventBus.Instance.GameChangeEventAfter += OnGameChangeEventApplied;
        }                
        this.ItemRectChanged += async() => {            
            await Task.Delay(100); // wait for the rect change to fully apply before scrolling
            ScrollToBottom();            
        };
    }

    public override void _ExitTree()
    {        
        if(Current == this)
        {                        
            Current = null;
            EventBus.Instance.GameChangeEventAfter -= OnGameChangeEventApplied;
        }
    }

    private void AddUnprocessedChangeEvents()
    {
        GetUnprocessedChangeEvents().ForEach(ev => AddChild(ev.ToGameHistoryItem()));
    }

    private void OnGameChangeEventApplied(string changeEventName)
    {
        AddUnprocessedChangeEvents();
    }

    private void ScrollToBottom()
    {
        Callable.From(() => { ScrollContainer.ScrollVertical = (int)ScrollContainer.GetVScrollBar().MaxValue; }).CallDeferred();
    }

    private List<ChangeEvent> GetUnprocessedChangeEvents()
    {
        List<ChangeEvent> unprocessed = new();
        foreach (ChangeEvent ev in MultiplayerSession.Instance.GameState.GameChangeEvents)
        {
            if (!DisplayedChangeEventIds.Contains(ev.Id) && ev.ToHistoryItem)
            {
                unprocessed.Add(ev);
                DisplayedChangeEventIds.Add(ev.Id);
            }
        }
        return unprocessed;
    }


    public class GameHistoryRow
    {
        public string Text { get; set; }
        public Faction Faction { get; set; }
    }
}
