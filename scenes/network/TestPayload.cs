using Godot;
using Metalama.Patterns.Observability;
using System;
using System.ComponentModel;
using System.Text.Json;

[Observable]
public partial class TestPayload
{
    public int Value { get; set; }

    public string Text { get; set; }

    public TestPayload1 Nested { get; set; }
    
    [Observable]
    public partial class TestPayload1
    {
        public float FloatValue { get; set; }
        
    }
}
