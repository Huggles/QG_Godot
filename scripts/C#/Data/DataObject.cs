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

    public virtual void LoadData() { }
}
