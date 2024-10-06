using KSG.RoverTwo.Enums;
using KSG.RoverTwo.Exceptions;
using KSG.RoverTwo.Models;
using KSG.RoverTwo.Tests.Extensions;
using Build = KSG.RoverTwo.Tests.Helpers.Builder;
using Task = KSG.RoverTwo.Models.Task;

namespace KSG.RoverTwo.Tests;

public class ProblemTests : TestBase
{
#pragma warning disable CS8602 // Dereference of a possibly null reference.
	[Fact]
	public void Problem_WithAllDefaults_PassesValidation()
	{
		var problem = BasicProblem().Fill();
		problem.Validate();
		Assert.True(true);
	}

	[Fact]
	public void DistanceFactor_WithOsrm_MustBeOne()
	{
		var problem = BasicProblem().Fill();
		problem.Engine = RoutingEngine.Osrm;
		problem.DistanceUnit = DistanceUnit.Rast;
		var exception = Assert.Throws<ValidationError>(() =>
		{
			problem.Validate();
		});
		Assert.Contains($"distanceUnit must be {DistanceUnit.Metre}", exception.Message);
	}

	[Fact]
	public void Jobs_WhenEmpty_FailsValidation()
	{
		var problem = BasicProblem().Fill();
		problem.Jobs.Clear();
		var exception = Assert.Throws<ValidationError>(() =>
		{
			problem.Validate();
		});
		Assert.Contains($"jobs is Missing", exception.Message);
	}

	[Fact]
	public void Job_WithDuplicateId_FailsValidation()
	{
		var problem = BasicProblem().Fill();
		var firstJob = problem.Jobs.First();
		problem.Jobs.Add(Build.Job(firstJob.Id));
		var exception = Assert.Throws<ValidationError>(() =>
		{
			problem.Validate();
		});
		Assert.Contains($"jobs#1.id={firstJob.Id} is NotUnique", exception.Message);
	}

	[Fact]
	public void Job_WithArrivalWindowCloseBeforeOpen_FailsValidation()
	{
		var problem = BasicProblem().Fill();
		var place = problem.Jobs.First();
		var tZero = new DateTimeOffset();
		place.ArrivalWindow.Open = tZero.AddHours(2);
		place.ArrivalWindow.Close = tZero.AddHours(1);
		var exception = Assert.Throws<ValidationError>(() =>
		{
			problem.Validate();
		});
		Assert.Contains($"jobs#0.arrivalWindow.close is before open", exception.Message);
	}

	[Fact]
	public void Hub_WhenDistanceMatrixIsRequiredByFactor_RequiresLocation()
	{
		var problem = BasicProblem().Fill();
		problem.Hubs.First().Location = null;
		Assert.True(problem.IsDistanceMatrixRequired);
		var exception = Assert.Throws<ValidationError>(() =>
		{
			problem.Validate();
		});
		Assert.Contains("hubs#0.location is Missing", exception.Message);
	}

	[Theory]
	[InlineData(999, 0)]
	[InlineData(-999, 0)]
	[InlineData(0, 999)]
	[InlineData(0, -999)]
	public void Jobs_WhenEngineIsOsrm_RequiresRealLatLong(double latitude, double longitude)
	{
		var problem = BasicProblem().Fill();
		problem.DistanceUnit = DistanceUnit.Metre;
		problem.Engine = RoutingEngine.Osrm;
		var place = problem.Jobs.First();
		place.Location = new Location() { X = longitude, Y = latitude };
		var exception = Assert.Throws<ValidationError>(() =>
		{
			problem.Validate();
		});
		if (longitude < -180 || longitude > 180)
		{
			Assert.Contains($"location.x must be in the range of -180 to 180", exception.Message);
		}
		if (latitude < -90 || latitude > 90)
		{
			Assert.Contains($"location.y must be in the range of -90 to 90", exception.Message);
		}
	}

