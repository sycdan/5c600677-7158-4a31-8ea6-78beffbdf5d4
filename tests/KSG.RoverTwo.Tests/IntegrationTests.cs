using KSG.RoverTwo.Tests.Extensions;
using Build = KSG.RoverTwo.Tests.Helpers.ProblemBuilder;

namespace KSG.RoverTwo.Tests;

public class IntegrationTests : TestBase
{
	public IntegrationTests() { }

	[Fact]
	public void Main_WithSolvableProblemJson_RendersSolution()
	{
		using var writer = new StringWriter();
		Console.SetOut(writer);

		var problem = Build.Problem().Fill();
		var json = problem.Serialize();
		Program.Main([json]);

		var consoleOutput = writer.ToString();
		Assert.Contains("<Solution>", consoleOutput);
		Assert.Contains("</Solution>", consoleOutput);
	}

	[Fact]
	public void Main_WithSolvableProblemFile_RendersSolution()
	{
		using var writer = new StringWriter();
		Console.SetOut(writer);

		var problem = Build.Problem().Fill();
		var json = problem.Serialize();
		var tempFile = Path.GetTempFileName();
		File.WriteAllText(tempFile, json);
		Program.Main(["--file", tempFile]);
		File.Delete(tempFile);

		var consoleOutput = writer.ToString();
		Assert.Contains("<Solution>", consoleOutput);
		Assert.Contains("</Solution>", consoleOutput);
	}
}
