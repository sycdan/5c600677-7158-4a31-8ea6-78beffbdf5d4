using KSG.RoverTwo.Models;
using Build = KSG.RoverTwo.Tests.Helpers.Builder;

namespace KSG.RoverTwo.Tests.Models;

public class VisitTests : TestBase
{
	[Theory]
	[InlineData(1, 1, 1)]
	[InlineData(100, 0.6, 60)]
	public void SimulateWork_WithRewardFactors_CalculatesExpectedValues(
		double defaultReward,
		double rewardFactor,
		double expectedReward
	)
	{
		var tool = Build.Tool();
		var metric = Build.RewardMetric();
		var task = Build.Task(tool: tool, reward: defaultReward);
		task.RewardsByMetric[metric] = defaultReward;
		var job = Build.Job(tasks: [task]);
		var worker = Build.Worker(capabilities: [Build.Capability(tool)]);
		var vehicle = new Vehicle(0, worker, 0);
		vehicle.RewardFactors.Add(metric, new() { [tool] = rewardFactor });
		vehicle.ToolTimes[tool] = 1;
		var visit = new Visit { Place = job, Worker = worker };
		visit.SimulateWork(vehicle);
		Assert.Equal(visit.EarnedRewards[metric], expectedReward);
	}
}
