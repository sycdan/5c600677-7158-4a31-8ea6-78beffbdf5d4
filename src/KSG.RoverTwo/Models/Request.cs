using System.Text.Json;
using KSG.RoverTwo.Enums;
using KSG.RoverTwo.Extensions;
using KSG.RoverTwo.Utils;
using Serilog;

namespace KSG.RoverTwo.Models;

/// <summary>
/// A problem statement for RoverTwo to solve.
/// </summary>
public class Request
{
	/// <summary>
	/// The start time of the problem. Must be before any individual worker start time.
	/// </summary>
	public DateTimeOffset TZero { get; init; } = DateTimeOffset.MinValue;

	/// <summary>
	/// How much time to wait for a solution.
	/// </summary>
	public int TimeoutSeconds { get; set; } = 10;

	/// <summary>
	/// Distance units per time unit. Used to infer travel times.
	/// </summary>
	public double DefaultTravelSpeed { get; set; } = 20;

	/// <summary>
	/// Arbitrary unit of distance measurement, simply used to help understand the data.
	/// </summary>
	public string DistanceUnit { get; init; } = "meter";

	/// <summary>
	/// Arbitrary unit of time measurement, simply used to help understand the data.
	/// </summary>
	public string TimeUnit { get; init; } = "second";

	/// <summary>
	/// Meters per distance unit.
	/// All distances will be multiplied by this.
	/// </summary>
	public double DistanceFactor { get; set; } = 1;

	/// <summary>
	/// Seconds per time unit.
	/// All times will be multiplied by this.
	/// </summary>
	public double TimeFactor { get; set; } = 1;

	/// <summary>
	/// Number of time units a worker can wait at a place for its time window to open.
	/// </summary>
	public double MaxIdleTime { get; set; } = 0;
	public long MaxIdleSeconds => (long)Math.Round(MaxIdleTime * TimeFactor);
	public RoutingEngine RoutingEngine { get; init; } = RoutingEngine.Internal;

	public List<Worker> Workers { get; init; } = [];
	private Dictionary<string, Worker> WorkersById { get; init; } = [];
	public List<Place> Places { get; set; } = [];
	private Dictionary<string, Place> PlacesById { get; init; } = [];
	public bool DoAllPlacesHaveLocations
	{
		get { return Places.All(x => null != x.Location); }
	}

	/// <summary>
	/// Defines all components of the per-worker cost matrix, and their relative weights.
	/// </summary>
	public List<CostFactor> CostFactors { get; init; } = [];
	private Dictionary<string, CostFactor> CostFactorsById { get; init; } = [];

	/// <summary>
	/// All tools that will be available to workers.
	/// </summary>
	public List<Tool> Tools { get; init; } = [];
	private Dictionary<string, Tool> ToolsById { get; init; } = [];

	public bool IsDistanceMatrixRequired
	{
		get
		{
			return CostFactors.Any(cf => Metric.Distance.Equals(cf.Metric))
				|| (
					CostFactors.Any(cf => Metric.TravelTime.Equals(cf.Metric))
					&& RoutingEngine.Internal.Equals(RoutingEngine)
				);
		}
	}
	public bool IsTravelTimeMatrixRequired
	{
		get { return CostFactors.Any(cf => Metric.TravelTime.Equals(cf.Metric)); }
	}

	/// <summary>
	/// Any non-guaranteed visit is considered optional, thus may not be visited.
	/// </summary>
	public List<Guarantee> Guarantees { get; set; } = [];

	/// <summary>
	/// Serializes the Request object to a JSON string.
	/// </summary>
	/// <returns></returns>
	public override string ToString()
	{
		return JsonSerializer.Serialize(this, JsonSerializerOptionsProvider.Default);
	}

	public static Request BuildFromFile(string filePath)
	{
		Log.Verbose("Building request from file: {file}", filePath);
		string jsonData = File.ReadAllText(filePath);
		return BuildFromJson(jsonData);
	}

	/// <summary>
	/// Builds a Request object from a JSON string.
	/// </summary>
	/// <param name="json"></param>
	/// <returns></returns>
	/// <exception cref="ArgumentException"></exception>
	public static Request BuildFromJson(string json)
	{
		Log.Verbose("Building request from json: {json}", json);
		var request = JsonSerializer.Deserialize<Request>(json, JsonSerializerOptionsProvider.Default);
		if (null == request)
		{
			throw new ArgumentException($"{request} not found in {json}");
		}
		return request;
	}

	public Request Validate()
	{
		Log.Verbose("Validating request data");

		if (WorkersById.Count > 0 || PlacesById.Count > 0 || ToolsById.Count > 0 || CostFactorsById.Count > 0)
		{
			Log.Debug(
				"Request already validated; WorkerCount: {WorkerCount} PlaceCount: {PlaceCount} ToolCount {ToolCount} FactorCount {FactorCount}",
				WorkersById.Count,
				PlacesById.Count,
				ToolsById.Count,
				CostFactorsById.Count
			);
			return this;
		}

		Log.Debug("TZero: {TZero}", TZero);
		ValidateTools();

		// Order is important here
		ValidateCostFactors();
		ValidatePlaces();
		ValidateWorkers();
		ValidateGuarantees();

		return Clean();
	}

