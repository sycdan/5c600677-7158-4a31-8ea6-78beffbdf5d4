using KSG.RoverTwo.Exceptions;
using KSG.RoverTwo.Extensions;
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
		var loRewardTask = Build.Task("tiny thing", 1000).WithTool(tool);
		var hiRewardTask = Build.Task("huge thing", 9000).WithTool(tool);
		var targetJob = Build.Job("high-reward-job", (1, 0), jobWindow, optional: true).WithTasks([hiRewardTask]);
		var otherJob = Build.Job("low-reward-job", (1, 0), jobWindow, optional: true).WithTasks([loRewardTask]);
		var problem = Build
			.Problem(weightTravelTime: 1, weightWorkTime: 1, weightReward: 1)
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
		Assert.Contains(otherJob, solution.SkippedJobs);
	}

	[Fact]
	public void MinimizeDistance_WithMultipleOverlappingJobs_PrefersClosestJob()
	{
		var tZero = new DateTimeOffset();
		var jobWindow = new Window { Open = tZero.AddMinutes(50), Close = tZero.AddMinutes(60) };
		var hub = Build.Hub(coordinates: (0, 0), name: "hub");
		var jobA = Build.Job(coordinates: (3, 0), name: "jobA", arrivalWindow: jobWindow, optional: true);
		var jobB = Build.Job(coordinates: (2, 0), name: "jobB", arrivalWindow: jobWindow, optional: true);
		var jobC = Build.Job(coordinates: (1, 0), name: "jobC", arrivalWindow: jobWindow, optional: true);
		var worker = Build.Worker(startHub: hub);
		var problem = Build
			.Problem(weightDistance: 100, weightWorkTime: 1)
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
		var tool = Build.Tool();
		var worker = Build.Worker().WithAddedCapability(tool);
		var requiredTask = Build.Task().WithTool(tool);
		var optionalTask = Build.Task(optional: true);
		var job = Build.Job(name: "Test Job").WithTasks([requiredTask, optionalTask]);
		var problem = Build.Problem().WithWorker(worker).WithJob(job).Fill();
		var solver = new Solver(problem);

		var solution = solver.Solve();

		Assert.Contains(requiredTask, solution.Visits[1].CompletedTasks);
		Assert.DoesNotContain(optionalTask, solution.Visits[1].CompletedTasks);
	}

	[Fact]
	public void BuildPrecedenceMatrix_WithRequiredAndOptionalTasks_PreventsVisitingNodesOutOfOrder()
	{
		var firstTask = Build.Task(name: "required thing");
		var secondTask = Build.Task(name: "optional thing", optional: true);
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

	[Fact]
	public void Constructor_WithSingleWorkerAndZeroCompletionChance_LeavesNoViableWorkers()
	{
		var job = Build.Job();
		var tool = Build.Tool($"tool-for-job-{job.Id}");
		var task = Build.Task().WithTool(tool);
		var worker = Build.Worker().WithAddedCapability(tool, completionChance: 0);
		var problem = Build.Problem().WithWorker(worker).WithJob(job.WithTasks([task])).Fill();

		// There should be only the tool we explicitly created.
		Assert.True(problem.Tools.Count == 1);
		Assert.Equal(tool, problem.Tools.First());

		Assert.Throws<NoViableWorkerException>(() =>
		{
			var solver = new Solver(problem);
		});
	}

	/// <summary>
	/// Create a "visit" tool for each place, and give some workers a capability with that tool.
	/// Then add a job with a task that that requires the job's visit tool.
	/// </summary>
	[Fact]
	public void Solve_WithPerPlaceVisitTools_Works()
	{
		var problem = Build.Problem(weightDistance: 1);
		var capabilities = new List<Capability>();
		// Create 3 pairs of jobs and workers next to each other.
		for (var i = 0; i < 3; i++)
		{
			var job = Build.Job($"job-{i}", coordinates: (i, 0));
			var hub = Build.Hub($"hub-{i}", coordinates: (i, 0));
			var worker = Build.Worker($"worker-{i}", startHub: hub);
			var tool = Build.Tool($"tool-for-{job.Id}");
			var task = Build.Task($"task-with-{tool.Id}").WithTool(tool);
			problem.Hubs.Add(hub);
			problem.Workers.Add(worker);
			problem.Jobs.Add(job.WithTasks([task]));
			problem.Tools.Add(tool);
			capabilities.Add(Build.Capability(tool.Id));
		}
		problem.Workers[0].WithAddedCapability(capabilities[1]);
		problem.Workers[1].WithAddedCapability(capabilities[2]);
		problem.Workers[2].WithAddedCapability(capabilities[0]);
		var solver = new Solver(problem.Fill());

		var solution = solver.Solve();

		// The workers should have taken jobs based on capability, not distance.
		foreach (var (workerIndex, worker) in problem.Workers.Enumerate())
		{
			var visitedJob = solution.Visits.Where(v => v.Worker.Equals(worker) && v.Place is Job).First();
			if (workerIndex == 0)
			{
				Assert.Equal(visitedJob.Place, problem.Jobs.ToList()[1]);
			}
			if (workerIndex == 1)
			{
				Assert.Equal(visitedJob.Place, problem.Jobs.ToList()[2]);
			}
			if (workerIndex == 2)
			{
				Assert.Equal(visitedJob.Place, problem.Jobs.ToList()[0]);
			}
		}
	}

	/// <summary>
	/// Create a "visit" tool, and matching capability, for each worker,
	/// and make certain tasks require those tools.
	/// </summary>
	[Fact]
	public void Solve_WithPerWorkerVisitTools_Works()
	{
		var problem = Build.Problem(weightDistance: 1);
		// Create 3 pairs of jobs and workers next to each other.
		for (var i = 0; i < 3; i++)
		{
			var job = Build.Job($"job-{i}", coordinates: (i, 0));
			var hub = Build.Hub($"hub-{i}", coordinates: (i, 0));
			var worker = Build.Worker($"worker-{i}", startHub: hub);
			var tool = Build.Tool($"vehicle-for-{worker.Id}");
			var task = Build.Task($"task-for-{job.Id}");
			problem.Hubs.Add(hub);
			problem.Workers.Add(worker.WithAddedCapability(tool));
			problem.Jobs.Add(job.WithTasks([task]));
			problem.Tools.Add(tool);
		}
		problem.Jobs[0].Tasks[0].ToolId = problem.Tools[1].Id;
		problem.Jobs[1].Tasks[0].ToolId = problem.Tools[2].Id;
		problem.Jobs[2].Tasks[0].ToolId = problem.Tools[0].Id;
		var solver = new Solver(problem.Fill());

		var solution = solver.Solve();

		// The workers should have taken jobs based on capability, not distance.
		foreach (var (workerIndex, worker) in problem.Workers.Enumerate())
		{
			var visitedJob = solution.Visits.Where(v => v.Worker.Equals(worker) && v.Place is Job).First();
			if (workerIndex == 0)
			{
				Assert.Equal(visitedJob.Place, problem.Jobs.ToList()[2]);
			}
			if (workerIndex == 1)
			{
				Assert.Equal(visitedJob.Place, problem.Jobs.ToList()[0]);
			}
			if (workerIndex == 2)
			{
				Assert.Equal(visitedJob.Place, problem.Jobs.ToList()[1]);
			}
		}
	}

	/// <summary>
	/// Create a "break" tool, and matching capability, and "break" jobs
	/// for each worker, with a task that requires those tools.
	/// </summary>
	[Fact]
	public void Solve_WithWorkerBreakTimes_Works()
	{
		const int jobCount = 5;
		var problem = Build.Problem(timeUnit: Enums.TimeUnit.Hour, weightWorkTime: 1);
		var breakMetric = Build.Metric(Enums.MetricType.Custom, "break-minutes");
		problem.Metrics.Add(breakMetric);
		var hub = Build.Hub("hub");
		var tZero = new DateTimeOffset(1997, 8, 29, 2, 0, 0, -new TimeSpan(5, 0, 0));
		var worker = Build.Worker(
			"worker",
			startHub: hub,
			earliestStartTime: tZero,
			latestEndTime: tZero.AddHours(jobCount)
		);
		var breakTool = Build.Tool($"break-tool-for-{worker.Id}");
		var capability = Build.Capability(breakTool, 0.5); // half hour break
		var breakJob = Build
			.Job("mandatory-break", arrivalWindow: Window.From(tZero.AddHours(2), 2))
			.WithTask(Build.Task("eat-a-sandwich").WithTool(breakTool).WithReward(breakMetric, 30));
		problem.Jobs.Add(breakJob);
		for (var i = 0; i < jobCount; i++)
		{
			var job = Build.Job($"job-{i}", optional: true);
			var task = Build.Task($"task-for-{job.Id}");
			problem.Jobs.Add(job.WithTasks([task]));
			worker.WithAddedCapability(Build.Capability(task.ToolId));
		}
		var solver = new Solver(problem.WithHub(hub).WithWorker(worker.WithAddedCapability(capability)).Fill());

		var solution = solver.Solve();

		// The worker should have taken their break after completing two jobs.
		Assert.Equal(breakJob, solution.Visits[3].Place);
		// The last job should have been skipped due to the break time.
		Assert.Contains(problem.Jobs[^1], solution.SkippedJobs);
		var breakMinutes = solution.Visits.Sum(v =>
			v.EarnedRewards.Where(kvp => kvp.Key == breakMetric).Sum(kvp => kvp.Value)
		);
		Assert.Equal(30, breakMinutes);
	}
}
