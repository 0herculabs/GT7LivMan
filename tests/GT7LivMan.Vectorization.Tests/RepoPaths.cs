using System.IO;

namespace GT7LivMan.Vectorization.Tests;

/// <summary>Locates the repo root from the test assembly's output directory, for tests that need a scratch path under the repo (not the source of any curated asset — this project has none).</summary>
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