	[Fact]
	public void Job_WithoutTasks_FailsValidation()
	{
		var problem = BasicProblem().Fill();
		var noTaskJob = Build.Job();
		problem.Jobs.Insert(0, noTaskJob);
		var exception = Assert.Throws<ValidationError>(() =>
		{
			problem.Validate();
		});
		Assert.Contains("jobs#0.tasks is MissingOrEmpty", exception.Message);
	}

	[Fact]
	public void Task_WithDuplicateId_FailsValidation()
	{
		var tool = Build.Tool();
		var problem = BasicProblem().WithTool(tool);
		var firstTask = Build.Task();
		var secondTask = new Task()
		{
			Id = firstTask.Id,
			ToolId = tool.Id,
			Rewards = [],
		};
		problem.WithJobs([Build.Job().WithTasks([firstTask, secondTask])]);
		var exception = Assert.Throws<ValidationError>(() =>
		{
			problem.Fill().Validate();
		});
		Assert.Contains($"jobs#0.tasks#1.id={firstTask.Id} is NotUnique", exception.Message);
	}

	[Fact]
	public void Hub_WithDuplicateId_FailsValidation()
	{
		var problem = BasicProblem();
		var hub = Build.Hub();
		problem.WithHubs([hub, hub]).Fill();
		var exception = Assert.Throws<ValidationError>(() =>
		{
			problem.Validate();
		});
		Assert.Contains($"hubs#1.id={hub.Id} is NotUnique", exception.Message);
	}

	[Fact]
	public void Task_WithNonexistentToolId_FailsValidation()
	{
		var problem = BasicProblem().Fill();
		var firstJob = problem.Jobs.First();
		var firstTask = firstJob.Tasks.First();
		firstTask.ToolId = Guid.NewGuid().ToString();
		var exception = Assert.Throws<ValidationError>(() =>
		{
			problem.Validate();
		});
		Assert.Contains($"jobs#0.tasks#0.toolId={firstTask.ToolId} is Unrecognized", exception.Message);
	}

	[Fact]
	public void Task_WithNoRewards_FailsValidation()
	{
		var problem = BasicProblem().Fill();
		var firstJob = problem.Jobs.First();
		var firstTask = firstJob.Tasks.First();
		firstTask.Rewards.Clear();
		var exception = Assert.Throws<ValidationError>(() =>
		{
			problem.Validate();
		});
		Assert.Contains("jobs#0.tasks#0.rewards is MissingOrEmpty", exception.Message);
	}

	[Fact]
	public void Task_WithNegativeReward_FailsValidation()
	{
		var problem = BasicProblem().Fill();
		var firstJob = problem.Jobs.First();
		var firstTask = firstJob.Tasks.First();
		firstTask.Rewards.First().Amount = -1;
		var exception = Assert.Throws<ValidationError>(() =>
		{
			problem.Validate();
		});
		Assert.Contains("jobs#0.tasks#0.rewards#0.amount=-1 is LessThanZero", exception.Message);
	}

	[Fact]
	public void Task_WithNonexistentRewardMetric_FailsValidation()
	{
		var problem = BasicProblem().Fill();
		var firstJob = problem.Jobs.First();
		var firstTask = firstJob.Tasks.First();
		var firstReward = firstTask.Rewards.First();
		firstReward.MetricId = Guid.NewGuid().ToString();
		var exception = Assert.Throws<ValidationError>(() =>
		{
			problem.Validate();
		});
		Assert.Contains($"jobs#0.tasks#0.rewards#0.metricId={firstReward.MetricId} is Unrecognized", exception.Message);
	}

	[Fact]
	public void Tools_MissingOrEmpty_FailsValidation()
	{
		var problem = BasicProblem().Fill();
		problem.Tools.Clear();
		var exception = Assert.Throws<ValidationError>(() =>
		{
			problem.Validate();
		});
		Assert.Contains($"tools is Missing", exception.Message);
	}

