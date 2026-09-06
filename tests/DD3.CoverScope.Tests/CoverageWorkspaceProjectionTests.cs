using System.Diagnostics;
using System.Text;
using DD3.CoverScope.Models;
using DD3.CoverScope.Services;
using Xunit;
using Xunit.Abstractions;

namespace DD3.CoverScope.Tests;

public partial class CoverageWorkspaceProjectionTests(ITestOutputHelper output) : IDisposable
{
    private readonly string directory =
        Path.Combine(Path.GetTempPath(), $"coverscope-workspace-{Guid.NewGuid():N}");

    private CoverageReport CreateSmallReport(string name = "Demo")
    {
        Directory.CreateDirectory(directory);
        var reportPath = Path.Combine(directory, $"{name}.cobertura.xml");
        File.WriteAllText(reportPath, """
            <coverage><packages><package name="Demo"><classes>
              <class name="Demo.Worker" filename="Worker.cs"><methods>
                <method name="Run" signature="()"><lines><line number="1" hits="1" /><line number="2" hits="0" /></lines></method>
              </methods><lines><line number="1" hits="1" /><line number="2" hits="0" /></lines></class>
            </classes></package></packages></coverage>
            """);
        return TestServices.CreateCoverageReportService().Parse(reportPath);
    }

    private static string BuildCoberturaXml(
        int projectCount,
        int namespacesPerProject,
        int classesPerNamespace,
        int linesPerClass)
    {
        var xml = new StringBuilder("<coverage timestamp=\"1756944000\"><packages>");
        for (var projectIndex = 0; projectIndex < projectCount; projectIndex++)
        {
            xml.Append("<package name=\"Project").Append(projectIndex).Append("\"><classes>");
            for (var namespaceIndex = 0; namespaceIndex < namespacesPerProject; namespaceIndex++)
            {
                for (var classIndex = 0; classIndex < classesPerNamespace; classIndex++)
                {
                    var typeName = $"Project{projectIndex}.Area{namespaceIndex}.Worker{classIndex}";
                    var fileName = $"Project{projectIndex}/Area{namespaceIndex}/Worker{classIndex}.cs";
                    xml.Append("<class name=\"").Append(typeName)
                        .Append("\" filename=\"").Append(fileName).Append("\"><methods>");
                    for (var methodIndex = 0; methodIndex < 5; methodIndex++)
                    {
                        xml.Append("<method name=\"Method").Append(methodIndex)
                            .Append("\" signature=\"()\"><lines>");
                        var methodStart = methodIndex * (linesPerClass / 5) + 1;
                        var methodEnd = methodIndex == 4
                            ? linesPerClass
                            : methodStart + (linesPerClass / 5) - 1;
                        for (var line = methodStart; line <= methodEnd; line++)
                            AppendLine(xml, line);
                        xml.Append("</lines></method>");
                    }

                    xml.Append("</methods><lines>");
                    for (var line = 1; line <= linesPerClass; line++)
                        AppendLine(xml, line);
                    xml.Append("</lines></class>");
                }
            }
            xml.Append("</classes></package>");
        }
        return xml.Append("</packages></coverage>").ToString();
    }

    private static void AppendLine(StringBuilder xml, int line) =>
        xml.Append("<line number=\"").Append(line)
            .Append("\" hits=\"").Append(line % 4 == 0 ? 0 : 1)
            .Append("\"/>");

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
