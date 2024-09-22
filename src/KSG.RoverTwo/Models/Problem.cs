using System.Text.Json;
using KSG.RoverTwo.Enums;
using KSG.RoverTwo.Extensions;
using KSG.RoverTwo.Utils;
using Serilog;

namespace KSG.RoverTwo.Models;

/// <summary>
/// A problem to be solved, typically from an API request.
/// </summary>
public class Problem
{
	/// <summary>
	/// The start time of the problem. Must be before any individual worker start time.
	/// </summary>
	public DateTimeOffset TZero { get; set; } = DateTimeOffset.MinValue;

	/// <summary>
	/// How much time to wait for a solution.
	/// </summary>
	public int TimeoutSeconds { get; set; } = 1;

	/// <summary>
	/// Distance units per time unit. Used to infer travel times.
	/// </summary>
	public double DefaultTravelSpeed { get; set; } = 20;

	/// <summary>
	/// Arbitrary unit of distance measurement, simply used to help understand the data.
	/// </summary>
	public string DistanceUnit { get; set; } = "Meter";

	/// <summary>
	/// Arbitrary unit of time measurement, simply used to help understand the data.
	/// </summary>
	public string TimeUnit { get; set; } = "Second";

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

	/// <summary>
	/// Whether to use the simple routing engine (for testing purposes) or a real one.
	/// </summary>
	public RoutingEngine Engine { get; set; } = RoutingEngine.Simple;

	/// <summary>
	/// A list of workers to solve for.
	/// </summary>
	public List<Worker> Workers { get; set; } = [];
	private Dictionary<string, Worker> WorkersById { get; init; } = [];

	/// <summary>
	/// All possible places the workers may visit.
	/// </summary>
	public List<Place> Places { get; set; } = [];
	private Dictionary<string, Place> PlacesById { get; init; } = [];
	internal bool DoAllPlacesHaveLocations => Places.All(x => x.Location is not null);

	/// <summary>
	/// All possible risk/reward factors used to determine route cost, and their relative weights.
	/// </summary>
	public List<Metric> Metrics { get; set; } = [];
	private Dictionary<string, Metric> MetricsById { get; init; } = [];
	internal bool IsDistanceMatrixRequired
	{
		get
		{
			return Metrics.Any(cf => MetricType.Distance.Equals(cf.Type))
				|| (Metrics.Any(cf => MetricType.TravelTime.Equals(cf.Type)) && RoutingEngine.Simple.Equals(Engine));
		}
	}
	internal bool IsTravelTimeMatrixRequired
	{
		get { return Metrics.Any(cf => MetricType.TravelTime.Equals(cf.Type)); }
	}

	/// <summary>
	/// All tools that will be available to workers.
	/// </summary>
	public List<Tool> Tools { get; set; } = [];
	private Dictionary<string, Tool> ToolsById { get; init; } = [];

	/// <summary>
	/// Which workers are allowed or required to visit which places.
	/// Any non-guaranteed visit is considered optional.
	/// </summary>
	public List<Guarantee> Guarantees { get; set; } = [];

	/// <summary>
	/// Serializes the problem to a JSON string.
	/// </summary>
	/// <returns></returns>
	public override string ToString()
	{
		return JsonSerializer.Serialize(this, JsonSerializerOptionsProvider.Default);
	}

	/// <summary>
	/// Builds a problem from a file.
	/// </summary>
	/// <param name="filePath">Path to the JSON file containing problem data.</param>
	/// <returns>The problem object.</returns>
	public static Problem FromFile(string filePath)
	{
		Log.Verbose("Building problem from file: {file}", filePath);
		string jsonData = File.ReadAllText(filePath);
		return FromJson(jsonData);
	}

	/// <summary>
	/// Builds a problem from a JSON string.
	/// </summary>
	/// <param name="json">Problem data in JSON format.</param>
	/// <returns>The problem object.</returns>
	/// <exception cref="ArgumentException">If the string is empty.</exception>
	public static Problem FromJson(string json)
	{
		Log.Verbose("Building problem from json: {json}", json);
		var problem = JsonSerializer.Deserialize<Problem>(json, JsonSerializerOptionsProvider.Default);
		if (null == problem)
		{
			throw new ArgumentException($"{problem} not found in {json}");
		}
		return problem;
	}

