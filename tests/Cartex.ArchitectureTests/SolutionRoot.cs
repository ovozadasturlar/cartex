using System.Runtime.CompilerServices;

namespace Cartex.ArchitectureTests;

internal static class SolutionRoot
{
    public static string Find([CallerFilePath] string sourceFile = "")
    {
        var startDirectories = new[]
        {
            Path.GetDirectoryName(sourceFile),
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory
        };

        foreach (var startDirectory in startDirectories.Where(Directory.Exists).Distinct())
        {
            for (var directory = new DirectoryInfo(startDirectory!);
                 directory is not null;
                 directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Cartex.slnx")))
                    return directory.FullName;
            }
        }

        throw new InvalidOperationException(
            $"Solution root not found from: {string.Join(", ", startDirectories)}.");
    }
}