	private Request Clean()
	{
		Log.Verbose("Cleaning request data");
		return this;
	}

	private void ValidateTools()
	{
		var errorBuilder = new ValidationErrorBuilder().AddContext(nameof(Tools));
		if (null == Tools || Tools.Count == 0)
		{
			throw errorBuilder.Build(ValidationErrorType.MissingOrEmpty);
		}

		foreach (var (i, tool) in Tools.Enumerate())
		{
			errorBuilder.AddContext(i).AddContext(nameof(tool.Id));
			if (string.IsNullOrWhiteSpace(tool.Id))
			{
				throw errorBuilder.Build(ValidationErrorType.Empty);
			}
			if (!ToolsById.TryAdd(tool.Id, tool))
			{
				throw errorBuilder.AddContext(tool.Id, "=").Build(ValidationErrorType.NotUnique);
			}
			errorBuilder.PopContext().AddContext(nameof(tool.DefaultTime));
			if (tool.DefaultTime <= 0)
			{
				throw errorBuilder.Build(ValidationErrorType.LessThanOrEqualToZero);
			}
			errorBuilder.PopContext(2);
		}
	}

	private void ValidatePlaces()
	{
		var errorBuilder = new ValidationErrorBuilder().AddContext(nameof(Places));
		if (null == Places || Places.Count == 0)
		{
			throw errorBuilder.Build(ValidationErrorType.MissingOrEmpty);
		}

		foreach (var (i, place) in Places.Enumerate())
		{
			errorBuilder.AddContext(i).AddContext(nameof(place.Id));
			if (string.IsNullOrWhiteSpace(place.Id))
			{
				throw errorBuilder.Build(ValidationErrorType.MissingOrEmpty);
			}
			if (!PlacesById.TryAdd(place.Id, place))
			{
				throw errorBuilder.AddContext(place.Id, "=").Build(ValidationErrorType.NotUnique);
			}
			errorBuilder.PopContext().AddContext(nameof(place.ArrivalWindow));
			if (place.ArrivalWindow.Open > DateTimeOffset.MinValue)
			{
				if (place.ArrivalWindow.Close < place.ArrivalWindow.Open)
				{
					throw errorBuilder.Build(
						$"Arrival window {nameof(place.ArrivalWindow.Close)} is before {nameof(place.ArrivalWindow.Open)}"
					);
				}
				if (place.ArrivalWindow.Open < TZero)
				{
					throw errorBuilder.Build(
						$"Arrival window {nameof(place.ArrivalWindow.Open)} is before {nameof(TZero)}"
					);
				}
			}
			errorBuilder.PopContext().AddContext(nameof(place.Location));
			if (place.Location is null && IsDistanceMatrixRequired)
			{
				throw errorBuilder.Build(ValidationErrorType.Missing);
			}
			if (place.Location is not null)
			{
				Log.Debug("{place} @ {location}", place, place.Location);
				if (RoutingEngine.Osrm.Equals(RoutingEngine))
				{
					// Longitude
					if (place.Location.X < -180 || place.Location.X > 180)
					{
						throw errorBuilder.Build(
							$"{nameof(place.Location.X)} coordinate must be in the range of -180 to 180"
						);
					}
					// Latitude
					if (place.Location.Y < -90 || place.Location.Y > 90)
					{
						throw errorBuilder.Build(
							$"{nameof(place.Location.Y)} coordinate must be in the range of -90 to 90"
						);
					}
				}
				errorBuilder.PopContext();
			}
			errorBuilder.AddContext(nameof(place.Tasks));
			foreach (var (ti, task) in place.Tasks.Enumerate())
			{
				errorBuilder.AddContext(ti).AddContext(nameof(task.ToolId));
				if (string.IsNullOrWhiteSpace(task.ToolId))
				{
					throw errorBuilder.AddContext(task.ToolId, "=").Build(ValidationErrorType.MissingOrEmpty);
				}
				errorBuilder.PopContext().AddContext(nameof(task.Name));
				if (string.IsNullOrWhiteSpace(task.Name))
				{
					throw errorBuilder.AddContext(task.Name, "=").Build(ValidationErrorType.MissingOrEmpty);
				}
				// errorBuilder.PopContext().AddContext(nameof(task.Reward));
				// if (task.Reward < 0)
				// {
				// 	throw errorBuilder.AddContext(task.Reward.ToString(), "=").Build(ValidationErrorType.LessThanZero);
				// }
				errorBuilder.PopContext(2);
			}
			errorBuilder.PopContext(2);
		}
	}

