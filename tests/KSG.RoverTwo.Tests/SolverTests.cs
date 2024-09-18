using KSG.RoverTwo.Models;

namespace KSG.RoverTwo.Tests;

public class SolverTests : TestBase
{
	public SolverTests() { }

	[Fact]
	public void EquidistantJobs_DifferentRewards_HigherRewardIsPicked()
	{
		// Make two optional jobs, both with the same distance from a worker, one with a higher reward
		var tZero = new DateTimeOffset();
		var jobWindow = new Window { Open = tZero.AddMinutes(50), Close = tZero.AddMinutes(60) };
		var tool = CreateTool("tool", 1);
		var hubId = "home";
		var targetJobId = "high-reward-job";
		var otherJobId = "low-reward-job";
		var loRewardTask = CreateTask("tiny thing", tool.Id, 1000);
		var hiRewardTask = CreateTask("huge thing", tool.Id, 9000);
		var request = CreateRequest(tZero, weightTravelTime: 1, weightWorkTime: 1, weightReward: 1)
			.WithTool(tool)
			.WithHub(CreateHub(hubId, (0, 0)))
			.WithJob(CreateJob(targetJobId, (1, 0), jobWindow, [hiRewardTask]))
			.WithJob(CreateJob(otherJobId, (1, 0), jobWindow, [loRewardTask]))
			.WithWorker(CreateWorker("bob", hubId, [CreateCapability(tool.Id)]));
		var solver = new Solver(request);

		var solution = solver.Solve();

		// hub -> best job -> hub
		Assert.True(solution.Visits.Count == 3);
		Assert.Equal(hubId, solution.Visits[0].PlaceId);
		Assert.Equal(targetJobId, solution.Visits[1].PlaceId);
		Assert.Equal(hubId, solution.Visits[2].PlaceId);
		Assert.Contains(otherJobId, solution.Skipped);
	}
}
