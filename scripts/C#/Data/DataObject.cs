using Godot;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;

public partial class DataObject : IGameData
{
    public string Stringify()
    {
        return JsonSerializer.Serialize(this);
    }
    public object GetValue(string key)
    {
        if (GetType().GetProperty(key) != null)
        {
            return GetType().GetProperty(key).GetValue(this);
        }
        return null;
    }
    public virtual Task LoadData()
    {
        return null;
    }

    // public static T ParseString<T>(string jsonString) where T : DataObject
    // {
    //     T parsedDataObject = JsonSerializer.Deserialize<T>(jsonString);
    //     return parsedDataObject;
    // }

    // public static T ParseDictionary<T>(Dictionary<string, object> jsonDictionary) where T : DataObject, new()
    // {
    //     T dataObject = new();
    //     Type dataObjectType = typeof(T);
    //     dataObject.FromDictionary(jsonDictionary, dataObjectType, dataObject);       
    //     return dataObject;
    // }

    // public void FromDictionary(Dictionary<string, object> jsonDictionary) {
    //     FromDictionary(jsonDictionary, GetType(), this);        
    // }

    // private void FromDictionary(Dictionary<string, object> jsonDictionary, Type dataObjectType, DataObject dataObject){
    //     foreach (string key in jsonDictionary.Keys){
    //         object value = jsonDictionary[key];
    //         if (dataObjectType.GetProperty(key) != null){
    //             if(value is Dictionary<string,object>){  
    //                 Dictionary<string,object> childDictionary = (Dictionary<string,object>)value;
    //                 string dataClass = childDictionary["DataClass"].ToString();
    //                 Type childObjectType = Type.GetType(dataClass);
    //                 DataObject childObject = (DataObject)Activator.CreateInstance(childObjectType);
    //                 childObject.FromDictionary(childDictionary);
    //                 dataObjectType.GetProperty(key).SetValue(dataObject, childObject, null);
    //             } else {
    //                 PropertyInfo propertyInfo = dataObjectType.GetProperty(key);
    //                 propertyInfo.SetValue(dataObject, value, null);
    //             }
    //         }            
    //     }
    // }
}
