using Godot;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

public class ConditionParser
{
    // public override IConditionV2 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    // {
    //     using var doc = JsonDocument.ParseValue(ref reader);
    //     JsonElement root = doc.RootElement;
    //     JsonElement conditionTypeElement;
    //     if (root.TryGetProperty("type", out conditionTypeElement))
    //     {
    //         string conditionTypeString = conditionTypeElement.ToString().ToUpper();
    //         ConditionType conditionType;
    //         if (!Enum.TryParse(conditionTypeString, out conditionType))
    //         {
    //             throw new NotImplementedException($"Could not parse condition type {conditionTypeString}");
    //         }
    //         return conditionType switch
    //         {
    //             ConditionType.CONDITION => JsonSerializer.Deserialize<ConditionV2>(root),
    //             ConditionType.GROUP => JsonSerializer.Deserialize<ConditionGroup>(root),
    //             ConditionType.PREFAB => (ConditionPrefab)JsonSerializer.Deserialize(root, GetConditionPrefabType(root)),
    //             _ => throw new NotImplementedException($"Could not parse condition type {conditionTypeString}"),
    //         };
    //     }
    //     else
    //     {
    //         throw new NotImplementedException($"Missing condition type");
    //     }
    // }

    // public override void Write(Utf8JsonWriter writer, IConditionV2 value, JsonSerializerOptions options)
    // {
    //     JsonSerializer.Serialize(writer, value, options);
    // }
    
    // private Type GetConditionPrefabType(JsonElement jsonElement)
    // {
    //     JsonElement prefabNameElement;
    //     if(jsonElement.TryGetProperty("name", out prefabNameElement))
    //     {
    //         string prefabName = prefabNameElement.ToString();
    //         string prefabClassName = "ConditionPrefab" + prefabName.ToPascalCase();
    //         Type ConditionPrefabType = Type.GetType(prefabClassName);
    //         if(ConditionPrefabType == null)
    //         {
    //             throw new NotImplementedException($"Could not parse prefab name {prefabClassName}");
    //         }
    //         return ConditionPrefabType;
    //     } else
    //     {
    //         throw new NotImplementedException($"Could not parse prefab name");
    //     }        
    // }
}
