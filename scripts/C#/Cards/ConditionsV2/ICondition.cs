using Godot;
using System;
using System.Text.Json.Serialization;



[JsonPolymorphic(TypeDiscriminatorPropertyName = "type",
    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToNearestAncestor)]
[JsonDerivedType(typeof(ConditionV2), "CONDITION")]
[JsonDerivedType(typeof(ConditionGroup), "GROUP")]
[JsonDerivedType(typeof(ConditionPrefabAlways), "PREFAB_ALWAYS")]
[JsonDerivedType(typeof(ConditionPrefabCanBuild), "PREFAB_CANBUILD")]
public interface ICondition
{
    bool MeetCondition();
}
