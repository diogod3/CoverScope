using System.Diagnostics;
using System.Text.Json;
using DD3.CoverScope.Brokers;
using DD3.CoverScope.Models.Reviews;
using DD3.CoverScope.Services.Reviews;
using Xunit;

namespace DD3.CoverScope.Tests;

public sealed class ReviewProcessTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "coverscope-process-tests-" + Guid.NewGuid().ToString("N"));
    public ReviewProcessTests() => Directory.CreateDirectory(directory);

    [Fact]
    public async Task ProcessSupervisorReturnsExitCodeAndDrainsOutput()
    {
        var result = await new ProcessBroker().ExecuteAsync(new("dotnet", ["--version"], directory), CancellationToken.None);
        Assert.Equal(0, result.ExitCode);
        Assert.Matches(@"\d+\.\d+\.\d+", result.Output);
        Assert.DoesNotContain(ProcessSupervisor.Ready, result.Output);
    }

    [Fact]
    public async Task GitComparisonUsesCommonAncestorAndBlocksUntrackedFiles()
    {
        var broker = new ProcessBroker();
        async Task Git(params string[] args)
        {
            var result = await broker.ExecuteAsync(new("git", args, directory), CancellationToken.None);
            Assert.True(result.ExitCode == 0, result.Error);
        }
        await Git("init", "-b", "main");
        await Git("config", "user.name", "CoverScope test fixture");
        await Git("config", "user.email", "fixture@example.invalid");
        await File.WriteAllTextAsync(Path.Combine(directory, "Example.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        await File.WriteAllTextAsync(Path.Combine(directory, "Example.cs"), "class Example { }\n");
        await Git("add", "."); await Git("commit", "-m", "base");
        await Git("checkout", "-b", "feature");
        await File.AppendAllTextAsync(Path.Combine(directory, "Example.cs"), "class Added { }\n");
        await Git("add", "."); await Git("commit", "-m", "feature change");
        var service = new GitComparisonService(broker, new FileSystemBroker());
        var target = Path.Combine(directory, "Example.csproj");
        var repository = await service.InspectAsync(target);
        await service.AssertCleanAsync(directory, CancellationToken.None);
        var comparison = await service.ResolveAsync(repository, new(target, new("refs/heads/main", false, false)), CancellationToken.None);
        Assert.NotEqual(comparison.Commit, comparison.Baseline);
        var file = Assert.Single(await service.ChangesAsync(comparison, CancellationToken.None));
        Assert.Equal("Example.cs", file.Path);
        Assert.Equal(1, file.Added);
        Assert.Contains("class Added", file.After ?? "");
        await File.WriteAllTextAsync(Path.Combine(directory, "untracked.txt"), "pending");
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AssertCleanAsync(directory, CancellationToken.None));
    }

    [Fact]
    public async Task SourceWorkerLoadsProjectMembershipAndGroupsPartialTypes()
    {
        var project = Path.Combine(directory, "Example.csproj");
        await File.WriteAllTextAsync(project, """
            <Project Sdk="Microsoft.NET.Sdk.Razor">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework><IsTestProject>true</IsTestProject></PropertyGroup>
              <ItemGroup>
                <FrameworkReference Include="Microsoft.AspNetCore.App" />
                <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
              </ItemGroup>
            </Project>
            """);
        await File.WriteAllTextAsync(Path.Combine(directory, "App.razor"), "@namespace Example.Components\n<p>Fixture</p>");
        await File.WriteAllTextAsync(Path.Combine(directory, "Service.cs"), "public partial class Service { public string Read() { var pair = (1, 2); return pair.ToString() + typeof(Example.Components.App).Name + new Dependency().Value; } }");
        await File.WriteAllTextAsync(Path.Combine(directory, "Service.Validations.cs"), "public partial class Service { private bool Validate() => true; }");
        await File.WriteAllTextAsync(Path.Combine(directory, "Dependency.cs"), "public class Dependency { public int Value => 1; }");
        var broker = new ProcessBroker();
        var restore = await broker.ExecuteAsync(new("dotnet", ["restore", project, "--disable-build-servers"], directory), CancellationToken.None);
        Assert.True(restore.ExitCode == 0, restore.Output + restore.Error);
        var output = Path.Combine(directory, "index.json");

        var indexed = await broker.ExecuteAsync(new("dotnet", [typeof(ProcessBroker).Assembly.Location, "--internal-index", directory, project, output, "Debug"], directory), CancellationToken.None);

        Assert.True(indexed.ExitCode == 0, indexed.Output + indexed.Error);
        var evidence = JsonSerializer.Deserialize<CodeEvidence>(await File.ReadAllTextAsync(output), ReviewStore.Json)!;
        Assert.True(evidence.Complete, string.Join("\n", evidence.Limitations));
        Assert.Equal(2, evidence.Types.Count);
        var type = Assert.Single(evidence.Types, x => x.Name == "Service");
        Assert.Equal(2, type.Files.Count);
        Assert.Equal(2, type.Members.Count);
        Assert.Equal(3, evidence.Documents.Count);
        Assert.Contains(evidence.Relationships, edge => edge.From == type.Id && edge.To == evidence.Types.Single(x => x.Name == "Dependency").Id && edge.Kind == "Calls");
        Assert.All(evidence.Relationships, edge =>
        {
            Assert.Contains(evidence.Types, x => x.Id == edge.From);
            Assert.Contains(evidence.Types, x => x.Id == edge.To);
        });
    }

    [Fact]
    public async Task CancellationStopsOwnedParentAndChild()
    {
        // Build a fixture that launches a long-lived copy of itself; the actual supervisor must own both.
        var project = Path.Combine(directory, "Sleeper.csproj");
        await File.WriteAllTextAsync(project, "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings></PropertyGroup></Project>");
        await File.WriteAllTextAsync(Path.Combine(directory, "Program.cs"), """
        using System.Diagnostics;
        using System.Reflection;
        if (args.Length == 0)
        {
            var start = new ProcessStartInfo("dotnet") { UseShellExecute = false };
            start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
            start.ArgumentList.Add("child");
            using var child = Process.Start(start)!;
            File.WriteAllText("child.pid", child.Id.ToString());
        }
        await Task.Delay(TimeSpan.FromMinutes(5));
        """);
        var broker = new ProcessBroker();
        var built = await broker.ExecuteAsync(new("dotnet", ["build", project, "--disable-build-servers", "-p:UseSharedCompilation=false"], directory), CancellationToken.None);
        Assert.True(built.ExitCode == 0, built.Output + built.Error);
        using var cancellation = new CancellationTokenSource();
        var execution = broker.ExecuteAsync(new("dotnet", [Path.Combine(directory, "bin", "Debug", "net10.0", "Sleeper.dll")], directory), cancellation.Token);
        var pidFile = Path.Combine(directory, "child.pid");
        try
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (!File.Exists(pidFile) && !execution.IsCompleted && DateTime.UtcNow < deadline) { await Task.Delay(50); }
            Assert.True(File.Exists(pidFile), "Fixture child did not start.");
            var childId = int.Parse(await File.ReadAllTextAsync(pidFile), System.Globalization.CultureInfo.InvariantCulture);
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => execution);
            try { using var child = Process.GetProcessById(childId); Assert.True(child.HasExited); }
            catch (ArgumentException) { /* The owned child no longer exists. */ }
        }
        finally
        {
            cancellation.Cancel();
            try { await execution; }
            catch (OperationCanceledException) { }
        }
    }

    public void Dispose()
    {
        if (!Directory.Exists(directory)) { return; }
        // Git marks loose objects read-only on Windows. Clear that flag only inside this fixture.
        foreach (var path in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        { File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.ReadOnly); }
        Directory.Delete(directory, true);
    }
}
