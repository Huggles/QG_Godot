using Godot;
using Godot.Collections;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using Metalama.Patterns.Observability;

[Observable]
public partial class MultiplayerSession : Node
{
    [Export]
    public string ConfigurationJson {get; set;}

    [Export]
    public string GameStateJson {get; set;}


    public TestPayload GameState {get; set;}

    private readonly DeepPropertyObserver _gameStateObserver;

    public MultiplayerSession()
    {
        _gameStateObserver = new DeepPropertyObserver(OnGameStateDeepChanged);
    }

    public override void _Ready()
    {   
        this.PropertyChanged += OnPropertyChanged;                    
        DebugUtilities.PrintPeer($"MultiplayerSession ready", DebugVerbosity.INFO);
    }

    public override void _EnterTree()
    {
        base._EnterTree();
        DebugUtilities.PrintPeer($"MultiplayerSession entered tree", DebugVerbosity.INFO);
    }

    public void Start(string configuration)
    {        
        if(Multiplayer.IsServer())
        {            
            this.ConfigurationJson = configuration;
            DebugTimer();
        };     
    }
    
    public async void DebugTimer()
    { 
        await Task.Delay(500);
        GameState = new TestPayload() { Value = 0, Text = "Initial"};
        _gameStateObserver.Observe(GameState);

        await Task.Delay(1000);
        GameState.Value = 1;

        await Task.Delay(1000);
        GameState.Nested = new TestPayload.TestPayload1() { FloatValue = 0.0f };


        await Task.Delay(1000);
        GameState.Nested.FloatValue = 1.0f;
        
    }

    private void OnGameStateDeepChanged(object sender, PropertyChangedEventArgs e)
    {
        DebugUtilities.PrintPeer($"[Deep] {sender.GetType().Name}.{e.PropertyName} changed", DebugVerbosity.INFO);
        HandleGameStateChange();
    }

    private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        DebugUtilities.PrintPeer($"Property {e.PropertyName} {sender.GetType()}", DebugVerbosity.INFO);
        DebugUtilities.PrintPeer($"Property {e.PropertyName} changed to: {GetType().GetProperty(e.PropertyName)?.GetValue(this)}", DebugVerbosity.INFO);
        if(sender.GetType() == GameState?.GetType() || GetType().GetProperty(e.PropertyName)?.Name == nameof(GameState))
        {
            HandleGameStateChange();
        }
        else if(GetType().GetProperty(e.PropertyName)?.Name == nameof(GameStateJson))
        {
            HandleGameStateJsonChange();
        }

        else if(GetType().GetProperty(e.PropertyName)?.Name == nameof(ConfigurationJson))
        {
            HandleConfigurationJsonChange();
        }        
    }

    public void HandleGameStateChange()
    {
        if(!Multiplayer.IsServer())
            {
                DebugUtilities.PrintPeer($"GameState changed on client, ignoring", DebugVerbosity.INFO);
                return;
            }
            DebugUtilities.PrintPeer($"GameState changed", DebugVerbosity.INFO);
            GameStateJson = JsonSerializer.Serialize(GameState);
    }

    public void HandleGameStateJsonChange()
    {
        DebugUtilities.PrintPeer($"GameStateJson changed", DebugVerbosity.INFO);
        if (!Multiplayer.IsServer())
        {
            DebugUtilities.PrintPeer($"Parsing GameStateJson", DebugVerbosity.INFO);
            GameState = JsonSerializer.Deserialize<TestPayload>(GameStateJson);
            _gameStateObserver.Observe(GameState); // resubscribe to new deserialized graph
            DebugUtilities.PrintPeer($"New GameState: {JsonSerializer.Serialize(GameState)}", DebugVerbosity.INFO);
        }       
    }

    public void HandleConfigurationJsonChange()
    {
        DebugUtilities.PrintPeer($"ConfigurationJson changed", DebugVerbosity.INFO);
    }
}
