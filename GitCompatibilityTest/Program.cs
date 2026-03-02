using LibGit2Sharp;
using System.Runtime.InteropServices;

namespace GitCompatibilityTest;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("========================================");
        Console.WriteLine("LibGit2Sharp ARM64 macOS Compatibility Test");
        Console.WriteLine("========================================");
        Console.WriteLine();

        // Output platform information
        PrintPlatformInfo();

        var results = new List<TestResult>();

        try
        {
            // Run all tests
            results.Add(await TestLibraryLoad());
            results.Add(await TestRepositoryInitialization());
            results.Add(await TestBasicOperations());
        }
        catch (Exception ex)
        {
            LogError("Unhandled exception during test execution", ex);
            Environment.Exit(1);
        }

        // Generate and display report
        GenerateReport(results);

        // Exit with appropriate code
        var allPassed = results.All(r => r.Passed);
        Environment.Exit(allPassed ? 0 : 1);
    }

    static void PrintPlatformInfo()
    {
        Console.WriteLine("Platform Information:");
        Console.WriteLine($"  OS: {RuntimeInformation.OSDescription}");
        Console.WriteLine($"  OS Architecture: {RuntimeInformation.OSArchitecture}");
        Console.WriteLine($"  Process Architecture: {RuntimeInformation.ProcessArchitecture}");
        Console.WriteLine($"  .NET Version: {Environment.Version}");
        Console.WriteLine();
    }

    static async Task<TestResult> TestLibraryLoad()
    {
        Console.WriteLine("Test 1: Library Load Verification");
        Console.WriteLine("----------------------------------------");

        try
        {
            // Attempt to access LibGit2Sharp static properties to trigger library load
            // GlobalSettings requires the native library to be loaded
            var version = GlobalSettings.Version;

            LogSuccess("LibGit2Sharp native library loaded successfully");
            LogInfo($"  Version: {version}");

            return new TestResult("Library Load", true);
        }
        catch (Exception ex)
        {
            LogError("Failed to load LibGit2Sharp native library", ex);
            return new TestResult("Library Load", false, ex.Message);
        }
        finally
        {
            Console.WriteLine();
        }
    }

    static async Task<TestResult> TestRepositoryInitialization()
    {
        Console.WriteLine("Test 2: Repository Initialization");
        Console.WriteLine("----------------------------------------");

        try
        {
            // Initialize Repository object pointing to current directory
            var currentDir = Directory.GetCurrentDirectory();
            LogInfo($"  Testing repository at: {currentDir}");

            using var repo = new Repository(currentDir);

            LogSuccess("Repository object created successfully");
            LogInfo($"  Repository is bare: {repo.Info.IsBare}");

            return new TestResult("Repository Initialization", true);
        }
        catch (Exception ex)
        {
            LogError("Failed to initialize Repository", ex);
            return new TestResult("Repository Initialization", false, ex.Message);
        }
        finally
        {
            Console.WriteLine();
        }
    }

    static async Task<TestResult> TestBasicOperations()
    {
        Console.WriteLine("Test 3: Basic Repository Operations");
        Console.WriteLine("----------------------------------------");

        try
        {
            var currentDir = Directory.GetCurrentDirectory();
            using var repo = new Repository(currentDir);

            // Test status retrieval
            var status = repo.RetrieveStatus();
            LogInfo($"  Repository status retrieved");
            LogInfo($"  Modified files: {status.Modified.Count()}");
            LogInfo($"  Untracked files: {status.Untracked.Count()}");

            // Test branch name retrieval
            var head = repo.Head;
            var branchName = head.FriendlyName;
            LogInfo($"  Current branch: {branchName}");

            // Test commit info
            if (head.Tip != null)
            {
                LogInfo($"  Latest commit: {head.Tip.Id.ToString().Substring(0, 7)} - {head.Tip.MessageShort}");
            }

            LogSuccess("Basic repository operations completed successfully");
            return new TestResult("Basic Operations", true);
        }
        catch (Exception ex)
        {
            LogError("Failed to execute basic repository operations", ex);
            return new TestResult("Basic Operations", false, ex.Message);
        }
        finally
        {
            Console.WriteLine();
        }
    }

    static void GenerateReport(List<TestResult> results)
    {
        Console.WriteLine("========================================");
        Console.WriteLine("Compatibility Test Report");
        Console.WriteLine("========================================");
        Console.WriteLine();

        var passed = results.Count(r => r.Passed);
        var total = results.Count;
        var allPassed = passed == total;

        Console.WriteLine($"Overall Status: {(allPassed ? "PASS" : "FAIL")}");
        Console.WriteLine($"Tests Passed: {passed}/{total}");
        Console.WriteLine();

        foreach (var result in results)
        {
            var statusIcon = result.Passed ? "✓" : "✗";
            var statusText = result.Passed ? "PASS" : "FAIL";
            Console.WriteLine($"  [{statusIcon}] {result.TestName}: {statusText}");
            if (!result.Passed && result.ErrorMessage != null)
            {
                Console.WriteLine($"      Error: {result.ErrorMessage}");
            }
        }

        Console.WriteLine();
        Console.WriteLine("========================================");

        if (allPassed)
        {
            LogSuccess("All tests passed! LibGit2Sharp is compatible with this platform.");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[FAIL] Some tests failed. LibGit2Sharp may not be fully compatible with this platform.");
            Console.ResetColor();
        }
    }

    static void LogSuccess(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[PASS] {message}");
        Console.ResetColor();
    }

    static void LogInfo(string message)
    {
        Console.WriteLine($"[INFO] {message}");
    }

    static void LogError(string message, Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[FAIL] {message}");
        Console.ResetColor();
        Console.WriteLine($"  Exception Type: {ex.GetType().Name}");
        Console.WriteLine($"  Message: {ex.Message}");
        if (ex.StackTrace != null)
        {
            Console.WriteLine($"  Stack Trace: {ex.StackTrace}");
        }
    }

    record TestResult(string TestName, bool Passed, string? ErrorMessage = null);
}
