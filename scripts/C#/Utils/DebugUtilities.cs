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
    
    private static DebugVerbosity CurrentVerbosity =>
        GameSettings.Instance != null ? GameSettings.Debug : DebugVerbosity.FINEST;

    public static void PrintPeerError(String message){
        GD.PrintErr("("+ FormattedDateTime +")"+" Server: " + message);
    }

    /// <summary>Prints when the current debug level is >= the specified level. Defaults to FINEST.</summary>
    /// 
    public static void PrintPeer(Object o, DebugVerbosity level = DebugVerbosity.FINEST){
        if (CurrentVerbosity < level) {
            return;
        }
        if(o == null)
        {
            o = "null";
        }
        PrintPeer(o.ToString(), level);
    }

    public static void PrintPeer(String message, DebugVerbosity level = DebugVerbosity.FINEST){
        if (CurrentVerbosity >= level){
            GD.Print($"({FormattedDateTime}) [{level}]: {message}");
        }
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
