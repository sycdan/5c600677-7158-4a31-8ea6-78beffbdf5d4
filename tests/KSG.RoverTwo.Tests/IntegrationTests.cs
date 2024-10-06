using KSG.RoverTwo.Tests.Extensions;
using Build = KSG.RoverTwo.Tests.Helpers.Builder;

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
		var tempFile = Path.GetTempFileName();
		File.WriteAllText(tempFile, json);
		Program.Main([tempFile]);
		File.Delete(tempFile);

		var consoleOutput = writer.ToString();
		Assert.Contains("<Solution>", consoleOutput);
		Assert.Contains("</Solution>", consoleOutput);
	}
}
