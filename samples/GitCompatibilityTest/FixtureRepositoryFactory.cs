using LibGit2Sharp;

namespace GitCompatibilityTest;

internal static class FixtureRepositoryFactory
{
    private static readonly StatusOptions SharedStatusOptions = new()
    {
        IncludeUntracked = true,
        RecurseUntrackedDirs = true,
        DetectRenamesInIndex = true,
        DetectRenamesInWorkDir = true
    };

    public static FixtureRepositoryInfo Create(string fixtureRepositoryPath)
    {
        var repositoryPath = Path.GetFullPath(fixtureRepositoryPath);
        if (Directory.Exists(repositoryPath))
        {
            Directory.Delete(repositoryPath, recursive: true);
        }

        Directory.CreateDirectory(repositoryPath);
        Repository.Init(repositoryPath);

        CreateTrackedFiles(repositoryPath);

        using var repository = new Repository(repositoryPath);
        var signature = new Signature(
            "GitCompatibilityTest",
            "git-compatibility@example.local",
            new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));

        Commands.Stage(repository, "*");
        repository.Commit("Create deterministic benchmark fixture", signature, signature);

        CreateDirtyWorkingTree(repositoryPath);

        var dirtyStatus = repository.RetrieveStatus(SharedStatusOptions);

        return new FixtureRepositoryInfo
        {
            RepositoryPath = repositoryPath,
            InitialBranch = repository.Head.FriendlyName ?? "HEAD",
            CommitCount = repository.Commits.Count(),
            TrackedFileCount = CountTrackedFiles(repositoryPath),
            DirtyEntryCount = dirtyStatus.Count(entry => entry.State != FileStatus.Unaltered)
        };
    }

    private static void CreateTrackedFiles(string repositoryPath)
    {
        WriteFile(repositoryPath, "src/alpha.txt", "alpha-line-1\nalpha-line-2\n");
        WriteFile(repositoryPath, "src/beta.txt", "beta-line-1\nbeta-line-2\n");
        WriteFile(repositoryPath, "src/nested/gamma.json", "{\n  \"name\": \"gamma\",\n  \"enabled\": true\n}\n");
        WriteFile(repositoryPath, "docs/guide.md", "# Fixture guide\n\nThis repository is used for deterministic Git benchmarks.\n");
        WriteFile(repositoryPath, ".gitignore", "bin/\nobj/\n");
    }

    private static void CreateDirtyWorkingTree(string repositoryPath)
    {
        File.AppendAllText(Path.Combine(repositoryPath, "src", "alpha.txt"), "alpha-line-3\n");
        File.AppendAllText(Path.Combine(repositoryPath, "docs", "guide.md"), "\nPending local note.\n");
        WriteFile(repositoryPath, "scratch/untracked.txt", "untracked-content\n");
    }

    private static int CountTrackedFiles(string repositoryPath)
    {
        return Directory
            .EnumerateFiles(repositoryPath, "*", SearchOption.AllDirectories)
            .Count(path => !path.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
    }

    private static void WriteFile(string repositoryPath, string relativePath, string content)
    {
        var fullPath = Path.Combine(repositoryPath, relativePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(fullPath, content);
    }
}
