using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;

public partial class DebugUtilities : Node
{
    public static Dictionary<String,Object> CommandLineArguments => ParseCommandLineArguments();

    public static string InstancePrefix => PlayerScene.Current != null ? (PlayerScene.Current.GetMultiplayerAuthority() == 1 ? "[SERVER]" : "[CLIENT]") : "[UNKNOWN]";

    public static string FormattedDateTime {
        get {
            return Time.GetTimeStringFromSystem();

        }
    }
    
    private static DebugVerbosity CurrentVerbosity =>
        GameSettings.Instance != null ? GameSettings.Debug : DebugVerbosity.FINEST;

    public static void PrintPeerError(String message){
        GD.PrintRich($"{InstancePrefix}[color=red]({FormattedDateTime}) [ERROR]: {message}[/color]");
    }

    /// <summary>Prints when the current debug level is >= the specified level. Defaults to FINEST.</summary>
    /// 
    public static void PrintPeer(Object o){
        if(o == null)
        {
            o = "null";
        }
        PrintPeer(o.ToString());
    }

    public static void PrintPeer(String message){
        GD.PrintRich($"{InstancePrefix}[color={"green"}]({FormattedDateTime})]: {message}[/color]");
        
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
