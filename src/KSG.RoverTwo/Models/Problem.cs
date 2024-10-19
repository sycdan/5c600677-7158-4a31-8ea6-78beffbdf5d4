using System.Text.Json;
using System.Text.Json.Nodes;
using KSG.RoverTwo.Enums;
using KSG.RoverTwo.Extensions;
using KSG.RoverTwo.Helpers;
using KSG.RoverTwo.Interfaces;
using Serilog;

namespace KSG.RoverTwo.Models;

/// <summary>
/// A problem to be solved, typically from an API request.
/// </summary>
public class Problem
{
	/// <summary>
	/// How much time to wait for a solution.
	/// </summary>
	public int TimeoutSeconds { get; set; } = 1;

	/// <summary>
	/// Distance units per time unit. Used to infer travel times.
	/// </summary>
	public double DefaultTravelSpeed { get; set; } = 20;

	/// <summary>
	/// The unit of measurement used in <see cref="Distances"/>>.
	/// </summary>
	public DistanceUnit DistanceUnit { get; set; } = DistanceUnit.Metre;

	/// <summary>
	/// The length of each whole time unit used to define durations in the problem.
	/// </summary>
	public TimeUnit TimeUnit { get; set; } = TimeUnit.Second;

	/// <summary>
	/// Number of time units a worker can wait at a place for its time window to open.
	/// </summary>
	public double MaxIdleTime { get; set; } = 0;

	/// <summary>
	/// All possible places at which the workers may start or end their route.
	/// </summary>
	public List<Hub> Hubs { get; set; } = [];

	/// <summary>
	/// All possible places at which the workers may complete tasks.
	/// </summary>
	public List<Job> Jobs { get; set; } = [];

