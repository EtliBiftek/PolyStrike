using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

var repositoryRoot = args.Length > 0
    ? Path.GetFullPath(args[0])
    : Directory.GetCurrentDirectory();
var assetsRoot = Path.Combine(repositoryRoot, "Assets");

if (!Directory.Exists(assetsRoot))
{
    Console.Error.WriteLine($"Assets directory not found: {assetsRoot}");
    return 2;
}

var files = Directory
    .EnumerateFiles(assetsRoot, "*.cs", SearchOption.AllDirectories)
    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
    .ToArray();

var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
var errorCount = 0;

foreach (var file in files)
{
    var source = File.ReadAllText(file);
    var tree = CSharpSyntaxTree.ParseText(source, parseOptions, file);
    var errors = tree.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
    if (errors.Length == 0)
        continue;

    foreach (var error in errors)
    {
        var span = error.Location.GetLineSpan();
        var line = span.StartLinePosition.Line + 1;
        var column = span.StartLinePosition.Character + 1;
        var relative = Path.GetRelativePath(repositoryRoot, file).Replace('\\', '/');
        Console.Error.WriteLine($"{relative}({line},{column}): {error.Id}: {error.GetMessage()}");
        errorCount++;
    }
}

if (errorCount > 0)
{
    Console.Error.WriteLine($"C# syntax check failed with {errorCount} error(s).");
    return 1;
}

Console.WriteLine($"C# syntax check passed for {files.Length} source files.");
return 0;
