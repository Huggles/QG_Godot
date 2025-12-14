using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;

public partial class DebugUtilities : Node
{
    public static Dictionary<String,Object> CommandLineArguments => ParseCommandLineArguments();

    public static string FormattedDateTime {
        get {
            return Time.GetTimeStringFromSystem();

        }
    }
    
    public static void PrintPeerError(String message){
        GD.PrintErr("("+ FormattedDateTime +")"+" Server: " + message);
    }

    public static void PrintPeer(String message){
        GD.Print("("+ FormattedDateTime +")"+" Server: " + message);
    }
    
    public static void PrintPeer(Object o){
        if(o == null)
        {
            o = "null";
        }
        GD.Print("("+ FormattedDateTime +")"+" Server: " + o.ToString());
    }

    private static Dictionary<String, Object> ParseCommandLineArguments()
    {
        Dictionary<String, Object> keyValuePairs = new Dictionary<String, Object>();

        foreach (String argument in OS.GetCmdlineArgs())
        {
            if (argument.Contains("--"))
            {
                if (argument.Contains("="))
                {
                    string key = argument.Split("=")[0];
                    string value = argument.Split("=")[1];

                    keyValuePairs.Add(key.TrimPrefix("--"), value);
                }
                else
                {
                    keyValuePairs.Add(argument.TrimPrefix("--"), true);
                }
            }
        }
        return keyValuePairs;
    }
}
