using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class RayTraceCaster : GodotObject
{
    private const float RAY_LENGTH = 5000f;

    private InputManager inputManager;
    private Camera2D Camera => inputManager.Camera;

    private List<Godot.Collections.Dictionary> currentRaycastCollisions = new();
    private List<Node3D> currentRaycastColliders = new();

    public RayTraceCaster(InputManager inputManager)
    {
        this.inputManager = inputManager;
    }       

    public static uint LayerToMask(int layerNumber)
    {
        return (uint)(1 << (layerNumber - 1));
    }


  
  // Optional: debug method (not used)
    private void DebugTargets(IEnumerable<Node3D> targets)
    {
        foreach (var target in targets)
        {
            if (target is RayTraceHandler handler)
            {
                // GD.Print($"Raycast target: {handler.ClickableSprite.Identifier}");
            }
            else
            {
                // GD.Print(target);
            }
        }
    }
}
