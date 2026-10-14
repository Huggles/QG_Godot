using Godot;
using System;
using System.Collections.Generic;
using System.Runtime.Loader;
using System.Text.Json;

/// <summary>
/// <c>-- expansion_selftest=true</c>: proves an expansion's code assembly works in this build, then quits
/// with exit code 0 (all checks passed) or 1. Exists for the Total War spike (can a separate DLL ship in
/// the DLC depot?), and stays useful afterwards for checking an exported build with its DLC in place.
///
/// Expansion types are reached by name only, through <see cref="TypeRegistry"/>: core never references
/// an expansion's types, and this test must not either, or it would prove nothing.
/// </summary>
public static class ExpansionSelfTest
{
    public const string Arg = "expansion_selftest";

    private const string ExpansionId = "total_war";
    private const string CardClass = "SpikeCard";
    private const string MessageClass = "SpikePingPresentationEvent";
    private const string MessageDiscriminator = "TW.SpikePing";

    public static bool Requested => CliArgs.GetBool(Arg);

    public static void RunAndQuit(SceneTree tree)
    {
        List<(string Name, bool Passed, string Detail)> results = new();

        void Check(string name, Func<string> body)
        {
            try
            {
                results.Add((name, true, body()));
            }
            catch (Exception e)
            {
                results.Add((name, false, $"{e.GetType().Name}: {e.Message}"));
            }
        }

        LoadedExpansion expansion = ExpansionRegistry.Find(ExpansionId);

        Check("manifest discovered, assembly found", () =>
        {
            Require(expansion != null, $"no '{ExpansionId}' under {ExpansionRegistry.Root}");
            Require(expansion.AssemblyPath != null,
                $"{expansion.Error} (looked in {string.Join(", ", ExpansionLoader.CandidatePaths(expansion.Manifest.Assembly))})");
            return expansion.AssemblyPath;
        });

        Check("assembly loaded next to the game assembly", () =>
        {
            Require(expansion?.Assembly != null, expansion?.Error ?? "not loaded");
            AssemblyLoadContext gameContext = AssemblyLoadContext.GetLoadContext(typeof(CardLogic).Assembly);
            AssemblyLoadContext expansionContext = AssemblyLoadContext.GetLoadContext(expansion.Assembly);
            Require(gameContext == expansionContext, $"game in '{gameContext?.Name}', expansion in '{expansionContext?.Name}'");
            Require(expansion.IsAvailable, expansion.Error);
            return $"context '{gameContext?.Name}', {expansion.Rules.Count} rules class(es)";
        });

        Type cardType = null;
        Check($"TypeRegistry resolves {CardClass} to a CardLogic", () =>
        {
            cardType = TypeRegistry.Resolve(CardClass);
            Require(cardType != null, "not found");
            Require(typeof(CardLogic).IsAssignableFrom(cardType), $"{cardType.FullName} is not a CardLogic");
            Require(cardType.Assembly == expansion?.Assembly, $"resolved from {cardType.Assembly.GetName().Name}");
            return cardType.AssemblyQualifiedName;
        });

        Check("Godot sees the card as a script", () =>
        {
            Require(cardType != null, "no card type");
            CardLogic card = (CardLogic)Activator.CreateInstance(cardType);
            try
            {
                Script script = card.GetScript().As<Script>();
                Require(script != null, "instance has no script attached");
                string echoed = card.Call("SpikeEcho", "x").AsString();
                Require(echoed == "x", $"SpikeEcho through Godot returned '{echoed}'");
                return $"script '{script.ResourcePath}', Call(\"SpikeEcho\") ok";
            }
            finally
            {
                card.Free();
            }
        });

        Check("card OnActivate runs", () =>
        {
            Require(cardType != null, "no card type");
            CardLogic card = (CardLogic)Activator.CreateInstance(cardType);
            try
            {
                return $"{card.OnActivate().Count} step(s)";
            }
            finally
            {
                card.Free();
            }
        });

        Check($"expansion message round-trips as '{MessageDiscriminator}'", () =>
        {
            Type messageType = TypeRegistry.Resolve(MessageClass);
            Require(messageType != null, $"{MessageClass} not found");
            GameMessage sent = (GameMessage)Activator.CreateInstance(messageType, Faction.UNITED_KINGDOM, "ping");
            try
            {
                string json = JsonSerializer.Serialize(sent.ToDto(), typeof(GameMessageDto), GameMessageJson.Options);
                Require(json.Contains($"\"$type\":\"{MessageDiscriminator}\""), $"no discriminator in {json}");

                GameMessageDto dto = JsonSerializer.Deserialize<GameMessageDto>(json, GameMessageJson.Options);
                GameMessage received = GameMessage.FromDto(dto);
                try
                {
                    Require(received.GetType() == messageType, $"came back as {received.GetType().Name}");
                    Require(received.SummaryText() == "ping" && received.TriggeringFaction == Faction.UNITED_KINGDOM,
                        $"came back as '{received.SummaryText()}' from {received.TriggeringFaction}");
                    return json;
                }
                finally
                {
                    received.Free();
                }
            }
            finally
            {
                sent.Free();
            }
        });

        Check("core card and message unchanged", () =>
        {
            Type buildArmy = TypeRegistry.Resolve("BuildArmy");
            Require(buildArmy != null && buildArmy.Assembly == typeof(CardLogic).Assembly, "BuildArmy no longer resolves to the core type");

            ChangeStepChangeEventDto step = new() { Id = 7, NewStep = TurnStep.SUPPLY };
            string viaDefault = JsonSerializer.Serialize(step, typeof(GameMessageDto));
            string viaShared = JsonSerializer.Serialize(step, typeof(GameMessageDto), GameMessageJson.Options);
            Require(viaDefault == viaShared, $"wire format changed:\n  {viaDefault}\n  {viaShared}");

            GameMessageDto back = JsonSerializer.Deserialize<GameMessageDto>(viaShared, GameMessageJson.Options);
            Require(back is ChangeStepChangeEventDto { NewStep: TurnStep.SUPPLY, Id: 7 }, "ChangeStep did not round-trip");
            return viaShared;
        });

        int passed = 0;
        foreach ((string name, bool ok, string detail) in results)
        {
            if (ok) passed++;
            GD.Print($"[expansion selftest] {(ok ? "PASS" : "FAIL")}  {name}: {detail}");
        }

        bool allPassed = passed == results.Count;
        GD.Print($"[expansion selftest] {(allPassed ? "PASS" : "FAIL")} {passed}/{results.Count} " +
                 $"({(OS.HasFeature("template") ? "exported build" : "editor/dev build")}, install root {ExpansionLoader.InstallRoot})");
        tree.Quit(allPassed ? 0 : 1);
    }

    private static void Require(bool condition, string failure)
    {
        if (!condition) throw new InvalidOperationException(failure);
    }
}
