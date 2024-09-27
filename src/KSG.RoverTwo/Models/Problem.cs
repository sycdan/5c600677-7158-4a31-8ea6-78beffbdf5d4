using System.Text.Json;
using KSG.RoverTwo.Enums;
using KSG.RoverTwo.Extensions;
using KSG.RoverTwo.Interfaces;
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

	/// <summary>
	/// All possible places the workers may visit.
	/// </summary>
	public List<Place> Places { get; set; } = [];
	internal bool DoAllPlacesHaveLocations => Places.All(x => x.Location is not null);

	/// <summary>
	/// All possible risk/reward factors used to determine route cost, and their relative weights.
	/// </summary>
	public List<Metric> Metrics { get; set; } = [];
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

	/// <summary>
	/// Which workers are allowed or required to visit which places.
	/// Any non-guaranteed visit is considered optional.
	/// </summary>
	public List<Guarantee> Guarantees { get; set; } = [];

	/// <summary>
	/// Every object in the problem, keyed by its unique ID.
	/// </summary>
	internal Dictionary<string, IAmUnique> EntitiesById { get; private init; } = [];
	private int ToolCount => EntitiesById.Count(x => x.Value is Tool);
	private int PlaceCount => EntitiesById.Count(x => x.Value is Place);
	private int WorkerCount => EntitiesById.Count(x => x.Value is Worker);
	private int MetricCount => EntitiesById.Count(x => x.Value is Metric);

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
	///
	/// Any errors will be reported using lowerCamelCase naming.
	/// </summary>
	/// <returns>The Problem object itself.</returns>
	public Problem Validate()
	{
		Log.Verbose("Validating problem data");

		if (EntitiesById.Count > 0)
		{
			Log.Debug(
				"Problem already validated; WorkerCount: {WorkerCount} PlaceCount: {PlaceCount} ToolCount {ToolCount} MetricCount {MetricCount}",
				WorkerCount,
				PlaceCount,
				ToolCount,
				MetricCount
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
		var errorBuilder = new ValidationErrorBuilder();

		var distanceUnit = DistanceUnit;
		EnsureSome(distanceUnit, errorBuilder, nameof(distanceUnit));

		var distanceFactor = DistanceFactor;
		EnsurePositive(distanceFactor, errorBuilder.AddContext(nameof(distanceFactor)));
		if (RoutingEngine.Osrm.Equals(Engine) && !1.Equals(distanceFactor))
		{
			throw errorBuilder.Build($"must be 1 for {nameof(RoutingEngine.Osrm)}");
		}
	}

	private void ValidateTools()
	{
		var errorBuilder = new ValidationErrorBuilder();

		var tools = Tools;
		EnsureSome(tools, errorBuilder.AddContext(nameof(tools)));
		foreach (var (toolIndex, tool) in Tools.Enumerate())
		{
			Log.Debug("{field}#{index}={tool}", nameof(tools), toolIndex, tool);
			EnsureUniqueId(tool.Id, tool, errorBuilder.AddContext(toolIndex));

			var delay = tool.Delay;
			EnsurePositive(delay, errorBuilder, nameof(delay));

			errorBuilder.PopContext();
		}
	}

	private void ValidatePlaces()
	{
		if (ToolCount.Equals(0))
		{
			throw new InvalidOperationException($"call {nameof(ValidateTools)} before {nameof(ValidatePlaces)}");
		}
		if (MetricCount.Equals(0))
		{
			throw new InvalidOperationException($"call {nameof(ValidateMetrics)} before {nameof(ValidatePlaces)}");
		}

		var tZero = TZero;
		var places = Places;
		var errorBuilder = new ValidationErrorBuilder().AddContext(nameof(places));
		EnsureSome(places, errorBuilder);
		foreach (var (placeIndex, place) in places.Enumerate())
		{
			Log.Debug("{field}#{index}={entity}", nameof(places), placeIndex, place);
			EnsureUniqueId(place.Id, place, errorBuilder.AddContext(placeIndex));

			// Ensure the window's open and close times are valid.
			var arrivalWindow = place.ArrivalWindow;
			errorBuilder.AddContext(nameof(arrivalWindow));
			if (arrivalWindow.Open > DateTimeOffset.MinValue)
			{
				var open = arrivalWindow.Open;
				var close = arrivalWindow.Close;
				if (arrivalWindow.Close < arrivalWindow.Open)
				{
					throw errorBuilder.AddContext(nameof(close)).Build($"is before {nameof(open)}");
				}
				if (arrivalWindow.Open < TZero)
				{
					throw errorBuilder.AddContext(nameof(open)).Build($"is before {nameof(tZero)}");
				}
			}
			errorBuilder.PopContext();

			// Validate the location.
			var location = place.Location;
			errorBuilder.AddContext(nameof(location));
			if (location is null && IsDistanceMatrixRequired)
			{
				throw errorBuilder.Build(ValidationErrorType.Missing);
			}
			if (location is not null)
			{
				Log.Verbose("{place} @ {location}", place, location);
				var x = location.X;
				var y = location.Y;
				if (RoutingEngine.Osrm.Equals(Engine))
				{
					// Validate longitude.
					if (x < -180 || x > 180)
					{
						throw errorBuilder.AddContext(x, "=").Build($"must be in the range of -180 to 180");
					}
					// Validate latitude.
					if (location.Y < -90 || location.Y > 90)
					{
						throw errorBuilder.AddContext(y, "=").Build($"must be in the range of -90 to 90");
					}
				}
			}
			errorBuilder.PopContext();

			// Inject a visit task into the place's tasks and then validate them all.
			var tasks = place.Tasks;
			errorBuilder.AddContext(nameof(tasks));
			foreach (var (taskIndex, task) in place.Tasks.Enumerate())
			{
				Log.Debug("{field}#{index}={entity}", nameof(tasks), taskIndex, task);
				EnsureUniqueId(task.Id, task, errorBuilder.AddContext(taskIndex));
				task.Order = taskIndex + 1;
				task.Place = place;

				// Ensure the name is not blank.
				var name = task.Name;
				EnsureSome(name, errorBuilder, nameof(name));

				// Ensure the tool exists and add it to the task.
				var toolId = task.ToolId;
				task.Tool = EnsureEntityExists<Tool>(toolId, errorBuilder, nameof(toolId));

				// Validate the task's rewards.
				var rewards = task.Rewards;
				errorBuilder.AddContext(nameof(rewards));
				// Every task must have at least one reward.
				EnsureSome(rewards, errorBuilder);
				foreach (var (rewardIndex, reward) in rewards.Enumerate())
				{
					errorBuilder.AddContext(rewardIndex);

					// The reward amount cannot be negative.
					var amount = reward.Amount;
					EnsureNotNegative(amount, errorBuilder, nameof(amount));

					// Ensure the Metric exists and accumulate rewards for it.
					var metricId = reward.MetricId;
					var metric = EnsureEntityExists<Metric>(metricId, errorBuilder, nameof(metricId));
					reward.Metric = metric;
					task.RewardsByMetric.TryAdd(metric, 0);
					task.RewardsByMetric[metric] += amount;

					errorBuilder.PopContext();
				}
				errorBuilder.PopContext(2);
			}
			errorBuilder.PopContext(2);
		}
	}

	private void ValidateWorkers()
	{
		if (ToolCount is 0)
		{
			throw new InvalidOperationException($"call {nameof(ValidateTools)} before {nameof(ValidateWorkers)}");
		}
		if (PlaceCount is 0)
		{
			throw new InvalidOperationException($"call {nameof(ValidatePlaces)} before {nameof(ValidateWorkers)}");
		}

		var workers = Workers;
		var errorBuilder = new ValidationErrorBuilder().AddContext(nameof(workers));
		EnsureSome(workers, errorBuilder);
		foreach (var (workerIndex, worker) in workers.Enumerate())
		{
			Log.Debug("{field}#{index}={entity}", nameof(workers), workerIndex, worker);
			EnsureUniqueId(worker.Id, worker, errorBuilder.AddContext(workerIndex));

			// Ensure the worker has a valid start place.
			var startPlaceId = worker.StartPlaceId;
			var startPlace = EnsureEntityExists<Place>(startPlaceId, errorBuilder, nameof(startPlaceId));
			worker.StartPlace = startPlace;

			// Ensure the worker has a valid end place.
			var endPlaceId = worker.EndPlaceId;
			var endPlace = EnsureEntityExists<Place>(endPlaceId, errorBuilder, nameof(endPlaceId));
			worker.EndPlace = endPlace;

			// Ensure the worker has a travel speed factor greater than zero.
			var travelSpeedFactor = worker.TravelSpeedFactor;
			errorBuilder.AddContext(nameof(travelSpeedFactor));
			if (travelSpeedFactor <= 0)
			{
				throw errorBuilder.Build(ValidationErrorType.LessThanOrEqualToZero);
			}
			errorBuilder.PopContext();

			var capabilities = worker.Capabilities;
			errorBuilder.AddContext(nameof(capabilities));
			EnsureSome(capabilities, errorBuilder);
			foreach (var (capabilityIndex, capability) in capabilities.Enumerate())
			{
				errorBuilder.AddContext(capabilityIndex);

				// Ensure the Tool exists and add it to the Capability.
				var toolId = capability.ToolId;
				errorBuilder.AddContext(nameof(toolId));
				var tool = EnsureEntityExists<Tool>(toolId, errorBuilder);
				if (!worker.CapabilitiesByTool.TryAdd(tool, capability))
				{
					throw errorBuilder.AddContext(toolId, "=").Build(ValidationErrorType.NotUnique);
				}
				errorBuilder.PopContext(2);
			}
			errorBuilder.PopContext();

			// Validate the worker's reward modifiers, if they have any.
			var rewardModifiers = worker.RewardModifiers;
			errorBuilder.AddContext(nameof(rewardModifiers));
			foreach (var (rewardModifierIndex, rewardModifier) in rewardModifiers.Enumerate())
			{
				errorBuilder.AddContext(rewardModifierIndex);

				// Ensure the metric exists.
				var metricId = rewardModifier.MetricId;
				var metric = EnsureEntityExists<Metric>(metricId, errorBuilder, nameof(metricId));
				rewardModifier.Metric = metric;

				// TODO more variable modifiers (e.g. place + tool)
				if (rewardModifier.ToolId is not null && rewardModifier.PlaceId is not null)
				{
					throw errorBuilder.Build("cannot have both a tool and a place");
				}

				// TODO allow only unique combinations of tool/place/metric

				// If set, ensure the tool exists and it is accompanied by a factor.
				if (rewardModifier.ToolId is { } toolId)
				{
					var tool = EnsureEntityExists<Tool>(toolId, errorBuilder, nameof(toolId));
					rewardModifier.Tool = tool;
					var factor = rewardModifier.Factor;
					EnsureNotNegative(factor, errorBuilder, nameof(factor));
				}

				// If set, ensure the place exists and it is accompanied by an amount.
				if (rewardModifier.PlaceId is { } placeId)
				{
					var place = EnsureEntityExists<Place>(placeId, errorBuilder, nameof(placeId));
					rewardModifier.Place = place;
					var amount = rewardModifier.Amount;
					EnsurePresent(amount, errorBuilder, nameof(amount));
				}

				// Only one of amount or factor can be defined.
				if (rewardModifier.Amount is not null && rewardModifier.Factor is not null)
				{
					throw errorBuilder.Build("cannot have both an amount and a factor");
				}

				errorBuilder.PopContext();
			}
			errorBuilder.PopContext(2);
		}
	}

	private void ValidateMetrics()
	{
		var factorsByMetric = new Dictionary<MetricType, Metric>();
		var metrics = Metrics;
		var errorBuilder = new ValidationErrorBuilder().AddContext(nameof(metrics));
		EnsureSome(metrics, errorBuilder);
		foreach (var (metricIndex, metric) in metrics.Enumerate())
		{
			Log.Debug("{field}#{index}={entity}", nameof(metrics), metricIndex, metric);
			errorBuilder.AddContext(metricIndex);
			EnsureUniqueId(metric.Id, metric, errorBuilder);

			// Built-in metrics must be unique.
			var type = metric.Type;
			errorBuilder.AddContext(nameof(type));
			if (!MetricType.Custom.Equals(type) && !factorsByMetric.TryAdd(type, metric))
			{
				throw errorBuilder.AddContext(type.ToString(), "=").Build(ValidationErrorType.NotUnique);
			}
			errorBuilder.PopContext();

			var weight = metric.Weight;
			EnsureNotNegative(weight, errorBuilder, nameof(weight));

			errorBuilder.PopContext();
		}
	}

	/// <summary>
	/// Only one worker may be guaranteed to visit a given place.
	/// Many workers may be excluded from visiting a given place.
	/// </summary>
	/// <exception cref="InvalidOperationException">If called out of order.</exception>
	private void ValidateGuarantees()
	{
		if (PlaceCount is 0)
		{
			throw new InvalidOperationException($"call {nameof(ValidatePlaces)} before {nameof(ValidateGuarantees)}");
		}
		if (WorkerCount is 0)
		{
			throw new InvalidOperationException($"call {nameof(ValidateWorkers)} before {nameof(ValidateGuarantees)}");
		}

		if (Guarantees is not { Count: > 0 })
		{
			Log.Information("{field} not provided; all visits will be optional", nameof(Guarantees));
			return;
		}

		var mustVisitsByPlaceId = new Dictionary<string, string>();
		var guarantees = Guarantees;
		var errorBuilder = new ValidationErrorBuilder().AddContext(nameof(guarantees));
		foreach (var (guaranteeIndex, visit) in guarantees.Enumerate())
		{
			Log.Debug("{field}#{index}={entity}", nameof(guarantees), guaranteeIndex, visit);
			errorBuilder.AddContext(guaranteeIndex);

			var workerId = visit.WorkerId;
			errorBuilder.AddContext(nameof(workerId));
			if (!EntitiesById.ContainsKey(workerId))
			{
				throw errorBuilder.AddContext(nameof(workerId)).Build();
			}
			errorBuilder.PopContext();

			var placeId = visit.PlaceId;
			if (!EntitiesById.ContainsKey(placeId))
			{
				throw errorBuilder.AddContext(nameof(placeId)).Build();
			}
			errorBuilder.AddContext(nameof(visit.MustVisit));
			if (visit.MustVisit)
			{
				if (!mustVisitsByPlaceId.TryAdd(placeId, workerId))
				{
					throw errorBuilder.Build($"is already true for {mustVisitsByPlaceId[placeId]}");
				}
			}
			errorBuilder.PopContext(2);
		}
	}

	private void EnsureUniqueId(string? id, IAmUnique entity, ValidationErrorBuilder errorBuilder, string? field = null)
	{
		errorBuilder.AddContext(field ?? nameof(id));
		if (string.IsNullOrWhiteSpace(id))
		{
			throw errorBuilder.Build(ValidationErrorType.MissingOrEmpty);
		}
		if (!EntitiesById.TryAdd(id, entity))
		{
			throw errorBuilder.AddContext(id, "=").Build(ValidationErrorType.NotUnique);
		}
		errorBuilder.PopContext();
	}

	private static void EnsurePresent<T>(T? value, ValidationErrorBuilder errorBuilder, string? field = null)
	{
		if (field is not null)
		{
			errorBuilder.AddContext(field);
		}
		if (value is null)
		{
			throw errorBuilder.Build(ValidationErrorType.Missing);
		}
		if (field is not null)
		{
			errorBuilder.PopContext();
		}
	}

	private static void EnsureNotNegative(double? value, ValidationErrorBuilder errorBuilder, string? field = null)
	{
		if (field is not null)
		{
			errorBuilder.AddContext(field);
		}
		EnsurePresent(value, errorBuilder);
		if (value < 0)
		{
			throw errorBuilder.AddContext(value.Value, "=").Build(ValidationErrorType.LessThanZero);
		}
		if (field is not null)
		{
			errorBuilder.PopContext();
		}
	}

	private static void EnsurePositive(double? value, ValidationErrorBuilder errorBuilder, string? field = null)
	{
		if (field is not null)
		{
			errorBuilder.AddContext(field);
		}
		EnsurePresent(value, errorBuilder);
		if (value <= 0)
		{
			throw errorBuilder.AddContext(value.Value, "=").Build(ValidationErrorType.LessThanOrEqualToZero);
		}
		if (field is not null)
		{
			errorBuilder.PopContext();
		}
	}

	/// <summary>
	/// Validates that a list contains at least one item.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <param name="list"></param>
	/// <param name="errorBuilder"></param>
	/// <param name="field"></param>
	private static void EnsureSome<T>(IList<T>? list, ValidationErrorBuilder errorBuilder, string? field = null)
	{
		if (field is not null)
		{
			errorBuilder.AddContext(field);
		}
		if (list is not { Count: > 0 })
		{
			throw errorBuilder.Build(ValidationErrorType.MissingOrEmpty);
		}
		if (field is not null)
		{
			errorBuilder.PopContext();
		}
	}

	/// <summary>
	/// Validates that a string contains at least one non-whitespace character.
	/// </summary>
	/// <param name="value">The string to validate.</param>
	/// <param name="errorBuilder"></param>
	/// <param name="field">If provided, will be temporarily added to the errorBuilder context.</param>
	private static void EnsureSome(string? value, ValidationErrorBuilder errorBuilder, string? field = null)
	{
		if (field is not null)
		{
			errorBuilder.AddContext(field);
		}
		if (string.IsNullOrWhiteSpace(value))
		{
			throw errorBuilder.Build(ValidationErrorType.MissingOrEmpty);
		}
		if (field is not null)
		{
			errorBuilder.PopContext();
		}
	}

	private T EnsureEntityExists<T>(string id, ValidationErrorBuilder errorBuilder, string? field = null)
	{
		if (field is not null)
		{
			errorBuilder.AddContext(field);
		}
		EnsureSome(id, errorBuilder);
		if (!EntitiesById.TryGetValue(id, out IAmUnique? obj) || obj is not T entity)
		{
			throw errorBuilder.AddContext(id, "=").Build(ValidationErrorType.Unrecognized);
		}
		if (field is not null)
		{
			errorBuilder.PopContext();
		}
		return entity;
	}
}
