using KSG.RoverTwo.Enums;
using KSG.RoverTwo.Models;
using Task = KSG.RoverTwo.Models.Task;

namespace KSG.RoverTwo.Tests.Helpers;

public static class Builder
{
	internal const string REWARD = "reward";

	public static Place Hub(
		string? id = null,
		(double x, double y)? coordinates = null,
		Window? window = null,
		string? name = null
	)
	{
		id ??= Guid.NewGuid().ToString();
		name ??= $"hub:{id}";
		coordinates ??= (0, 0);
		window ??= new Window();
		var place = new Place()
		{
			Id = id,
			Name = name,
			Type = PlaceType.Hub,
			Location = Location.From(coordinates.Value),
			ArrivalWindow = window,
		};
		return place;
	}

	public static Place Job(
		string? id = null,
		(double x, double y)? coordinates = null,
		Window? window = null,
		List<Task>? tasks = null,
		string? name = null
	)
	{
		var location = coordinates is null ? null : Location.From(coordinates.Value);
		var arrivalWindow = window is null ? new Window() : window;
		id ??= Guid.NewGuid().ToString();
		return new Place()
		{
			Id = id,
			Name = name ?? $"job:{id}",
			Type = PlaceType.Job,
			Location = location,
			Tasks = tasks ?? [],
			ArrivalWindow = arrivalWindow,
		};
	}

	public static Tool Tool(string? id = null, int delay = 1, double completionRate = 1)
	{
		return new Tool()
		{
			Id = id ?? Guid.NewGuid().ToString(),
			Delay = delay,
			CompletionRate = completionRate,
		};
	}

	public static Worker Worker(
		string? id = null,
		Place? startPlace = null,
		Place? endPlace = null,
		List<Capability>? capabilities = null
	)
	{
		startPlace ??= Hub();
		var worker = new Worker
		{
			Id = id ?? Guid.NewGuid().ToString(),
			StartPlaceId = (startPlace ?? Hub()).Id,
			EndPlaceId = (endPlace ?? startPlace!).Id,
			Capabilities = capabilities ?? [],
		};
		return worker;
	}

	public static Capability Capability(string toolId, double delayFactor = 1)
	{
		var capability = new Capability { ToolId = toolId, DelayFactor = delayFactor };
		return capability;
	}

	public static Capability Capability(Tool tool, double delayFactor = 1)
	{
		return Capability(tool.Id, delayFactor);
	}

	public static Task Task(string? name = null, Tool? tool = null, double reward = 1)
	{
		var rewards = new List<Reward>
		{
			new() { MetricId = REWARD, Amount = reward },
		};
		return new Task
		{
			Name = name ?? Guid.NewGuid().ToString(),
			Tool = tool,
			ToolId = tool is null ? Guid.NewGuid().ToString() : tool.Id,
			Rewards = rewards,
		};
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
		DateTimeOffset? tZero = null,
		string distanceUnit = "Mile",
		double distanceFactor = 1609.34,
		string timeUnit = "Hour",
		double timeFactor = 3600,
		double defaultTravelSpeed = 45,
		double weightDistance = 0,
		double weightTravelTime = 0,
		double weightWorkTime = 0,
		double weightReward = 0
	)
	{
		var problem = new Problem()
		{
			TZero = tZero ?? DateTimeOffset.UtcNow,
			TimeUnit = timeUnit,
			TimeFactor = timeFactor,
			DistanceUnit = distanceUnit,
			DistanceFactor = distanceFactor,
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
