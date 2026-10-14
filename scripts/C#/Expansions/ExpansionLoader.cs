using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

/// <summary>
/// Loads each discovered expansion's code assembly, so an expansion can ship its rules and card classes
/// in its own DLL — in the DLC depot, next to its .pck — rather than inside the base game.
///
/// Three steps per assembly:
///   1. load it into the same AssemblyLoadContext as the game assembly, so its references to the game
///      (CardLogic, GameMessage, …) bind to the very types the game already loaded;
///   2. hand it to Godot (<see cref="Godot.Bridge.ScriptManagerBridge.LookupScriptsInAssembly"/>), which
///      registers its GodotObject subclasses as scripts — the same call Godot makes for the game's own
///      assembly at startup;
///   3. create its <see cref="IExpansionRules"/> and let them register their types.
///
/// Where the DLL is looked for: in an exported build, <c>&lt;install&gt;/dlc/</c>, which is where the DLC
/// depot puts it; anywhere else (editor, CLI, sim), next to the game assembly, where the main project's
/// build copies it.
///
/// Nothing here may take the base game down. A missing DLL marks the expansion unavailable; a DLL that
/// fails to load or register records the error on the expansion and is otherwise ignored.
/// </summary>
public static class ExpansionLoader
{
    public const string DlcFolderName = "dlc";

    /// <summary>The editor builds and runs the Debug configuration; Godot.NET.Sdk puts its output here.</summary>
    private const string DevBuildFolder = "res://.godot/mono/temp/bin/Debug";

    private static Assembly GameAssembly => typeof(CardLogic).Assembly;

    private static bool _loaded;
    private static bool _resolvingHooked;

    public static void LoadAll()
    {
        if (_loaded) return;
        _loaded = true;

        foreach (LoadedExpansion expansion in ExpansionRegistry.All)
        {
            if (!expansion.Manifest.HasAssembly) continue;

            try
            {
                Load(expansion);
            }
            catch (Exception e)
            {
                expansion.Error = $"{e.GetType().Name}: {e.Message}";
                DebugUtilities.PrintPeerError($"Expansions: '{expansion.Manifest.Id}' failed to load: {e}");
            }
        }
    }

    /// <summary>Where <paramref name="assemblyName"/>.dll is looked for, in order.</summary>
    public static List<string> CandidatePaths(string assemblyName)
    {
        string fileName = assemblyName + ".dll";
        List<string> paths = new();

        // "template" is set in exported builds only, never when running from the editor or `godot --path`.
        if (OS.HasFeature("template"))
        {
            paths.Add(Path.Combine(InstallRoot, DlcFolderName, fileName));
        }
        else
        {
            // Where the game's build puts it (target BuildExpansions). Not GameAssembly.Location: Godot
            // loads the game assembly from memory outside an export, so that is empty here.
            paths.Add(Path.Combine(ProjectSettings.GlobalizePath(DevBuildFolder), fileName));
        }

        return paths;
    }

    /// <summary>
    /// The folder the game is installed in: the exe's folder on Windows and Linux, the folder holding the
    /// .app bundle on macOS. DLC files go here rather than inside the bundle, which is signed.
    /// </summary>
    public static string InstallRoot
    {
        get
        {
            string executableFolder = Path.GetDirectoryName(OS.GetExecutablePath());
            DirectoryInfo folder = new(executableFolder);

            // <install>/Quartermaster General.app/Contents/MacOS/<executable>
            if (folder.Name == "MacOS" && folder.Parent?.Name == "Contents" && folder.Parent.Parent?.Extension == ".app")
                return folder.Parent.Parent.Parent!.FullName;

            return executableFolder;
        }
    }

    private static void Load(LoadedExpansion expansion)
    {
        string id = expansion.Manifest.Id;
        List<string> candidates = CandidatePaths(expansion.Manifest.Assembly);
        string path = candidates.FirstOrDefault(File.Exists);
        if (path == null)
        {
            expansion.Error = $"{expansion.Manifest.Assembly}.dll not found";
            DebugUtilities.PrintPeer($"Expansions: '{id}' unavailable, no {expansion.Manifest.Assembly}.dll in {string.Join(" or ", candidates)}");
            return;
        }

        AssemblyLoadContext context = AssemblyLoadContext.GetLoadContext(GameAssembly) ?? AssemblyLoadContext.Default;
        if (!_resolvingHooked)
        {
            context.Resolving += ResolveGameAssembly;
            _resolvingHooked = true;
        }

        Assembly assembly = context.LoadFromAssemblyPath(path);
        expansion.Assembly = assembly;
        expansion.AssemblyPath = path;
        DebugUtilities.PrintPeer($"Expansions: '{id}' loaded {assembly.GetName().Name} {assembly.GetName().Version} from {path}");

        Type[] types = ExportedTypes(assembly);
        CheckNoCoreNameIsShadowed(id, types);

        Godot.Bridge.ScriptManagerBridge.LookupScriptsInAssembly(assembly);
        TypeRegistry.AddAssembly(assembly);

        ExpansionRegistrar registrar = new(id);
        foreach (Type type in types.Where(t => typeof(IExpansionRules).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface))
        {
            IExpansionRules rules = (IExpansionRules)Activator.CreateInstance(type);
            if (rules.Id != id)
                throw new InvalidOperationException($"{type.Name} says it belongs to '{rules.Id}', but is in '{id}'s assembly");

            rules.Register(registrar);
            expansion.Rules.Add(rules);
        }

        if (expansion.Rules.Count == 0)
            throw new InvalidOperationException($"{assembly.GetName().Name} has no {nameof(IExpansionRules)} implementation");
    }

    /// <summary>
    /// An expansion is compiled against the game assembly and refers to it by name. The game assembly is
    /// already loaded in this context, but a context resolves names through its own rules first; answer
    /// with the loaded one so the expansion can never end up with a second copy of the game's types.
    /// </summary>
    private static Assembly ResolveGameAssembly(AssemblyLoadContext context, AssemblyName name)
        => name.Name == GameAssembly.GetName().Name ? GameAssembly : null;

    private static Type[] ExportedTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            // Almost always an expansion built against a different game version: name what is missing.
            string reasons = string.Join("; ", e.LoaderExceptions.Where(x => x != null).Select(x => x.Message).Distinct());
            throw new InvalidOperationException($"{assembly.GetName().Name} does not match this game version: {reasons}");
        }
    }

    /// <summary>
    /// Card classes live in the global namespace and are found by bare name, core first. An expansion type
    /// with a core type's name would therefore be silently ignored; refuse it loudly instead.
    /// </summary>
    private static void CheckNoCoreNameIsShadowed(string id, IEnumerable<Type> types)
    {
        List<string> shadowed = types
            .Where(t => t.Namespace == null && !t.IsNested && !t.Name.Contains('<') && GameAssembly.GetType(t.FullName!) != null)
            .Select(t => t.Name)
            .ToList();

        if (shadowed.Count > 0)
            throw new InvalidOperationException($"'{id}' defines types the game already has: {string.Join(", ", shadowed)}");
    }
}
