using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;


public abstract class ConditionBase : ICondition
{
    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("params")]
    public ConditionInput Parameters { get; set; }

    public abstract bool MeetCondition();

}

