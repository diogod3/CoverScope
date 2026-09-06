using System.Globalization;
using DD3.CoverScope.Models;
using DD3.CoverScope.Models.Exceptions;
using DD3.CoverScope.Services;
using DD3.CoverScope.Services.Views;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;

namespace DD3.CoverScope.Components.Shared;
public partial class CoverageExplorerWorkspace
{
    [Inject] public ICoverageExplorerWorkspaceViewService ViewService { get; set; } = default!;
    [Parameter, EditorRequired] public CoverageReport Report { get; set; } = default!;
    [Parameter] public bool IsVisible { get; set; }
    [Parameter] public CoverageMetricRow? MetricSelection { get; set; }
    [Parameter] public TestFailure? FailureSelection { get; set; }
    private IReadOnlyList<SourceLine> ReadSource(FileCoverage file)
    {
        try { return ViewService.ReadSource(file); }
        catch (CoverageOperationException) { return []; }
    }

    private CoverageReport? report;
    private CoverageMetricRow? previousMetric;
    private TestFailure? previousFailure;
    private bool scrollPending;

    protected override async Task OnParametersSetAsync()
    {
        if (!ReferenceEquals(report, Report))
        {
            report = Report;
            selectedFile = null;
            selectedClass = null;
            selectedMethod = null;
            sourceLines = [];
            targetLine = 0;
            ResetExplorerExpansion();
            var project = report.Packages.OrderBy(item => item.LineMetric.Percent).FirstOrDefault();
            var classItem = project?.Namespaces.SelectMany(item => item.Classes).OrderBy(item => item.LineMetric.Percent).FirstOrDefault();
            if (project is not null && classItem is not null) await SelectClass(project, classItem);
        }
        if (MetricSelection is not null && !ReferenceEquals(previousMetric, MetricSelection))
            await OpenMetricClass(MetricSelection);
        if (FailureSelection is not null && !ReferenceEquals(previousFailure, FailureSelection))
            await OpenFailureSource(FailureSelection);
        previousMetric = MetricSelection;
        previousFailure = FailureSelection;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!IsVisible || !scrollPending) return;
        scrollPending = false;
        await ViewService.ScrollToLineAsync(targetLine);
    }

    private ExplorerSortMode sortMode = ExplorerSortMode.Lowest;
    private ExplorerCoverageFilter coverageFilter = ExplorerCoverageFilter.All;
    private string filter = string.Empty;
    private FileCoverage? selectedFile;
    private ClassCoverage? selectedClass;
    private MethodCoverage? selectedMethod;
    private List<SourceLine> sourceLines = [];
    private int targetLine;
    private readonly HashSet<PackageCoverage> expandedProjects = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<NamespaceCoverage> expandedNamespaces = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<FileCoverage> expandedFiles = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<ClassCoverage> expandedClasses = new(ReferenceEqualityComparer.Instance);
    private ExplorerProjection Explorer =>
        ViewService.Project(report, filter, coverageFilter, sortMode);
    private IReadOnlyList<ExplorerProject> ExplorerProjects => Explorer.Projects;
    private int VisibleFileCount => Explorer.FileCount;
    private int VisibleClassCount => Explorer.ClassCount;

    private Task SelectClass(PackageCoverage project, ClassCoverage classItem) =>
        SelectSource(project, classItem, null, classItem.Lines.FirstOrDefault()?.Number ?? 0);

    private Task SelectFile(FileCoverage file)
    {
        selectedFile = file;
        selectedClass = null;
        selectedMethod = null;
        sourceLines = ReadSource(file).ToList();
        targetLine = 0;
        return Task.CompletedTask;
    }

    private Task SelectMethod(PackageCoverage project, ClassCoverage classItem, MethodCoverage method) =>
        SelectSource(project, classItem, method, method.StartLine);

    private async Task OpenMetricClass(CoverageMetricRow row)
    {
        var classItem = row.ClassFragments.FirstOrDefault();
        if (classItem is null) return;
        filter = row.FullName;
        coverageFilter = ExplorerCoverageFilter.All;
        ExpandSelection(row.Project, classItem);
        await SelectClass(row.Project, classItem);
    }

    private async Task SelectSource(PackageCoverage project, ClassCoverage classItem, MethodCoverage? method, int line)
    {
        selectedClass = classItem;
        selectedMethod = method;
        selectedFile = project.Files.FirstOrDefault(x => string.Equals(x.RelativePath, classItem.RelativePath, StringComparison.OrdinalIgnoreCase));
        sourceLines = selectedFile is null ? [] : ReadSource(selectedFile).ToList();
        targetLine = line;
        await Task.CompletedTask;
        scrollPending = targetLine > 0;
    }

    private static string FileDirectory(string relativePath)
    {
        var directory = Path.GetDirectoryName(relativePath);
        return string.IsNullOrWhiteSpace(directory) ? "project root" : directory;
    }

    private void ResetExplorerExpansion()
    {
        expandedProjects.Clear();
        expandedNamespaces.Clear();
        expandedFiles.Clear();
        expandedClasses.Clear();

        var projection = Explorer;
        foreach (var projectNode in projection.Projects)
            expandedProjects.Add(projectNode.Project);

        if (projection.Projects.Count != 1)
            return;

        foreach (var namespaceNode in projection.Projects[0].Namespaces)
        {
            expandedNamespaces.Add(namespaceNode.Namespace);
            if (namespaceNode.Files.Count == 1)
                expandedFiles.Add(namespaceNode.Files[0].File);
        }
    }

    private void ToggleProject(PackageCoverage project) => Toggle(expandedProjects, project);

    private void ToggleNamespace(NamespaceCoverage namespaceItem) =>
        Toggle(expandedNamespaces, namespaceItem);

    private Task ToggleFile(FileCoverage file)
    {
        Toggle(expandedFiles, file);
        return SelectFile(file);
    }

    private Task ToggleClass(PackageCoverage project, ClassCoverage classItem)
    {
        Toggle(expandedClasses, classItem);
        return SelectClass(project, classItem);
    }

    private void ExpandSelection(PackageCoverage project, ClassCoverage classItem)
    {
        expandedProjects.Add(project);
        var namespaceItem = project.Namespaces.FirstOrDefault(x => x.Classes.Contains(classItem));
        if (namespaceItem is not null)
            expandedNamespaces.Add(namespaceItem);
        var file = project.Files.FirstOrDefault(x =>
            string.Equals(x.RelativePath, classItem.RelativePath, StringComparison.OrdinalIgnoreCase));
        if (file is not null)
            expandedFiles.Add(file);
        expandedClasses.Add(classItem);
    }

    private static void Toggle<T>(HashSet<T> expanded, T item)
        where T : class
    {
        if (!expanded.Remove(item))
            expanded.Add(item);
    }

    private async Task OpenFailureSource(TestFailure failure)
    {
        var match = FindFailureFile(failure);
        if (match is null) return;

        selectedFile = match.Value.File;
        selectedClass = match.Value.File.Classes.FirstOrDefault(x => x.Lines.Any(y => y.Number == failure.SourceLine));
        selectedMethod = selectedClass?.Methods.FirstOrDefault(x => x.SourceLines.Any(y => y.Number == failure.SourceLine));
        sourceLines = ReadSource(match.Value.File).ToList();
        targetLine = failure.SourceLine ?? 0;
        await Task.CompletedTask;
        scrollPending = targetLine > 0;
    }
    private bool CanOpenFailureSource(TestFailure failure) => FindFailureFile(failure) is not null;
}
