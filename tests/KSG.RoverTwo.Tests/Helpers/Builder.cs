using KSG.RoverTwo.Enums;
using KSG.RoverTwo.Models;
using Task = KSG.RoverTwo.Models.Task;

namespace KSG.RoverTwo.Tests.Helpers;

public static class Builder
{
	internal const string REWARD = "reward";
	internal const string VISIT_TASK_PREFIX = "visit-task-";

	public static Hub Hub(string? id = null, (double x, double y)? coordinates = null, string? name = null)
	{
		var hub = new Hub
		{
			Id = id ?? Guid.NewGuid().ToString(),
			Name = name,
			Location = Location.From(coordinates ?? (0, 0)),
		};
		return hub;
	}

	public static Job Job(
		string? id = null,
		(double x, double y)? coordinates = null,
		Window? arrivalWindow = null,
		List<Task>? tasks = null,
		bool optional = false,
		string? name = null
	)
	{
		id ??= Guid.NewGuid().ToString();
		arrivalWindow ??= new();
		tasks ??= [];
		var job = new Job
		{
			Id = id,
			Name = name,
			Location = coordinates is null ? null : Location.From(coordinates.Value),
			ArrivalWindow = arrivalWindow,
			Optional = optional,
			Tasks = tasks,
		};
		return job;
	}

	public static Tool Tool(string? id = null, int defaultWorkTime = 1, double defaultCompletionChance = 1)
	{
		var tool = new Tool()
		{
			Id = id ?? Guid.NewGuid().ToString(),
			DefaultWorkTime = defaultWorkTime,
			DefaultCompletionChance = defaultCompletionChance,
		};
		return tool;
	}

	public static Worker Worker(
		string? id = null,
		Hub? startHub = null,
		Hub? endHub = null,
		List<Capability>? capabilities = null,
		double travelSpeedFactor = 1,
		DateTimeOffset? earliestStartTime = null,
		DateTimeOffset? latestEndTime = null
	)
	{
		startHub ??= Hub();
		var worker = new Worker
		{
			Id = id ?? Guid.NewGuid().ToString(),
			StartHubId = startHub.Id,
			EndHubId = (endHub ?? startHub).Id,
			Capabilities = capabilities ?? [],
			TravelSpeedFactor = travelSpeedFactor,
			EarliestStartTime = earliestStartTime,
			LatestEndTime = latestEndTime,
		};
		return worker;
	}

	public static Capability Capability(string toolId, double? workTime = null, double? completionChance = null)
	{
		var capability = new Capability
		{
			ToolId = toolId,
			WorkTime = workTime,
			CompletionChance = completionChance,
		};
		return capability;
	}

	public static Capability Capability(Tool tool, double? workTime = null, double? completionChance = null)
	{
		return Capability(tool.Id, workTime, completionChance);
	}

	public static MetricFactor RewardFactor(string metricId = REWARD, double factor = 1)
	{
		return new MetricFactor { MetricId = metricId, Factor = factor };
	}

	public static Task Task(string? id = null, double reward = 1, bool optional = false, string? name = null)
	{
		var rewards = new List<Reward>
		{
			new() { MetricId = REWARD, Amount = reward },
		};
		var task = new Task
		{
			Id = id ?? Guid.NewGuid().ToString(),
			Name = name,
			ToolId = Guid.NewGuid().ToString(),
			Rewards = rewards,
			Optional = optional,
		};
		return task;
	}

	public static Metric Metric(
		MetricType type,
		string? id = null,
		MetricMode mode = MetricMode.Minimize,
		double weight = 1
	)
	{
		var metric = new Metric
		{
			Id = id ?? Guid.NewGuid().ToString(),
			Type = type,
			Mode = mode,
			Weight = weight,
		};
		return metric;
	}

	public static Metric RewardMetric()
	{
		return Metric(id: REWARD, type: MetricType.Custom, mode: MetricMode.Maximize, weight: 1000000);
	}

	public static Problem Problem(
		DistanceUnit distanceUnit = DistanceUnit.Peninkulma,
		TimeUnit timeUnit = TimeUnit.Hour,
		double defaultTravelSpeed = 45,
		double weightDistance = 0,
		double weightTravelTime = 0,
		double weightWorkTime = 0,
		double weightReward = 0
	)
	{
		var problem = new Problem()
		{
			TimeUnit = timeUnit,
			DistanceUnit = distanceUnit,
			DefaultTravelSpeed = defaultTravelSpeed,
		};

		if (weightDistance > 0)
		{
			problem.Metrics.Add(
				new Metric
				{
					Type = MetricType.Distance,
					Mode = MetricMode.Minimize,
					Weight = weightDistance,
				}
			);
		}
		if (weightTravelTime > 0)
		{
			problem.Metrics.Add(
				new Metric
				{
					Type = MetricType.TravelTime,
					Mode = MetricMode.Minimize,
					Weight = weightTravelTime,
				}
			);
		}
		if (weightWorkTime > 0)
		{
			problem.Metrics.Add(
				new Metric
				{
					Type = MetricType.WorkTime,
					Mode = MetricMode.Minimize,
					Weight = weightWorkTime,
				}
			);
		}
		if (weightReward > 0)
		{
			problem.Metrics.Add(
				new Metric
				{
					Id = REWARD,
					Type = MetricType.Custom,
					Mode = MetricMode.Maximize,
					Weight = weightReward,
				}
			);
		}

		return problem;
	}
}