	[Fact]
	public void Tools_EmptyId_FailsValidation()
	{
		var problem = BasicProblem().Fill();
		var json = problem.AsJsonNode();
		json["tools"][0]["id"] = " ";
		var exception = Assert.Throws<ValidationError>(() =>
		{
			Problem.FromJson(json.ToString()).Validate();
		});
		Assert.Contains("tools#0.id is MissingOrEmpty", exception.Message);
	}

	[Fact]
	public void Tools_DuplicateId_FailsValidation()
	{
		var problem = BasicProblem().Fill();
		problem.Tools.Add(problem.Tools.First());
		var exception = Assert.Throws<ValidationError>(() =>
		{
			problem.Validate();
		});
		Assert.Contains($"tools#1.id={problem.Tools[0].Id} is NotUnique", exception.Message);
	}

	[Fact]
	public void Tools_NegativeDefaultWorkTime_FailsValidation()
	{
		var tool = Build.Tool(defaultWorkTime: -1);
		var problem = Build.Problem().WithTool(tool).Fill();
		var exception = Assert.Throws<ValidationError>(() =>
		{
			problem.Validate();
		});
		Assert.Contains($"tools#0.defaultWorkTime=-1 is LessThanOrEqualToZero", exception.Message);
	}

	[Fact]
	public void Tools_NegativeDefaultCompletionChance_FailsValidation()
	{
		var tool = Build.Tool(defaultCompletionChance: -1);
		var problem = Build.Problem().WithTool(tool).Fill();
		var exception = Assert.Throws<ValidationError>(() =>
		{
			problem.Validate();
		});
		Assert.Contains($"tools#0.defaultCompletionChance=-1 is LessThanOrEqualToZero", exception.Message);
	}

	[Fact]
	public void Problem_WithNoWorkers_FailsValidation()
	{
		var problem = BasicProblem().Fill();
		problem.Workers.Clear();
		var exception = Assert.Throws<ValidationError>(() =>
		{
			problem.Validate();
		});
		Assert.Contains($"workers is Missing", exception.Message);
	}

	[Fact]
	public void Worker_WithDuplicateId_FailsValidation()
	{
		var problem = BasicProblem().Fill();
		var firstWorker = problem.Workers.First();
		problem.Workers.Add(Build.Worker(firstWorker.Id));
		var exception = Assert.Throws<ValidationError>(() =>
		{
			problem.Validate();
		});
		Assert.Contains($"workers#1.id={firstWorker.Id} is NotUnique", exception.Message);
	}

	[Fact]
	public void Worker_WithNonexistentStartHubId_FailsValidation()
	{
		var problem = BasicProblem().Fill();
		var firstHub = problem.Hubs.First();
		var worker = Build.Worker(endHub: firstHub);
		problem.WithWorker(worker);
		var exception = Assert.Throws<ValidationError>(() =>
		{
			problem.Validate();
		});
		Assert.Contains(
			$"workers#{problem.Workers.Count - 1}.startHubId={worker.StartHubId} is Unrecognized",
			exception.Message
		);
	}

	[Fact]
	public void Worker_WithStartTimeAfterEndTime_FailsValidation()
	{
		var problem = BasicProblem().Fill();
		var tZero = new DateTimeOffset();
		var worker = Build.Worker();
		worker.EarliestStartTime = tZero.AddHours(2);
		worker.LatestEndTime = tZero.AddHours(1);
		problem.WithWorker(worker);
		var exception = Assert.Throws<ValidationError>(() =>
		{
			problem.Validate();
		});
		Assert.Contains(
			$"workers#{problem.Workers.Count - 1}.earliestStartTime is after latestEndTime",
			exception.Message
		);
	}

