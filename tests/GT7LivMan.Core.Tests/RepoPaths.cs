namespace GT7LivMan.Core.Tests;

/// <summary>Locates the repo root from the test assembly's output directory, for tests that read real asset files (e.g. the base plate SVG) rather than only in-memory fixtures.</summary>
internal static class RepoPaths
{
    public static string Root
    {
        get
        {
            string? dir = AppContext.BaseDirectory;
            while (dir is not null && !File.Exists(Path.Combine(dir, "GT7LivMan.sln")))
            {
                dir = Path.GetDirectoryName(dir);
            }

            return dir ?? throw new InvalidOperationException(
                $"Could not locate repo root (GT7LivMan.sln) walking up from {AppContext.BaseDirectory}.");
        }
    }
}
