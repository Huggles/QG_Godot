using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;

public partial class DebugUtilities : Node
{
    public static Dictionary<String,Object> CommandLineArguments => ParseCommandLineArguments();

    // A headless dedicated server is the authority (peer 1) but controls no faction, so it has no
    // local PlayerScene — label it [SERVER] directly instead of falling through to [UNKNOWN].
    public static string InstancePrefix => GameContext.IsHeadless ? "[SERVER]"
        : PlayerScene.Current != null ? (PlayerScene.Current.GetMultiplayerAuthority() == 1 ? "[SERVER]" : "[CLIENT]") : "[UNKNOWN]";

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

    /// <summary>
    /// Prints without BBCode parsing. Use this for anything containing a stack trace or arbitrary
    /// exception text: <see cref="PrintPeerError"/> goes through <c>GD.PrintRich</c>, which parses
    /// BBCode and silently eats the square brackets that .NET traces are full of ([T] generic
    /// frames, [0x0001a] IL offsets, [] array notation) — and an unbalanced tag leaves every
    /// subsequent console line coloured. Also raises a Godot error so it appears in the editor's
    /// Errors tab with a source location.
    /// </summary>
    public static void PrintPeerErrorRaw(String message){
        string prefix = $"{InstancePrefix}({FormattedDateTime}) [ERROR]: ";
        GD.PrintErr($"{prefix}{message}");

        // PushError gets only the first line: it surfaces in the editor's Errors tab, and pushing the
        // whole multi-line trace there would duplicate everything PrintErr already wrote.
        int firstBreak = message?.IndexOf('\n') ?? -1;
        string summary = firstBreak > 0 ? message.Substring(0, firstBreak) : message;
        GD.PushError($"{prefix}{summary}");
    }

    /// <summary>
    /// Prints <paramref name="message"/> normally; prints <paramref name="finestMessage"/> instead when verbosity is FINEST.
    /// </summary>
    public static void PrintPeer(Object o, Object oFinest = null){
        PrintPeer(o?.ToString() ?? "null", oFinest?.ToString() ?? "null");
        
    }
   
    public static void PrintPeer(string message, string finestMessage = null){
        if(CurrentVerbosity == DebugVerbosity.INFO && message == null) return;

        string text = (CurrentVerbosity == DebugVerbosity.FINEST && !string.IsNullOrEmpty(finestMessage)) ? finestMessage : message;
        GD.PrintRich($"{InstancePrefix}[color={"green"}]({FormattedDateTime})]: {text}[/color]");
    }

    public static void PrintPeerFinest(string message){
        PrintPeer(null, message);
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