	[Fact]
	public void Worker_WithNonexistentEndHubId_FailsValidation()
	{
		var problem = BasicProblem().Fill();
		var firstHub = problem.Hubs.First();
		var hub = Build.Hub();
		var worker = Build.Worker(startHub: firstHub, endHub: hub);
		problem.WithWorker(worker);
		var exception = Assert.Throws<ValidationError>(() =>
		{
			problem.Validate();
		});
		Assert.Contains(
			$"workers#{problem.Workers.Count - 1}.endHubId={worker.EndHubId} is Unrecognized",
			exception.Message
		);
	}

	[Fact]
	public void Worker_WithNegativeTravelSpeedFactor_FailsValidation()
	{
		var worker = Build.Worker(travelSpeedFactor: -1);
		var problem = BasicProblem().WithWorker(worker).Fill();
		var exception = Assert.Throws<ValidationError>(() =>
		{
			problem.Validate();
		});
		Assert.Contains(
			$"workers#{problem.Workers.Count - 1}.travelSpeedFactor is LessThanOrEqualToZero",
			exception.Message
		);
	}

	[Fact]
	public void Worker_WithNoCapabilities_FailsValidation()
	{
		var problem = BasicProblem().Fill();
		var worker = problem.Workers.First();
		worker.Capabilities.Clear();
		var exception = Assert.Throws<ValidationError>(() =>
		{
			problem.Validate();
		});
		Assert.Contains($"workers#0.capabilities is MissingOrEmpty", exception.Message);
	}

	[Fact]
	public void Worker_CapabilityWithNonexistentTool_FailsValidation()
	{
		var problem = BasicProblem().Fill();
		var worker = problem.Workers.First();
		var tool = Build.Tool();
		worker.WithAddedCapability(Build.Capability(tool.Id));
		var exception = Assert.Throws<ValidationError>(() =>
		{
			problem.Validate();
		});
		Assert.Contains($"workers#0.capabilities#1.toolId={tool.Id} is Unrecognized", exception.Message);
	}

	[Fact]
	public void Worker_CapabilityWithNoWorkTime_GetsToolDefault()
	{
		var problem = BasicProblem();
		var tool = Build.Tool(defaultWorkTime: 999);
		var capability = Build.Capability(tool.Id);
		var worker = Build.Worker().WithAddedCapability(capability);

		problem.WithTool(tool).WithWorker(worker).Fill().Validate();

		Assert.Equal(tool.DefaultWorkTime, capability.WorkTime);
	}

	[Fact]
	public void Worker_CapabilityWithNegativeWorkTime_FailsValidation()
	{
		var tool = Build.Tool();
		var capability = Build.Capability(tool.Id, workTime: -1);
		var worker = Build.Worker().WithAddedCapability(capability);
		var problem = BasicProblem().WithWorker(worker).Fill();
		var exception = Assert.Throws<ValidationError>(() =>
		{
			problem.Validate();
		});
		Assert.Contains($"workers#0.capabilities#0.workTime={capability.WorkTime} is LessThanZero", exception.Message);
	}

	[Fact]
	public void Worker_CapabilityWithNoCompletionChance_GetsToolDefault()
	{
		var problem = BasicProblem();
		var tool = Build.Tool(defaultCompletionChance: Random.Shared.NextDouble());
		var capability = Build.Capability(tool.Id);
		var worker = Build.Worker().WithAddedCapability(capability);

		problem.WithTool(tool).WithWorker(worker).Fill().Validate();

		Assert.Equal(tool.DefaultCompletionChance, capability.CompletionChance);
	}

	[Fact]
	public void Worker_CapabilityWithNegativeCompletionChance_FailsValidation()
	{
		var tool = Build.Tool();
		var capability = Build.Capability(tool.Id, completionChance: -1);
		var worker = Build.Worker().WithAddedCapability(capability);
		var problem = BasicProblem().WithWorker(worker).Fill();
		var exception = Assert.Throws<ValidationError>(() =>
		{
			problem.Validate();
		});
		Assert.Contains(
			$"workers#0.capabilities#0.completionChance={capability.CompletionChance} is LessThanZero",
			exception.Message
		);
	}

