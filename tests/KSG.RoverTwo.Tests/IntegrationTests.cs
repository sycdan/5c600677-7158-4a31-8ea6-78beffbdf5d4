namespace KSG.RoverTwo.Tests;

public class IntegrationTests : TestBase
{
	public IntegrationTests() { }

	[Fact]
	public void Problem_FromJson_IsSolvable()
	{
		var problem = LoadProblemFromFile();
		var solver = new Solver(problem);

		var solution = solver.Solve();

		Assert.NotNull(solution);
	}
}
