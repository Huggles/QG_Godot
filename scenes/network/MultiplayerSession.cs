using Godot;
using Godot.Collections;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;

public partial class MultiplayerSession : Node, INotifyPropertyChanged
{

    public string TestProperty {get; set;}

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    public void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        DebugUtilities.PrintPeer($"Property changed: {e.PropertyName}");
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public override void _Ready()
    {
        
        DebugUtilities.PrintPeer($"MultiplayerSession ready: " + this.Name, DebugVerbosity.INFO);
        if(Multiplayer.IsServer())
        {       
            GameState gameStateInstance = new GameState();            
            gameStateInstance.SetMultiplayerAuthority(1); // host is authority  
            this.AddChild(gameStateInstance, true);   
            DebugUtilities.PrintPeer($"GameState created and added to scene tree", DebugVerbosity.INFO);    
            TestProperty = "Initial Value";

        };
        
    }
    public override void _EnterTree()
    {
        base._EnterTree();
        DebugUtilities.PrintPeer($"MultiplayerSession entered tree", DebugVerbosity.INFO);
    }
}