	/// <summary>
	/// Ensures the problem is valid, and populates internal data structures.
	/// </summary>
	/// <returns>The Problem object itself.</returns>
	public Problem Validate()
	{
		Log.Verbose("Validating problem data");

		if (WorkersById.Count > 0 || PlacesById.Count > 0 || ToolsById.Count > 0 || MetricsById.Count > 0)
		{
			Log.Debug(
				"Problem already validated; WorkerCount: {WorkerCount} PlaceCount: {PlaceCount} ToolCount {ToolCount} FactorCount {FactorCount}",
				WorkersById.Count,
				PlacesById.Count,
				ToolsById.Count,
				MetricsById.Count
			);
			return this;
		}

		ValidateTzero();
		ValidateDistanceConfig();

		// Order is important here
		ValidateTools();
		ValidateMetrics();
		ValidatePlaces();
		ValidateWorkers();
		ValidateGuarantees();

		return this;
	}

	private void ValidateTzero()
	{
		Log.Verbose("TZero: {TZero}", TZero);
		// No validations are required for TZero at this time
	}

	private void ValidateDistanceConfig()
	{
		var errorBuilder = new ValidationErrorBuilder().AddContext(nameof(DistanceUnit));
		if (string.IsNullOrWhiteSpace(DistanceUnit))
		{
			throw errorBuilder.Build(ValidationErrorType.MissingOrEmpty);
		}

		errorBuilder.PopContext().AddContext(nameof(DistanceFactor));
		if (DistanceFactor <= 0)
		{
			throw errorBuilder
				.AddContext(DistanceFactor.ToString(), "=")
				.Build(ValidationErrorType.LessThanOrEqualToZero);
		}
		if (RoutingEngine.Osrm.Equals(Engine) && !1.Equals(DistanceFactor))
		{
			throw errorBuilder.Build($"must be 1 for {nameof(RoutingEngine.Osrm)}");
		}
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
			errorBuilder.PopContext().AddContext(nameof(tool.Delay));
			if (tool.Delay <= 0)
			{
				throw errorBuilder.Build(ValidationErrorType.LessThanOrEqualToZero);
			}
			errorBuilder.PopContext(2);
		}
	}

	private void ValidatePlaces()
	{
		if (ToolsById.Count.Equals(0))
		{
			throw new InvalidOperationException($"call {nameof(ValidateTools)} before {nameof(ValidatePlaces)}");
		}
		if (MetricsById.Count.Equals(0))
		{
			throw new InvalidOperationException($"call {nameof(ValidateMetrics)} before {nameof(ValidatePlaces)}");
		}

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
				if (RoutingEngine.Osrm.Equals(Engine))
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
					throw errorBuilder.Build(ValidationErrorType.MissingOrEmpty);
				}
				if (!ToolsById.TryGetValue(task.ToolId, out Tool? tool))
				{
					throw errorBuilder.AddContext(task.ToolId, "=").Build(ValidationErrorType.Invalid);
				}
				task.Tool = tool;

				// Populate the name if it's missing
				if (string.IsNullOrWhiteSpace(task.Name))
				{
					task.Name = task.Id;
				}

				errorBuilder.PopContext().AddContext(nameof(task.Rewards));
				if (task.Rewards.Count.Equals(0))
				{
					throw errorBuilder.Build(ValidationErrorType.MissingOrEmpty);
				}
				foreach (var (ri, reward) in task.Rewards.Enumerate())
				{
					errorBuilder.AddContext(ri).AddContext(nameof(reward.Amount));
					if (reward.Amount <= 0)
					{
						throw errorBuilder.Build(ValidationErrorType.LessThanOrEqualToZero);
					}

					errorBuilder.PopContext().AddContext(nameof(reward.MetricId));
					if (!MetricsById.TryGetValue(reward.MetricId, out Metric? metric))
					{
						throw errorBuilder.AddContext(reward.MetricId, "=").Build(ValidationErrorType.Invalid);
					}
					reward.Metric = metric;
					task.RewardsByMetric[metric] = reward.Amount;

					errorBuilder.PopContext(2);
				}

				errorBuilder.PopContext(2);
			}

			errorBuilder.PopContext(2);
		}
	}

	private void ValidateWorkers()
	{
		if (ToolsById.Count.Equals(0))
		{
			throw new InvalidOperationException($"{nameof(Tools)} must be validated before {nameof(Workers)}");
		}

		var errorBuilder = new ValidationErrorBuilder().AddContext(nameof(Workers));
		if (null == Workers || Workers.Count == 0)
		{
			throw errorBuilder.Build(ValidationErrorType.MissingOrEmpty);
		}

		foreach (var (i, worker) in Workers.Enumerate())
		{
			errorBuilder.AddContext(i);

			// Ensure the worker has a unique identifier
			errorBuilder.AddContext(nameof(worker.Id));
			if (string.IsNullOrWhiteSpace(worker.Id))
			{
				throw errorBuilder.Build(ValidationErrorType.MissingOrEmpty);
			}
			if (!WorkersById.TryAdd(worker.Id, worker))
			{
				throw errorBuilder.AddContext(worker.Id, "=").Build(ValidationErrorType.NotUnique);
			}
			errorBuilder.PopContext();

			// Ensure the worker has a valid start place
			errorBuilder.AddContext(nameof(worker.StartPlaceId));
			if (string.IsNullOrWhiteSpace(worker.StartPlaceId))
			{
				throw errorBuilder.Build(ValidationErrorType.MissingOrEmpty);
			}
			if (!PlacesById.TryGetValue(worker.StartPlaceId, out Place? startPlace))
			{
				throw errorBuilder.AddContext(worker.StartPlaceId, "=").Build(ValidationErrorType.Invalid);
			}
			worker.StartPlace = startPlace;
			errorBuilder.PopContext();

			// Ensure the worker has a valid end place
			errorBuilder.AddContext(nameof(worker.EndPlaceId));
			if (string.IsNullOrWhiteSpace(worker.EndPlaceId))
			{
				throw errorBuilder.Build(ValidationErrorType.MissingOrEmpty);
			}
			if (!PlacesById.TryGetValue(worker.EndPlaceId, out Place? endPlace))
			{
				throw errorBuilder.AddContext(worker.EndPlaceId, "=").Build(ValidationErrorType.Invalid);
			}
			worker.EndPlace = endPlace;
			errorBuilder.PopContext();

			// Ensure the worker has a travel speed factor greater than zero
			errorBuilder.AddContext(nameof(worker.TravelSpeedFactor));
			if (worker.TravelSpeedFactor <= 0)
			{
				throw errorBuilder.Build(ValidationErrorType.LessThanOrEqualToZero);
			}
			errorBuilder.PopContext();

			// Validate capabilities
			errorBuilder.AddContext(nameof(worker.Capabilities));
			foreach (var (ci, capability) in worker.Capabilities.Enumerate())
			{
				errorBuilder.AddContext(ci);

				// Ensure the tool exists and add it to the worker's capabilities
				errorBuilder.AddContext(nameof(capability.ToolId));
				if (string.IsNullOrWhiteSpace(capability.ToolId))
				{
					throw errorBuilder.Build(ValidationErrorType.MissingOrEmpty);
				}
				if (!ToolsById.TryGetValue(capability.ToolId, out Tool? tool))
				{
					throw errorBuilder.AddContext(capability.ToolId, "=").Build(ValidationErrorType.Invalid);
				}
				if (!worker.CapabilitiesByTool.TryAdd(tool, capability))
				{
					throw errorBuilder.AddContext(capability.ToolId, "=").Build(ValidationErrorType.NotUnique);
				}

				errorBuilder.PopContext(2);
			}
			errorBuilder.PopContext();

			// Validate visit costs
			errorBuilder.AddContext(nameof(worker.VisitCosts));
			foreach (var (vci, visitCost) in worker.VisitCosts.Enumerate())
			{
				errorBuilder.AddContext(vci);

				// Ensure the place exists
				errorBuilder.AddContext(nameof(visitCost.PlaceId));
				if (!PlacesById.TryGetValue(visitCost.PlaceId, out Place? place))
				{
					throw errorBuilder.AddContext(visitCost.PlaceId, "=").Build(ValidationErrorType.Invalid);
				}
				visitCost.Place = place;
				errorBuilder.PopContext();

				// Ensure the metric exists
				errorBuilder.AddContext(nameof(visitCost.MetricId));
				if (!MetricsById.TryGetValue(visitCost.MetricId, out Metric? metric))
				{
					throw errorBuilder.AddContext(visitCost.MetricId, "=").Build(ValidationErrorType.Invalid);
				}
				visitCost.Metric = metric;
				errorBuilder.PopContext();

				// Ensure the amount is greater than zero
				errorBuilder.AddContext(nameof(visitCost.Amount));
				if (visitCost.Amount <= 0)
				{
					throw errorBuilder
						.AddContext(visitCost.Amount.ToString(), "=")
						.Build(ValidationErrorType.LessThanOrEqualToZero);
				}
				errorBuilder.PopContext();

				errorBuilder.PopContext();
			}
			errorBuilder.PopContext();

			// Validate reward factors
			errorBuilder.AddContext(nameof(worker.RewardModifiers));
			foreach (var (rfi, rewardFactor) in worker.RewardModifiers.Enumerate())
			{
				errorBuilder.AddContext(rfi);

				// Ensure the metric exists
				errorBuilder.AddContext(nameof(rewardFactor.MetricId));
				if (!MetricsById.TryGetValue(rewardFactor.MetricId, out Metric? metric))
				{
					throw errorBuilder.AddContext(rewardFactor.MetricId, "=").Build(ValidationErrorType.Invalid);
				}
				rewardFactor.Metric = metric;
				errorBuilder.PopContext();

				// Ensure the multiplier is at least zero
				errorBuilder.AddContext(nameof(rewardFactor.Factor));
				if (rewardFactor.Factor < 0)
				{
					throw errorBuilder
						.AddContext(rewardFactor.Factor.ToString(), "=")
						.Build(ValidationErrorType.LessThanZero);
				}
				errorBuilder.PopContext();

				errorBuilder.PopContext();
			}

			errorBuilder.PopContext(2);
		}
	}

	private void ValidateMetrics()
	{
		var errorBuilder = new ValidationErrorBuilder().AddContext(nameof(Metrics));
		if (null == Metrics || Metrics.Count == 0)
		{
			throw errorBuilder.Build(ValidationErrorType.MissingOrEmpty);
		}

		var factorsByMetric = new Dictionary<MetricType, Metric>();
		foreach (var (i, metric) in Metrics.Enumerate())
		{
			Log.Debug("Metric {i}: {metric}", i, metric);
			errorBuilder.AddContext(i).AddContext(nameof(metric.Id));
			if (string.IsNullOrWhiteSpace(metric.Id))
			{
				throw errorBuilder.Build(ValidationErrorType.MissingOrEmpty);
			}
			if (!MetricsById.TryAdd(metric.Id, metric))
			{
				throw errorBuilder.AddContext(metric.Id, "=").Build(ValidationErrorType.NotUnique);
			}
			errorBuilder.PopContext().AddContext(nameof(metric.Type));
			// Built-in metrics must be unique
			if (!MetricType.Custom.Equals(metric.Type) && !factorsByMetric.TryAdd(metric.Type, metric))
			{
				throw errorBuilder.AddContext(metric.Type.ToString(), "=").Build(ValidationErrorType.NotUnique);
			}
			errorBuilder.PopContext().AddContext(nameof(metric.Weight));
			if (metric.Weight < 0)
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
}
