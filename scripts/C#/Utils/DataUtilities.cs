using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;

public partial class DataUtilities : Object
{
    public static System.Collections.Generic.Dictionary<String, ClassItem> ClassMap {
        get {
            if (ClassMap == null || ClassMap.Keys.Count == 0) {
                var classList = ProjectSettings.GetGlobalClassList();
                foreach (Godot.Collections.Dictionary classDictionaryItem in classList) {
                    ClassItem classItem = new ClassItem(
                        classDictionaryItem["class"].ToString(), 
                        classDictionaryItem["base"].ToString(), 
                        classDictionaryItem["path"].ToString()
                        );
                    ClassMap[classItem.Name] = classItem;
                }
            }
            return ClassMap;
        }
    }

    public class ClassItem {
        public String Name;
	    public String BaseClass;
	    public String Path;
	
	    public ClassItem(String name, String baseClass, String path){
            this.Name = name;
		    this.BaseClass = baseClass;
		    this.Path = path;
        }
    }
	
}
