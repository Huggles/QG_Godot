using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public static class ConditionExtensions
{
    public static bool AllTrue(this List<Condition> conditions, GameSituation situation)
    {
        return conditions.All(condition=>condition.MeetCondition(situation));
    }
    public static bool AnyTrue(this List<Condition> conditions, GameSituation situation)
    {
        return conditions.Any(condition=>condition.MeetCondition(situation));
    }   
}
