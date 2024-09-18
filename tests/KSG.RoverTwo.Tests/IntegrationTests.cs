namespace KSG.RoverTwo.Tests;

public class IntegrationTests : TestBase
{
	public IntegrationTests() { }

	[Fact]
	public void Problem_FromJson_IsSolvable()
	{
		var request = LoadProblemFromFile();
		var solver = new Solver(request);

		var solution = solver.Solve();

		Assert.NotNull(solution);
	}
}
