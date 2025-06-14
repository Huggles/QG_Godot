using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class RayTraceCaster : GodotObject
{
    private const float RAY_LENGTH = 5000f;

    private InputManager inputManager;
    private Camera3D Camera => inputManager.Camera;

    private List<Godot.Collections.Dictionary> currentRaycastCollisions = new();
    private List<Node3D> currentRaycastColliders = new();

    public RayTraceCaster(InputManager inputManager)
    {
        this.inputManager = inputManager;
    }

    public void CastRays(InputEventMouse mouseEvent)
    {
        
        var results = ShootRays();
        var resultColliders = new List<object>();
        foreach (var result in results)
        {
            Node3D colliderNode = result["collider"].As<Node3D>();
            resultColliders.Add(colliderNode);
        }

        var exitedResults = new List<Godot.Collections.Dictionary>();
        var enteredResults = new List<Godot.Collections.Dictionary>();

        foreach (var current in currentRaycastCollisions)
        {
            Node3D colliderNode = current["collider"].As<Node3D>();
            if (!resultColliders.Contains(colliderNode))
            {
                exitedResults.Add(current);
            }
        }

        foreach (var result in results)
        {
            Node3D colliderNode = result["collider"].As<Node3D>();
            if (!currentRaycastColliders.Contains(colliderNode))
            {
                enteredResults.Add(result);
            }
        }

        if (enteredResults.Count > 0)
        {
            DebugUtilities.PrintPeer("enteredResults");
            DebugUtilities.PrintPeer(enteredResults.Count);
            foreach (var result in enteredResults)
            {
                Node3D colliderNode = result["collider"].As<Node3D>();
                DebugUtilities.PrintPeer(colliderNode.GetParent<ClickableSprite3D>().Identifier);

            }
        }

        currentRaycastCollisions.Clear();
        currentRaycastColliders.Clear();

        foreach (var result in results)
        {
            currentRaycastCollisions.Add(result);
            
            Node3D colliderNode = result["collider"].As<Node3D>();
            currentRaycastColliders.Add(colliderNode);
        }

        foreach (var entered in enteredResults)
        {
            Node3D colliderNode = entered["collider"].As<Node3D>();            
            if (colliderNode is RayTraceHandler rth)
            {
                rth.OnStartHit(Camera, mouseEvent, (Vector3)entered["position"], (Vector3)entered["normal"]);
            }
        }

        foreach (var exited in exitedResults)
        {
            Node3D colliderNode = exited["collider"].As<Node3D>();                        
            if (colliderNode is RayTraceHandler rth)
            {
                rth.OnStopHit(Camera, mouseEvent, (Vector3)exited["position"], (Vector3)exited["normal"]);
            }
        }

        foreach (var current in currentRaycastCollisions)
        {
            Node3D colliderNode = current["collider"].As<Node3D>();                                    
            if (colliderNode is RayTraceHandler rth)
            {
                rth.OnHitting(Camera, mouseEvent, (Vector3)current["position"], (Vector3)current["normal"]);
            }
        }
    }

    private Godot.Collections.Array<Godot.Collections.Dictionary> ShootRays()
    {
        
        var spaceState = Camera.GetWorld3D().DirectSpaceState;
        var from = Camera.ProjectRayOrigin(inputManager.MousePosition);
        var to = from + Camera.ProjectRayNormal(inputManager.MousePosition) * RAY_LENGTH;
        DebugDraw3D.DrawLine(from, to, new Color(255,0,0), 1);

        var results = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        var collidersToIgnore = new Godot.Collections.Array<Rid>();

        while (true)
        {
            var query = PhysicsRayQueryParameters3D.Create(from, to, LayerToMask(4), collidersToIgnore);            
            query.CollideWithAreas = true;

            var result = spaceState.IntersectRay(query);
            if (result.Count == 0)
                break;

            results.Add(result);
            Node3D colliderNode = result["collider"].As<Node3D>();
            if (colliderNode is CollisionObject3D collisionObject3D)
            {
                collidersToIgnore.Add(collisionObject3D.GetRid());
            }
        }
        return results;
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
