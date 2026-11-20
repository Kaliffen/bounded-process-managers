using System.Reflection;
using System.Runtime.Loader;
using Bpm.Compiler;
using Bpm.Dsl;
using Bpm.Generation;

// bpm compile <assembly.dll> [--out <dir>] [--check]
//
// Loads an assembly, finds every static member marked [BoundedProcess] that yields a
// ProcessDefinition, compiles it, writes generated artefacts, and exits non-zero on any error so
// that an MSBuild target can turn DSL violations into build failures.
//   --check   do not write; fail if generated artefacts differ from those on disk (CI drift gate).

if (args.Length < 2 || args[0] != "compile")
{
    Console.Error.WriteLine("usage: bpm compile <assembly.dll> [--out <dir>] [--check]");
    return 2;
}

var assemblyPath = Path.GetFullPath(args[1]);
var outDir = Arg("--out") ?? Path.Combine(Path.GetDirectoryName(assemblyPath)!, "bpm");
var check = args.Contains("--check");

var dir = Path.GetDirectoryName(assemblyPath)!;
AssemblyLoadContext.Default.Resolving += (ctx, name) =>
{
    var candidate = Path.Combine(dir, name.Name + ".dll");
    return File.Exists(candidate) ? ctx.LoadFromAssemblyPath(candidate) : null;
};
var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);

var definitions = new List<(string Origin, ProcessDefinition Def)>();
foreach (var type in assembly.GetTypes())
foreach (var member in type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly))
{
    if (member.GetCustomAttribute<BoundedProcessAttribute>() is null) continue;
    object? value = member switch
    {
        PropertyInfo p => p.GetValue(null),
        FieldInfo f => f.GetValue(null),
        MethodInfo m when m.GetParameters().Length == 0 => m.Invoke(null, null),
        _ => null,
    };
    var origin = $"{type.FullName}.{member.Name}";
    switch (value)
    {
        case ProcessDefinition d: definitions.Add((origin, d)); break;
        case IEnumerable<ProcessDefinition> ds: definitions.AddRange(ds.Select(d => (origin, d))); break;
        default:
            Console.Error.WriteLine($"error BPM000: [BoundedProcess] member {origin} does not yield a ProcessDefinition");
            return 1;
    }
}

if (definitions.Count == 0)
{
    Console.WriteLine($"bpm: no [BoundedProcess] definitions in {Path.GetFileName(assemblyPath)}");
    return 0;
}

var failed = false;
var drift = new List<string>();
foreach (var (origin, def) in definitions)
{
    var result = ProcessCompiler.Compile(def);
    foreach (var d in result.Diagnostics)
        Console.WriteLine(d);
    if (!result.Success)
    {
        failed = true;
        continue;
    }

    var p = result.Process!;
    Console.WriteLine($"bpm: {p.Name} v{p.Version} ok — {p.States.Count} states, {p.Transitions.Count} transitions, T_DAG={p.Analysis.TDag.Pretty()}, T_max={p.Analysis.TMax.Pretty()}{(p.Definition.MaxBudget is { } b ? $" (budget {b.Pretty()})" : "")}, hash {p.TableHash}");

    foreach (var (file, content) in Generators.All(p))
    {
        var path = Path.Combine(outDir, file);
        if (check)
        {
            var existing = File.Exists(path) ? Normalize(File.ReadAllText(path)) : null;
            if (existing != Normalize(content)) drift.Add(path);
        }
        else
        {
            Directory.CreateDirectory(outDir);
            File.WriteAllText(path, content);
        }
    }
}

if (check && drift.Count > 0)
{
    foreach (var d in drift)
        Console.WriteLine($"{d}: error BPM100: generated behaviour changed; regenerate and get the diff reviewed");
    return 1;
}
if (!check && !failed) Console.WriteLine($"bpm: artefacts written to {outDir}");
return failed ? 1 : 0;

string? Arg(string name)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

static string Normalize(string s) => s.Replace("\r\n", "\n");
