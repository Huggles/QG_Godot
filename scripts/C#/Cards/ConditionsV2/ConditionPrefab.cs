using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

public abstract class ConditionPrefab : ConditionBase
{
    [JsonPropertyName("name")]
    public string Name { get; set; }
}
