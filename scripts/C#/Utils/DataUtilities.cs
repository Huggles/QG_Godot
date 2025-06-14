using Godot;
using System;
using System.Collections.Generic;

public partial class DataUtilities : Object
{
    private static Dictionary<String, ClassItem> classMap;
    public static Dictionary<String, ClassItem> ClassMap {
        get {
            if (classMap == null || classMap.Keys.Count == 0) {
                classMap = new Dictionary<String, ClassItem>();
                Godot.Collections.Array<Godot.Collections.Dictionary> classList = ProjectSettings.GetGlobalClassList();
                DebugUtilities.PrintPeer(Json.Stringify(classList));
                foreach (Godot.Collections.Dictionary classDictionaryItem in classList) {
                    
                    ClassItem classItem = new ClassItem(
                        classDictionaryItem["class"].ToString(), 
                        classDictionaryItem["base"].ToString(), 
                        classDictionaryItem["path"].ToString(),
                        classDictionaryItem["language"].ToString()
                        );
                    classMap[classItem.Name+"."+classItem.Language] = classItem;
                }
            }
            return classMap;
        }
    }

    public class ClassItem {
        public String Name;
	    public String BaseClass;
        public String Language;
	    public String Path;
	
	    public ClassItem(String name, String baseClass, String path, String language){
            this.Name = name;
		    this.BaseClass = baseClass;
		    this.Path = path;
            this.Language = language;
        }
    }
	
}
