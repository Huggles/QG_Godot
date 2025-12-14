using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
public class ConditionGroup : ConditionBase
{
    [JsonPropertyName("conditions")]
    public List<ICondition> Conditions { get; set; }

    [JsonPropertyName("logic")]
    public string Logic { get; set; }

    public override bool MeetCondition()
    {
        return Logic switch
        {
            "AND" => Conditions.All(r => r.MeetCondition()),
            "OR" => Conditions.Any(r => r.MeetCondition()),
            _ => false
        };
    }
}