	private void ValidateWorkers()
	{
		var errorBuilder = new ValidationErrorBuilder().AddContext(nameof(Workers));
		if (null == Workers || Workers.Count == 0)
		{
			throw errorBuilder.Build(ValidationErrorType.MissingOrEmpty);
		}

		foreach (var (i, worker) in Workers.Enumerate())
		{
			errorBuilder.AddContext(i).AddContext(nameof(worker.Id));
			if (string.IsNullOrWhiteSpace(worker.Id))
			{
				throw errorBuilder.Build(ValidationErrorType.MissingOrEmpty);
			}
			if (!WorkersById.TryAdd(worker.Id, worker))
			{
				throw errorBuilder.AddContext(worker.Id, "=").Build(ValidationErrorType.NotUnique);
			}
			errorBuilder.PopContext().AddContext(nameof(worker.StartPlaceId));
			if (string.IsNullOrWhiteSpace(worker.StartPlaceId))
			{
				throw errorBuilder.Build(ValidationErrorType.MissingOrEmpty);
			}
			errorBuilder.PopContext().AddContext(nameof(worker.EndPlaceId));
			if (string.IsNullOrWhiteSpace(worker.EndPlaceId))
			{
				throw errorBuilder.Build(ValidationErrorType.MissingOrEmpty);
			}
			errorBuilder.AddContext(nameof(worker.TravelSpeedFactor));
			if (worker.TravelSpeedFactor <= 0)
			{
				throw errorBuilder.Build(ValidationErrorType.LessThanOrEqualToZero);
			}
			errorBuilder.PopContext(3);
		}

		// @TODO validate capabilities
	}

	private void ValidateCostFactors()
	{
		var errorBuilder = new ValidationErrorBuilder().AddContext(nameof(CostFactors));
		if (null == CostFactors || CostFactors.Count == 0)
		{
			throw errorBuilder.Build(ValidationErrorType.MissingOrEmpty);
		}

		var factorsByMetric = new Dictionary<Metric, CostFactor>();
		foreach (var (i, costFactor) in CostFactors.Enumerate())
		{
			Log.Debug("Cost Factor {i}: {factor}", i, costFactor);
			errorBuilder.AddContext(i).AddContext(nameof(costFactor.Id));
			if (string.IsNullOrWhiteSpace(costFactor.Id))
			{
				throw errorBuilder.Build(ValidationErrorType.MissingOrEmpty);
			}
			if (!CostFactorsById.TryAdd(costFactor.Id, costFactor))
			{
				throw errorBuilder.AddContext(costFactor.Id, "=").Build(ValidationErrorType.NotUnique);
			}
			errorBuilder.PopContext().AddContext(nameof(costFactor.Metric));
			// Built-in metrics must be unique
			if (!Metric.Custom.Equals(costFactor.Metric) && !factorsByMetric.TryAdd(costFactor.Metric, costFactor))
			{
				throw errorBuilder.AddContext(costFactor.Metric.ToString(), "=").Build(ValidationErrorType.NotUnique);
			}
			errorBuilder.PopContext().AddContext(nameof(costFactor.Weight));
			if (costFactor.Weight < 0)
			{
				throw errorBuilder.Build(ValidationErrorType.LessThanZero);
			}
			errorBuilder.PopContext(2);
		}
	}

	/// <summary>
	/// Only one worker may be guaranteed to visit a given place.
	/// Many workers may be excluded from visiting a given place.
	/// </summary>
	/// <exception cref="ApplicationException"></exception>
	private void ValidateGuarantees()
	{
		if (0 == PlacesById.Count)
		{
			throw new ApplicationException($"call {nameof(ValidatePlaces)} first");
		}

		if (0 == WorkersById.Count)
		{
			throw new ApplicationException($"call {nameof(ValidateWorkers)} first");
		}

		if (null == Guarantees || Guarantees.Count == 0)
		{
			Log.Information("{field} not provided; all visits will be optional", nameof(Guarantees));
			return;
		}

		var mustVisitsByPlaceId = new Dictionary<string, string>();

		var errorBuilder = new ValidationErrorBuilder().AddContext(nameof(Guarantees));
		foreach (var (i, visit) in Guarantees.Enumerate())
		{
			errorBuilder.AddContext(i);
			if (!WorkersById.ContainsKey(visit.WorkerId))
			{
				throw errorBuilder.AddContext(nameof(visit.WorkerId)).Build();
			}
			if (!PlacesById.ContainsKey(visit.PlaceId))
			{
				throw errorBuilder.AddContext(nameof(visit.PlaceId)).Build();
			}
			errorBuilder.AddContext(nameof(visit.MustVisit));
			if (visit.MustVisit)
			{
				if (!mustVisitsByPlaceId.TryAdd(visit.PlaceId, visit.WorkerId))
				{
					throw errorBuilder.Build($"is already true for {mustVisitsByPlaceId[visit.PlaceId]}");
				}
			}
			errorBuilder.PopContext(2);
		}
	}

	public Request WithTool(Tool tool)
	{
		Tools.Add(tool);
		return this;
	}

	public Request WithHub(Place place)
	{
		if (!place.IsHub)
		{
			throw new ArgumentException($"{nameof(place.Type)} must be {PlaceType.Hub}");
		}
		Places.Add(place);
		return this;
	}

	public Request WithJob(Place place)
	{
		if (!place.IsJob)
		{
			throw new ArgumentException($"{nameof(place.Type)} must be {PlaceType.Job}");
		}
		Places.Add(place);
		return this;
	}

	public Request WithWorker(Worker worker)
	{
		Workers.Add(worker);
		return this;
	}
}
