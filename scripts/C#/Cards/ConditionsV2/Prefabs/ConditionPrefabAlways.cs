using Godot;
using System;
public class ConditionPrefabAlways : ConditionPrefab
{
    public override bool MeetCondition()
    {
        return true;
    }        
}
