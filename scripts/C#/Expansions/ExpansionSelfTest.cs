using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Loader;
using System.Text.Json;

/// <summary>
/// <c>-- expansion_selftest=true</c>: checks every expansion this build found can actually run here, then
/// quits with exit code 0 (all checks passed) or 1. For an exported build with its DLC in place, and after
/// a Godot upgrade (ExpansionLoader leans on an undocumented Godot call).
///
/// Generic on purpose: it knows no expansion's types, only what each one registered, so it keeps working
/// as expansions grow and never references their code, which core must not do.
/// </summary>
public static class ExpansionSelfTest
{
    public const string Arg = "expansion_selftest";

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

        Check("expansions found", () =>
        {
            Require(ExpansionRegistry.All.Count > 0, $"none under {ExpansionRegistry.Root}");
            return string.Join(", ", ExpansionRegistry.All.Select(e => e.Manifest.Id));
        });

        foreach (LoadedExpansion expansion in ExpansionRegistry.All)
            CheckExpansion(expansion, Check);

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
                 $"(game {BuildInfo.Version}, {(OS.HasFeature("template") ? "exported build" : "editor/dev build")}, " +
                 $"install root {ExpansionLoader.InstallRoot})");
        tree.Quit(allPassed ? 0 : 1);
    }

    private static void CheckExpansion(LoadedExpansion expansion, Action<string, Func<string>> check)
    {
        string id = expansion.Manifest.Id;

        check($"{id}: loaded", () =>
        {
            Require(expansion.IsLoaded, expansion.Error ?? "not loaded");
            return expansion.Manifest.HasAssembly ? expansion.AssemblyPath : "content only";
        });

        if (!expansion.Manifest.HasAssembly || !expansion.IsLoaded) return;

        check($"{id}: assembly shares the game's load context", () =>
        {
            AssemblyLoadContext gameContext = AssemblyLoadContext.GetLoadContext(typeof(CardLogic).Assembly);
            AssemblyLoadContext expansionContext = AssemblyLoadContext.GetLoadContext(expansion.Assembly);
            Require(gameContext == expansionContext, $"game in '{gameContext?.Name}', expansion in '{expansionContext?.Name}'");
            return $"'{gameContext?.Name}'";
        });

        check($"{id}: rules registered", () =>
        {
            Require(expansion.Rules.Count > 0, "no IExpansionRules");
            return string.Join(", ", expansion.Rules.Select(rules => rules.GetType().Name));
        });

        check($"{id}: messages round-trip", () =>
        {
            List<(Type DtoType, string Discriminator)> messages = GameMessageJson.RegisteredMessages(id);
            foreach ((Type dtoType, string discriminator) in messages)
            {
                GameMessageDto sent = (GameMessageDto)Activator.CreateInstance(dtoType);
                string json = JsonSerializer.Serialize(sent, typeof(GameMessageDto), GameMessageJson.Options);
                Require(json.Contains($"\"$type\":\"{discriminator}\""), $"no discriminator in {json}");

                GameMessageDto received = JsonSerializer.Deserialize<GameMessageDto>(json, GameMessageJson.Options);
                Require(received?.GetType() == dtoType, $"{discriminator} came back as {received?.GetType().Name}");
                Require(GameMessageJson.TryCreateExpansionMessage(received, out GameMessage message), $"{discriminator} has no factory");
                message.Free();
            }
            return messages.Count == 0 ? "none registered" : string.Join(", ", messages.Select(m => m.Discriminator));
        });
    }

    private static void Require(bool condition, string failure)
    {
        if (!condition) throw new InvalidOperationException(failure);
    }
}