	/// <summary>
	/// All possible risk/reward factors used to determine route cost, and their relative weights.
	/// </summary>
	public List<Metric> Metrics { get; set; } = [];
	internal bool IsDistanceMatrixRequired
	{
		get
		{
			return Metrics.Any(cf => MetricType.Distance.Equals(cf.Type))
				|| Metrics.Any(cf => MetricType.TravelTime.Equals(cf.Type));
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
	/// All workers able to visit places in the problem.
	/// </summary>
	public List<Worker> Workers { get; set; } = [];

	/// <summary>
	/// Precalculated distances between each place (hub or job) and each other place (excluding the place itself).
	/// If omitted, distances will be calculated using the Manhattan method.
	/// e.g.
	/// {
	/// 	"hub1":
	/// 	{
	/// 		"hub2": 1,
	/// 		"job1": 2,
	/// 		"job2": 3,
	/// 	},
	/// 	"hub2":
	/// 	{
	/// 		"hub1": 1,
	/// 		"job1": 4,
	/// 		"job2": 5,
	/// 	},
	/// 	"job1":
	/// 	{
	/// 		"hub1": 2,
	/// 		"hub2": 4,
	/// 		"job2": 6,
	/// 	},
	/// 	"job2":
	/// 	{
	/// 		"hub1": 3,
	/// 		"hub2": 5,
	/// 		"job1": 6,
	/// 	}
	/// }
	/// </summary>
	public Dictionary<string, Dictionary<string, double>>? Distances { get; set; }

	/// <summary>
	/// Every primary object in the problem, keyed by its unique ID.
	/// </summary>
	internal Dictionary<string, IAmUnique> ValidatedEntitiesById { get; private init; } = [];

	/// <summary>
	/// Used to store the validation context and throw errors on failures.
	/// </summary>
	internal readonly ValidationErrorBuilder ErrorBuilder = new();

	public override string ToString()
	{
		return Serialize();
	}

	/// <summary>
	/// Serializes the problem to a one-line JSON string.
	/// </summary>
	/// <returns></returns>
	public string Serialize()
	{
		return JsonSerializer.Serialize(this, JsonSerializerOptionsProvider.Default);
	}

	public JsonNode AsJsonNode()
	{
		return JsonNode.Parse(Serialize())!;
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

		if (ValidatedEntitiesById.Count > 0)
		{
			Log.Debug(
				"Problem already validated; WorkerCount: {WorkerCount} JobCount: {JobCount} ToolCount {ToolCount} MetricCount {MetricCount}",
				ValidatedEntitiesById.Values.OfType<Worker>().Count(),
				ValidatedEntitiesById.Values.OfType<Job>().Count(),
				ValidatedEntitiesById.Values.OfType<Tool>().Count(),
				ValidatedEntitiesById.Values.OfType<Metric>().Count()
			);
			return this;
		}

		// Order is important here
		ValidateTools();
		ValidateMetrics();
		ValidatePlaces();
		ValidateWorkers();

		return this;
	}

	private void ValidateTools()
	{
		var tools = Tools;
		ErrorBuilder.AddContext(nameof(tools));
		EnsureSome(tools);
		foreach (var (toolIndex, tool) in Tools.Enumerate())
		{
			Log.Debug("{field}#{index}={tool}", nameof(tools), toolIndex, tool);
			ErrorBuilder.AddContext(toolIndex);

			EnsureUniqueId(tool.Id, tool);

			var defaultWorkTime = tool.DefaultWorkTime;
			EnsurePositive(defaultWorkTime, nameof(defaultWorkTime));

			var defaultCompletionChance = tool.DefaultCompletionChance;
			EnsurePositive(defaultCompletionChance, nameof(defaultCompletionChance));

			ErrorBuilder.PopContext();
		}
		ErrorBuilder.PopContext();
	}

	private void ValidatePlaces()
	{
		if (!ValidatedEntitiesById.Values.OfType<Tool>().Any())
		{
			throw new InvalidOperationException($"call {nameof(ValidateTools)} before {nameof(ValidatePlaces)}");
		}
		if (!ValidatedEntitiesById.Values.OfType<Metric>().Any())
		{
			throw new InvalidOperationException($"call {nameof(ValidateMetrics)} before {nameof(ValidatePlaces)}");
		}

		ValidateHubs();
		ValidateJobs();
	}

	private void ValidateHubs()
	{
		var hubs = Hubs;
		ErrorBuilder.AddContext(nameof(hubs));
		foreach (var (hubIndex, hub) in hubs.Enumerate())
		{
			Log.Debug("{field}#{index}={entity}", nameof(hubs), hubIndex, hub);
			ErrorBuilder.AddContext(hubIndex);
			EnsureUniqueId(hub.Id, hub);

			ValidatePlaceLocation(hub);

			ErrorBuilder.PopContext();
		}
		ErrorBuilder.PopContext();
	}

	private void ValidateJobs()
	{
		var jobs = Jobs;
		ErrorBuilder.AddContext(nameof(jobs));
		EnsureSome(jobs);
		foreach (var (jobIndex, job) in jobs.Enumerate())
		{
			Log.Debug("{field}#{index}={entity}", nameof(jobs), jobIndex, job);
			ErrorBuilder.AddContext(jobIndex);
			EnsureUniqueId(job.Id, job);

			// Ensure the window's open and close times are valid.
			var arrivalWindow = job.ArrivalWindow;
			ErrorBuilder.AddContext(nameof(arrivalWindow));
			if (arrivalWindow.Open > DateTimeOffset.MinValue)
			{
				var open = arrivalWindow.Open;
				var close = arrivalWindow.Close;
				if (arrivalWindow.Close < arrivalWindow.Open)
				{
					throw ErrorBuilder.AddContext(nameof(close)).Build($"is before {nameof(open)}");
				}
			}
			ErrorBuilder.PopContext();

			ValidatePlaceLocation(job);

			ValidateJobTasks(job);

			ErrorBuilder.PopContext();
		}
		ErrorBuilder.PopContext();
	}

	private void ValidatePlaceLocation(Place place)
	{
		var location = place.Location;
		ErrorBuilder.AddContext(nameof(location));

		// Hubs always require locations when we need a distance matrix.
		if (place is Hub && location is null && IsDistanceMatrixRequired)
		{
			throw ErrorBuilder.Build(ValidationErrorType.Missing);
		}

		ErrorBuilder.PopContext();
	}

	/// <summary>
	/// Add a required visit task to the job's task list and then validate all tasks.
	/// </summary>
	/// <param name="job"></param>
	private void ValidateJobTasks(Job job)
	{
		var tasks = job.Tasks;
		ErrorBuilder.AddContext(nameof(tasks));
		EnsureSome(tasks);

		// Validate the tasks.
		foreach (var (taskIndex, task) in tasks.Enumerate())
		{
			Log.Debug("{field}#{index}={entity}", nameof(tasks), taskIndex, task);
			ErrorBuilder.AddContext(taskIndex);
			EnsureUniqueId(task.Id, task);
			task.Job = job;

			// Ensure the tool exists and add it to the task.
			var toolId = task.ToolId;
			task.Tool = EnsureEntityExists<Tool>(toolId, nameof(toolId));

			// Every user-defined task must have at least one valid reward.
			var rewards = task.Rewards;
			ErrorBuilder.AddContext(nameof(rewards));
			EnsureSome(rewards);
			foreach (var (rewardIndex, reward) in rewards.Enumerate())
			{
				ErrorBuilder.AddContext(rewardIndex);

				// The reward amount cannot be negative.
				var amount = reward.Amount;
				EnsureNotNegative(amount, nameof(amount));

				// Ensure the Metric exists and accumulate rewards for it.
				var metricId = reward.MetricId;
				var metric = EnsureEntityExists<Metric>(metricId, nameof(metricId));
				reward.Metric = metric;
				task.RewardsByMetric.TryAdd(metric, 0);
				task.RewardsByMetric[metric] += amount;

				ErrorBuilder.PopContext();
			}
			ErrorBuilder.PopContext(2);
		}
		ErrorBuilder.PopContext();
	}

	private void ValidateWorkers()
	{
		if (!ValidatedEntitiesById.Values.OfType<Tool>().Any())
		{
			throw new InvalidOperationException($"call {nameof(ValidateTools)} before {nameof(ValidateWorkers)}");
		}
		if (!ValidatedEntitiesById.Values.OfType<Job>().Any())
		{
			throw new InvalidOperationException($"call {nameof(ValidateJobs)} before {nameof(ValidateWorkers)}");
		}

		var workers = Workers;
		ErrorBuilder.AddContext(nameof(workers));
		EnsureSome(workers);
		foreach (var (workerIndex, worker) in workers.Enumerate())
		{
			Log.Debug("{field}#{index}={entity}", nameof(workers), workerIndex, worker);
			ErrorBuilder.AddContext(workerIndex);
			EnsureUniqueId(worker.Id, worker);

			ValidateWorkerTimeWindow(worker);
			ValidateWorkerHubs(worker);

			// Ensure the worker has a travel speed factor greater than zero.
			var travelSpeedFactor = worker.TravelSpeedFactor;
			ErrorBuilder.AddContext(nameof(travelSpeedFactor));
			if (travelSpeedFactor <= 0)
			{
				throw ErrorBuilder.Build(ValidationErrorType.LessThanOrEqualToZero);
			}
			ErrorBuilder.PopContext();

			ValidateWorkerCapabilities(worker);

			ErrorBuilder.PopContext();
		}
		ErrorBuilder.PopContext();
	}

	public void ValidateWorkerTimeWindow(Worker worker)
	{
		var earliestStartTime = worker.EarliestStartTime;
		var latestEndTime = worker.LatestEndTime;
		ErrorBuilder.AddContext(nameof(earliestStartTime));
		if (earliestStartTime > latestEndTime)
		{
			throw ErrorBuilder.Build($"is after {nameof(latestEndTime)}");
		}
		ErrorBuilder.PopContext();
	}

	/// <summary>
	/// Ensure the worker's start & end places are valid, and add them to the worker object.
	/// </summary>
	/// <param name="worker"></param>
	private void ValidateWorkerHubs(Worker worker)
	{
		var startHubId = worker.StartHubId;
		var startHub = EnsureEntityExists<Hub>(startHubId, nameof(startHubId));
		worker.StartHub = startHub;

		var endHubId = worker.EndHubId;
		var endHub = EnsureEntityExists<Hub>(endHubId, nameof(endHubId));
		worker.EndHub = endHub;
	}

	private void ValidateWorkerCapabilities(Worker worker)
	{
		var capabilities = worker.Capabilities;
		ErrorBuilder.AddContext(nameof(capabilities));
		EnsureSome(capabilities);
		foreach (var (capabilityIndex, capability) in capabilities.Enumerate())
		{
			ErrorBuilder.AddContext(capabilityIndex);
			capability.Worker = worker;

			// Ensure the Tool exists.
			var toolId = capability.ToolId;
			ErrorBuilder.AddContext(nameof(toolId));
			var tool = EnsureEntityExists<Tool>(toolId);
			if (!worker.CapabilitiesByTool.TryAdd(tool, capability))
			{
				throw ErrorBuilder.AddContext(toolId, "=").Build(ValidationErrorType.NotUnique);
			}
			capability.Tool = tool;
			ErrorBuilder.PopContext();

			// Use default work time if not provided.
			var workTime = capability.WorkTime;
			ErrorBuilder.AddContext(nameof(workTime));
			if (workTime is null)
			{
				capability.WorkTime = tool.DefaultWorkTime * capability.WorkTimeFactor;
			}
			else
			{
				EnsureNotNegative(workTime);
			}
			ErrorBuilder.PopContext();

			// Use default completion chance if not provided.
			var completionChance = capability.CompletionChance;
			ErrorBuilder.AddContext(nameof(completionChance));
			if (completionChance is null)
			{
				capability.CompletionChance = tool.DefaultCompletionChance;
			}
			else
			{
				EnsureNotNegative(completionChance);
			}
			ErrorBuilder.PopContext();

			ValidateCapabilityRewardFactors(capability);

			ErrorBuilder.PopContext();
		}
		ErrorBuilder.PopContext();
	}

	private void ValidateCapabilityRewardFactors(Capability capability)
	{
		var index = new Dictionary<Metric, MetricFactor>();

		var rewardFactors = capability.RewardFactors;
		ErrorBuilder.AddContext(nameof(rewardFactors));
		foreach (var (rewardFactorIndex, rewardFactor) in rewardFactors.Enumerate())
		{
			ErrorBuilder.AddContext(rewardFactorIndex);

			// Ensure the metric exists and only appears once.
			var metricId = rewardFactor.MetricId;
			ErrorBuilder.AddContext(nameof(metricId));
			var metric = EnsureEntityExists<Metric>(metricId);
			if (!index.TryAdd(metric, rewardFactor))
			{
				throw ErrorBuilder.AddContext(metricId, "=").Build(ValidationErrorType.NotUnique);
			}
			rewardFactor.Metric = metric;
			ErrorBuilder.PopContext();

			// Ensure the factor itself is at least 0.
			var factor = rewardFactor.Factor;
			EnsureNotNegative(factor, nameof(factor));

			ErrorBuilder.PopContext();
		}
		ErrorBuilder.PopContext();
	}

	private void ValidateMetrics()
	{
		var factorsByMetric = new Dictionary<MetricType, Metric>();
		var metrics = Metrics;
		ErrorBuilder.AddContext(nameof(metrics));
		EnsureSome(metrics);
		foreach (var (metricIndex, metric) in metrics.Enumerate())
		{
			Log.Debug("{field}#{index}={entity}", nameof(metrics), metricIndex, metric);
			ErrorBuilder.AddContext(metricIndex);
			EnsureUniqueId(metric.Id, metric);

			// Built-in metrics must be unique.
			var type = metric.Type;
			ErrorBuilder.AddContext(nameof(type));
			if (!MetricType.Custom.Equals(type) && !factorsByMetric.TryAdd(type, metric))
			{
				throw ErrorBuilder.AddContext(type.ToString(), "=").Build(ValidationErrorType.NotUnique);
			}
			ErrorBuilder.PopContext();

			var weight = metric.Weight;
			EnsureNotNegative(weight, nameof(weight));

			ErrorBuilder.PopContext();
		}
		ErrorBuilder.PopContext();
	}

	private void EnsureUniqueId(string? id, IAmUnique entity, string? field = null)
	{
		ErrorBuilder.AddContext(field ?? nameof(id));
		if (string.IsNullOrWhiteSpace(id))
		{
			throw ErrorBuilder.Build(ValidationErrorType.MissingOrEmpty);
		}
		if (!ValidatedEntitiesById.TryAdd(id, entity))
		{
			throw ErrorBuilder.AddContext(id, "=").Build(ValidationErrorType.NotUnique);
		}
		ErrorBuilder.PopContext();
	}

	private void EnsurePresent<T>(T? value, string? field = null)
	{
		if (field is not null)
		{
			ErrorBuilder.AddContext(field);
		}
		if (value is null)
		{
			throw ErrorBuilder.Build(ValidationErrorType.Missing);
		}
		if (field is not null)
		{
			ErrorBuilder.PopContext();
		}
	}

	private void EnsureNotNegative(double? value, string? field = null)
	{
		if (field is not null)
		{
			ErrorBuilder.AddContext(field);
		}
		EnsurePresent(value);
		if (value < 0)
		{
			throw ErrorBuilder.AddContext(value.Value, "=").Build(ValidationErrorType.LessThanZero);
		}
		if (field is not null)
		{
			ErrorBuilder.PopContext();
		}
	}

	/// <summary>
	/// Validates that a number is more than zero.
	/// </summary>
	/// <param name="value"></param>
	/// <param name="field"></param>
	private void EnsurePositive(double? value, string? field = null)
	{
		if (field is not null)
		{
			ErrorBuilder.AddContext(field);
		}
		EnsurePresent(value);
		if (value <= 0)
		{
			throw ErrorBuilder.AddContext(value.Value, "=").Build(ValidationErrorType.LessThanOrEqualToZero);
		}
		if (field is not null)
		{
			ErrorBuilder.PopContext();
		}
	}

	/// <summary>
	/// Validates that a list contains at least one item.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <param name="list"></param>
	/// <param name="field"></param>
	private void EnsureSome<T>(IList<T>? list, string? field = null)
	{
		if (field is not null)
		{
			ErrorBuilder.AddContext(field);
		}
		if (list is not { Count: > 0 })
		{
			throw ErrorBuilder.Build(ValidationErrorType.MissingOrEmpty);
		}
		if (field is not null)
		{
			ErrorBuilder.PopContext();
		}
	}

	/// <summary>
	/// Validates that a string contains at least one non-whitespace character.
	/// </summary>
	/// <param name="value">The string to validate.</param>
	/// <param name="field">If provided, will be temporarily added to the errorBuilder context.</param>
	private void EnsureSome(string? value, string? field = null)
	{
		if (field is not null)
		{
			ErrorBuilder.AddContext(field);
		}
		if (string.IsNullOrWhiteSpace(value))
		{
			throw ErrorBuilder.Build(ValidationErrorType.MissingOrEmpty);
		}
		if (field is not null)
		{
			ErrorBuilder.PopContext();
		}
	}

	private T EnsureEntityExists<T>(string id, string? field = null)
	{
		if (field is not null)
		{
			ErrorBuilder.AddContext(field);
		}
		EnsureSome(id);
		if (!ValidatedEntitiesById.TryGetValue(id, out IAmUnique? obj) || obj is not T entity)
		{
			throw ErrorBuilder.AddContext(id, "=").Build(ValidationErrorType.Unrecognized);
		}
		if (field is not null)
		{
			ErrorBuilder.PopContext();
		}
		return entity;
	}

	private void EnsureExpectedValue<T>(T expected, T other, string? field = null)
	{
		if (expected is null && other is null)
		{
			return;
		}
		if (field is not null)
		{
			ErrorBuilder.AddContext(field);
		}
		if (expected is null)
		{
			throw ErrorBuilder.Build("is expected to be null");
		}
		if (expected.Equals(other))
		{
			throw ErrorBuilder.Build($"is expected to be {expected}");
		}
		if (field is not null)
		{
			ErrorBuilder.PopContext();
		}
	}
}