	[Fact]
	public void CapabilityRewardFactor_WithNonexistentMetric_FailsValidation()
	{
		var problem = BasicProblem().Fill();
		var worker = problem.Workers.First();
		var capability = worker.Capabilities.First();
		var metric = Build.Metric(MetricType.Custom);
		var rewardFactor = Build.RewardFactor(metric.Id);
		capability.RewardFactors.Add(rewardFactor);
		var exception = Assert.Throws<ValidationError>(() =>
		{
			problem.Validate();
		});
		Assert.Contains(
			$"workers#0.capabilities#0.rewardFactors#0.metricId={metric.Id} is Unrecognized",
			exception.Message
		);
	}

	[Fact]
	public void CapabilityRewardFactor_WithDuplicatedMetric_FailsValidation()
	{
		var problem = BasicProblem().Fill();
		var worker = problem.Workers.First();
		var capability = worker.Capabilities.First();
		var rewardFactor = Build.RewardFactor();
		capability.RewardFactors.Add(rewardFactor);
		capability.RewardFactors.Add(rewardFactor);
		var exception = Assert.Throws<ValidationError>(() =>
		{
			problem.Validate();
		});
		Assert.Contains(
			$"workers#0.capabilities#0.rewardFactors#1.metricId={rewardFactor.MetricId} is NotUnique",
			exception.Message
		);
	}

	[Fact]
	public void CapabilityRewardFactor_WithNegativeFactor_FailsValidation()
	{
		var problem = BasicProblem().Fill();
		var worker = problem.Workers.First();
		var capability = worker.Capabilities.First();
		var rewardFactor = Build.RewardFactor(factor: -1);
		capability.RewardFactors.Add(rewardFactor);
		var exception = Assert.Throws<ValidationError>(() =>
		{
			problem.Validate();
		});
		Assert.Contains(
			$"workers#0.capabilities#0.rewardFactors#0.factor={rewardFactor.Factor} is LessThanZero",
			exception.Message
		);
	}

	[Fact]
	public void Metrics_NotProvided_FailsValidation()
	{
		var problem = BasicProblem().Fill();
		problem.Metrics.Clear();
		var exception = Assert.Throws<ValidationError>(() =>
		{
			problem.Validate();
		});
		Assert.Contains($"metrics is MissingOrEmpty", exception.Message);
	}

	[Fact]
	public void Metrics_DuplicateId_FailsValidation()
	{
		var problem = BasicProblem().Fill();
		problem.Metrics.Add(problem.Metrics.First());
		var exception = Assert.Throws<ValidationError>(() =>
		{
			problem.Validate();
		});
		Assert.Contains(
			$"metrics#{problem.Metrics.Count - 1}.id={problem.Metrics[0].Id} is NotUnique",
			exception.Message
		);
	}

	[Theory]
	[InlineData(MetricType.Distance)]
	[InlineData(MetricType.TravelTime)]
	[InlineData(MetricType.WorkTime)]
	public void Metrics_DuplicateBuiltinMetric_FailsValidation(MetricType metricType)
	{
		var problem = BasicProblem().Fill();
		problem.Metrics = [Build.Metric(metricType), Build.Metric(metricType)];
		var exception = Assert.Throws<ValidationError>(() =>
		{
			problem.Validate();
		});
		Assert.Contains($"metrics#1.type={metricType} is NotUnique", exception.Message);
	}

	[Fact]
	public void Metrics_NegativeWeight_FailsValidation()
	{
		var problem = BasicProblem().Fill();
		problem.Metrics = [Build.Metric(MetricType.Distance, weight: -1)];
		var exception = Assert.Throws<ValidationError>(() =>
		{
			problem.Validate();
		});
		Assert.Contains($"metrics#0.weight={problem.Metrics[0].Weight} is LessThanZero", exception.Message);
	}
#pragma warning restore CS8602 // Dereference of a possibly null reference.
}
