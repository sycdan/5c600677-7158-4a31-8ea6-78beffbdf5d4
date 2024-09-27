using KSG.RoverTwo.Models;
using KSG.RoverTwo.Tests.Extensions;
using Build = KSG.RoverTwo.Tests.Helpers.Builder;

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
		var tool = Build.Tool("tool", 1);
		var hub = Build.Hub(coordinates: (0, 0));
		var loRewardTask = Build.Task("tiny thing", tool, 1000);
		var hiRewardTask = Build.Task("huge thing", tool, 9000);
		var targetJob = Build.Job("high-reward-job", (1, 0), jobWindow, [hiRewardTask]);
		var otherJob = Build.Job("low-reward-job", (1, 0), jobWindow, [loRewardTask]);
		var problem = Build
			.Problem(tZero, weightTravelTime: 1, weightWorkTime: 1, weightReward: 1)
			.WithTool(tool)
			.WithPlaces([hub, targetJob, otherJob])
			.WithWorker(Build.Worker("bob", hub, hub, capabilities: [Build.Capability(tool.Id)]));
		var solver = new Solver(problem);

		var solution = solver.Solve();

		// hub -> best job -> hub
		Assert.True(solution.Visits.Count == 3);
		Assert.Equal(hub.Id, solution.Visits[0].Place.Id);
		Assert.Equal(targetJob.Id, solution.Visits[1].Place.Id);
		Assert.Equal(hub.Id, solution.Visits[2].Place.Id);
		Assert.Contains(otherJob, solution.SkippedPlaces);
	}

	[Fact]
	public void Minimize_Distance_Works()
	{
		var tZero = new DateTimeOffset();
		var jobWindow = new Window { Open = tZero.AddMinutes(50), Close = tZero.AddMinutes(60) };
		var hub = Build.Hub(coordinates: (0, 0), name: "hub");
		var jobA = Build.Job(coordinates: (3, 0), name: "jobA", window: jobWindow);
		var jobB = Build.Job(coordinates: (2, 0), name: "jobB", window: jobWindow);
		var jobC = Build.Job(coordinates: (1, 0), name: "jobC", window: jobWindow);
		var worker = Build.Worker(startPlace: hub);
		var problem = Build
			.Problem(tZero, weightDistance: 100, weightWorkTime: 1)
			.WithWorker(worker)
			.WithPlaces([hub, jobA, jobB, jobC])
			.Fill();
		var solver = new Solver(problem);

		var solution = solver.Solve();

		// We should have skipped the first 2 jobs, because they are the furthest away
		Assert.True(solution.Visits.Count == 3);
		Assert.Equal(hub.Id, solution.Visits[0].Place.Id);
		Assert.Equal(jobC.Id, solution.Visits[1].Place.Id);
		Assert.Equal(hub.Id, solution.Visits[2].Place.Id);
	}

	[Fact]
	public void Solve_WithTenableRequiredTaskAndUntenableOptionalTasks_MissesOptionalReward()
	{
		var tZero = new DateTimeOffset();
		var requiredTool = Build.Tool("required");
		var optionalTool = Build.Tool("optional", completionRate: 0.99);
		var hub = Build.Hub(coordinates: (0, 0), name: "hub");
		var optionalReward = 9000;
		var tasks = new[]
		{
			Build.Task().WithTool(requiredTool),
			Build.Task(reward: optionalReward).WithTool(optionalTool),
		};
		var job = Build.Job(coordinates: (1, 0), name: "job").WithTasks(tasks);
		var worker = Build.Worker(startPlace: hub).WithCapability(requiredTool);
		var problem = Build
			.Problem(tZero, weightDistance: 1, weightWorkTime: 1)
			.WithWorker(worker)
			.WithPlaces([hub, job])
			.Fill();
		var solver = new Solver(problem);

		var solution = solver.Solve();
		// TODO fix
		// Assert.Single(solution.MissedRewards);
		// Assert.Equal(optionalReward, solution.MissedRewards.First().Value);
	}

	[Fact]
	public void BuildPrecedenceMatrix_WithRequiredAndOptionalTasks_PreventsVisitingNodesOutOfOrder()
	{
		var firstTask = Build.Task(name: "required thing");
		var secondTask = Build.Task(name: "optional thing").WithOptional(true);
		var hub = Build.Hub();
		var job = Build.Job().WithTasks([firstTask, secondTask]);
		var problem = Build.Problem().WithPlaces([hub, job]).Fill();

		var solver = new Solver(problem);
		var matrix = solver.InvalidTransitMatrix;

		// The matrix should be 3x3 (hub, first task @ job, second task @ job).
		Assert.Equal(3, matrix.RowCount);
		Assert.Equal(3, matrix.ColumnCount);

		// Hub to first task is valid.
		Assert.Equal(0, matrix[0, 1]);

		// Hub to second task is invalid.
		Assert.NotEqual(0, matrix[0, 2]);

		// First task to second task is valid.
		Assert.Equal(0, matrix[1, 2]);

		// Second task to first task is invalid.
		Assert.NotEqual(0, matrix[2, 1]);
	}
}
