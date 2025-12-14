using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

public class ConditionV2 : ConditionBase
{
    [JsonPropertyName("field")]
    public string Field { get; set; }

    [JsonPropertyName("operator")]
    public string Operator { get; set; }

    [JsonPropertyName("value")]
    public object Value { get; set; }

    public override bool MeetCondition()
    {
        throw new NotImplementedException("");
    }


}
