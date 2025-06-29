using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public static class ConditionExtensions
{
    public static bool AllTrue(this List<Condition> conditions)
    {
        return conditions.All(condition=>condition.MeetCondition());
    }
    public static bool AnyTrue(this List<Condition> conditions)
    {
        return conditions.Any(condition=>condition.MeetCondition());
    }   
}
