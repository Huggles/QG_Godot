using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class PresentationItemImageButtonControl : Control
{    
    public TextureRect ImageControl;
    public Button Button;

    public override void _Ready()
    {

        Button = GetNode<Button>("%Button");
        ImageControl = GetNode<TextureRect>("%TextureRect");
    }
}
