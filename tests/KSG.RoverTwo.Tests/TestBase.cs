using System.Text.Json.Nodes;
using KSG.RoverTwo.Enums;
using KSG.RoverTwo.Models;
using Task = KSG.RoverTwo.Models.Task;

namespace KSG.RoverTwo.Tests;

public abstract class TestBase
{
	const string DEFAULT_PROBLEM_DATA_FILE = "vikings.json";
	const string REWARD = "glory";

	public static Request LoadProblemFromFile(string jsonFileName = DEFAULT_PROBLEM_DATA_FILE)
	{
		var json = LoadJsonDataFromFile(jsonFileName);
		var request = Request.BuildFromJson(json.ToString());
		return request;
	}

	public static JsonNode LoadJsonDataFromFile(string jsonFileName = DEFAULT_PROBLEM_DATA_FILE)
	{
		string jsonFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../data/", jsonFileName);
		string jsonData = File.ReadAllText(jsonFilePath);
		var json = JsonNode.Parse(jsonData);
		if (null == json)
		{
			throw new ApplicationException($"No problem data found in {jsonFileName}");
		}
		return json;
	}

	public static Place CreateHub(string id, (double x, double y)? coordinates = null, Window? window = null)
	{
		var location = coordinates is null ? null : Location.BuildFrom(coordinates.Value);
		return new Place()
		{
			Id = id,
			Type = PlaceType.Hub,
			Location = location,
			ArrivalWindow = window ?? new Window(),
		};
	}

	public static Place CreateJob(
		string id,
		(double x, double y)? coordinates = null,
		Window? window = null,
		List<Task>? tasks = null
	)
	{
		var location = coordinates is null ? null : Location.BuildFrom(coordinates.Value);
		var arrivalWindow = window is null ? new Window() : window;
		return new Place()
		{
			Id = id,
			Type = PlaceType.Job,
			Location = location,
			Tasks = tasks ?? [],
			ArrivalWindow = arrivalWindow,
		};
	}

	public static Tool CreateTool(string id, int defaultTime, double completionRate = 1)
	{
		return new Tool()
		{
			Id = id,
			DefaultTime = defaultTime,
			CompletionRate = completionRate,
		};
	}

	public static Worker CreateWorker(string id, string homePlaceId, List<Capability>? capabilities = null)
	{
		var worker = new Worker
		{
			Id = id,
			StartPlaceId = homePlaceId,
			EndPlaceId = homePlaceId,
			Capabilities = capabilities ?? [],
		};
		return worker;
	}

	public static Capability CreateCapability(string toolId, double delayFactor = 1)
	{
		var capability = new Capability { ToolId = toolId, DelayFactor = delayFactor };
		return capability;
	}

	public static Task CreateTask(string name, string toolId, int reward)
	{
		return new Task
		{
			Name = name,
			ToolId = toolId,
			Rewards = { { REWARD, reward } },
		};
	}

	public static Request CreateRequest(
		DateTimeOffset tZero,
		string distanceUnit = "miles",
		double distanceFactor = 1609.34,
		string timeUnit = "hours",
		double timeFactor = 3600,
		double defaultTravelSpeed = 45,
		double weightDistance = 0,
		double weightTravelTime = 0,
		double weightWorkTime = 0,
		double weightReward = 0
	)
	{
		var request = new Request()
		{
			TZero = tZero,
			TimeUnit = timeUnit,
			TimeFactor = timeFactor,
			DistanceUnit = distanceUnit,
			DistanceFactor = distanceFactor,
			DefaultTravelSpeed = defaultTravelSpeed,
		};

		if (weightDistance > 0)
		{
			request.CostFactors.Add(new CostFactor { Metric = Metric.Distance, Weight = weightDistance });
		}
		if (weightTravelTime > 0)
		{
			request.CostFactors.Add(new CostFactor { Metric = Metric.TravelTime, Weight = weightTravelTime });
		}
		if (weightWorkTime > 0)
		{
			request.CostFactors.Add(new CostFactor { Metric = Metric.WorkTime, Weight = weightWorkTime });
		}
		if (weightReward > 0)
		{
			request.CostFactors.Add(
				new CostFactor
				{
					Metric = Metric.Custom,
					Weight = weightReward,
					Id = REWARD,
					IsBenefit = true,
				}
			);
		}

		return request;
	}
}
