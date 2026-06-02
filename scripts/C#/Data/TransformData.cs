using Godot;
using System;

public partial class TransformData : DataObject
{
        public float Scale { get; set; }
        public float XPosition { get; set; }
        public float YPosition { get; set; }
        public float ZPosition { get; set; }
        public float XRotation { get; set; }
        public float YRotation { get; set; }
        public float ZRotation { get; set; }

        public Vector3 Position => new Vector3(XPosition, YPosition, ZPosition);    
}
