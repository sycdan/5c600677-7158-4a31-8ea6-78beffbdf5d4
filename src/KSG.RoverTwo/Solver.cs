using Google.OrTools.ConstraintSolver;
using KSG.RoverTwo.Enums;
using KSG.RoverTwo.Exceptions;
using KSG.RoverTwo.Extensions;
using KSG.RoverTwo.Helpers;
using KSG.RoverTwo.Models;
using MathNet.Numerics.LinearAlgebra;
using Serilog;
using Window = KSG.RoverTwo.Models.Window;

namespace KSG.RoverTwo;

public class Solver
{
	/// <summary>
	/// The problem being solved.
	/// </summary>
	public Problem Problem { get; private init; }

	/// <summary>
	/// All distances in the problem are multiplied by this.
	/// </summary>
	internal double DistanceFactor => ConvertDistance.Factors[Problem.DistanceUnit];

	/// <summary>
	/// the earliest time that any event can occur in the solution.
	/// </summary>
	public DateTimeOffset TZero { get; private init; }

	/// <summary>
	/// Meters between nodes.
	/// </summary>
	public Matrix<double> DistanceMatrix { get; private init; }

	/// <summary>
	/// Seconds between nodes.
	/// </summary>
	public Matrix<double> TravelTimeMatrix { get; private init; }

	/// <summary>
	/// Any value other than 0 is considered invalid.
	/// </summary>
	public Matrix<double> InvalidTransitMatrix { get; private init; }

	/// <summary>
	/// Places and tasks which need to be visited and completed respectively.
	/// </summary>
	public List<Node> Nodes { get; private init; } = [];
	internal IEnumerable<Node> JobNodes => Nodes.Where(n => n.IsJob);

	/// <summary>
	/// How many rows and columns should each matrix have.
	/// </summary>
	public int Size => Nodes.Count;

	/// <summary>
	/// Every vehicle being considered in the problem.
	/// </summary>
	public List<Vehicle> Vehicles { get; private init; } = [];

	/// <summary>
	/// How much to weight each metric in the objective function.
	/// </summary>
	public Dictionary<Metric, double> MetricWeights { get; private init; } = [];

	/// <summary>
	/// Determines the decimal precision we carry over from the double matrices.
	/// e.g. a cost of 0.123456 will equal 123456.
	/// </summary>
	private const long COST_FACTOR_SCALE = 1000000;
	private const string DIMENSION_TIME = "SecondsSinceTZero";
	private RoutingDimension TimeDimension { get; init; }
	private RoutingIndexManager Manager { get; init; }
	private RoutingModel Routing { get; init; }
	private RoutingSearchParameters SearchParameters { get; init; }

	public Solver(Problem problem)
	{
		Problem = problem.Validate();

		TZero = DetermineTzero();
		PopulateNodes();
		PopulateMetricWeights();
		DistanceMatrix = BuildDistanceMatrix();
		TravelTimeMatrix = BuildTravelTimeMatrix();
		InvalidTransitMatrix = BuildInvalidTransitMatrix();
		PopulateVehicles();

		SearchParameters = BuildSearchParameters();
		Manager = BuildRoutingManager();
		Routing = BuildRoutingModel();
		TimeDimension = BuildTimeDimension();
		ApplyObjectiveFunction();
		ApplyVehicleVisitRules();
		ApplyPrecedenceRules();
	}

	/// <summary>
	/// The start time of the problem will be either the earliest worker start time,
	/// or the earliest job window open time, whichever is earlier.
	/// If no times are defined, it will default to the minimum possible timestamp value.
	/// </summary>
	/// <returns></returns>
	private DateTimeOffset DetermineTzero()
	{
		var tZero = DateTimeOffset.MaxValue;
		var minWorkerStart = DateTimeOffset.MaxValue;
		foreach (var worker in Problem.Workers)
		{
			if (worker.EarliestStartTime is not null && worker.EarliestStartTime < minWorkerStart)
			{
				minWorkerStart = worker.EarliestStartTime.Value;
			}
		}
		if (minWorkerStart < tZero)
		{
			tZero = minWorkerStart;
		}
		foreach (var job in Problem.Jobs)
		{
			if (job.ArrivalWindow.Open < tZero)
			{
				tZero = job.ArrivalWindow.Open;
			}
			if (job.ArrivalWindow.Close < minWorkerStart)
			{
				Log.Warning(
					"Arrival window for {job} closes before any worker starts their route, so it will be optional.",
					job
				);
				job.Optional = true;
			}
		}
		if (minWorkerStart == DateTimeOffset.MaxValue)
		{
			Log.Warning("Cannot determine {field} because no worker has an earliest start time.", nameof(TZero));
			tZero = DateTimeOffset.MinValue;
		}
		Log.Debug("{field}: {value}", nameof(tZero), tZero);
		return tZero;
	}

	private void PopulateVehicles()
	{
		// Collect all the metric values and build a cost matrix for each vehicle.
		foreach (var worker in Problem.Workers)
		{
			var vehicle = new Vehicle(Vehicles.Count, worker, Size);
			PopulateVehicleMatrices(vehicle);
			Vehicles.Add(vehicle);
		}

		// Find the maximum value for each metric across all vehicles.
		var vehicleMaximumsByMetric = MetricWeights.Keys.ToDictionary(
			cf => cf,
			cf => Vehicles.Select(v => v.MetricMatrices[cf].MaxValue()).Max()
		);

		// Finally build the cost matrix for each vehicle.
		foreach (var vehicle in Vehicles)
		{
			PopulateVehicleCostMatrix(vehicle, vehicleMaximumsByMetric);
		}
	}

	/// <summary>
	/// Create nodes for each place in the problem.
	/// The initial node will have an arrival window and all required tasks.
	/// Subsequent nodes will each have one optional task, so each may be skipped.
	/// </summary>
	/// <param name="Problem"></param>
	private void PopulateNodes()
	{
		Nodes.Clear();

		// Hubs do not have tasks.
		foreach (var hub in Problem.Hubs)
		{
			Nodes.Add(new Node(id: Nodes.Count, place: hub, tasks: []));
		}

		foreach (var job in Problem.Jobs)
		{
			// Add the required tasks to the initial node for the place, with its arrival time window.
			var requiredTasks = job.Tasks.Where(t => !t.Optional).OrderBy(t => t.Order).ToList();
			Nodes.Add(
				new Node(
					id: Nodes.Count,
					place: job,
					tasks: requiredTasks,
					skippable: job.Optional,
					timeWindow: job.ArrivalWindow.RelativeTo(TZero)
				)
			);

			// Add a node for each optional task, so that any number of them may be skipped.
			var optionalTasks = job.Tasks.Where(t => t.Optional).OrderBy(t => t.Order);
			foreach (var task in optionalTasks)
			{
				Nodes.Add(new Node(id: Nodes.Count, place: job, tasks: [task], skippable: true));
			}
		}
	}

	/// <summary>
	/// Normalizes the metric weights in the problem so that their sum equals 1.
	/// </summary>
	/// <param name="Problem"></param>
	private void PopulateMetricWeights()
	{
		double totalWeight = Problem.Metrics.Sum(f => f.Weight);
		foreach (var metric in Problem.Metrics)
		{
			MetricWeights[metric] = metric.Weight / totalWeight;
		}
	}

	public static string? NoCostReason(Node a, Node b)
	{
		var isSamePlace = a.Place.Equals(b.Place);
		var aHasNoLocation = a.Place.Location is null;
		var bHasNoLocation = b.Place.Location is null;
		if (isSamePlace || aHasNoLocation || bHasNoLocation)
		{
			return isSamePlace ? "the places are the same"
				: aHasNoLocation ? $"{a.Place} has no location"
				: $"{a.Place} has no location";
		}
		return null;
	}

	/// <summary>
	/// Distance is tracked in meters, so we need to potentially convert from what the problem has.
	/// </summary>
	/// <param name="Problem"></param>
	/// <param name="nodes"></param>
	/// <returns></returns>
	public Matrix<double> BuildDistanceMatrix()
	{
		if (Size == 0)
		{
			throw new ApplicationException($"cannot build {nameof(DistanceMatrix)} of {nameof(Size)} 0");
		}
		var matrix = Matrix<double>.Build.Dense(Size, Size, 0);
		if (!Problem.IsDistanceMatrixRequired)
		{
			Log.Information("{attribute} not required", nameof(DistanceMatrix));
			return matrix;
		}
		Log.Information("building {attribute} from location coordinates", nameof(DistanceMatrix));
		foreach (var (a, fromNode) in Nodes.Enumerate())
		{
			foreach (var (b, toNode) in Nodes.Enumerate(a))
			{
				var noDistanceReason = NoCostReason(fromNode, toNode);
				if (noDistanceReason is null)
				{
					if (RoutingEngine.Simple.Equals(Problem.Engine))
					{
						matrix[a, b] = fromNode.Place.Location!.ManhattanDistanceTo(toNode.Place.Location!);
					}
					else
					{
						throw new NotImplementedException(nameof(Problem.Engine));
					}
				}
				Log.Verbose(
					"distance from #{a} {fromNode} @ {fromLocation} to #{b} {toNode} @ {toLocation} is {distance} {distanceUnit} {reason}",
					a,
					fromNode.Place,
					fromNode.Place.Location,
					b,
					toNode.Place,
					toNode.Place.Location,
					matrix[a, b],
					Problem.DistanceUnit,
					noDistanceReason is null ? "" : $" because {noDistanceReason}"
				);
			}
		}
		return matrix.Multiply(ConvertDistance.Factors[Problem.DistanceUnit]);
	}

	/// <summary>
	/// Time is expected in seconds, so we need to potentially convert from what the problem has.
	/// </summary>
	/// <param name="Problem"></param>
	/// <returns>values in seconds</returns>
	public Matrix<double> BuildTravelTimeMatrix()
	{
		if (Size == 0)
		{
			throw new ApplicationException($"cannot build {nameof(TravelTimeMatrix)} of {nameof(Size)} 0");
		}
		var matrix = Matrix<double>.Build.Dense(Size, Size, 0);
		if (!Problem.IsTravelTimeMatrixRequired)
		{
			Log.Information("{attribute} matrix not required", nameof(TravelTimeMatrix));
			return matrix;
		}
		Log.Information(
			"building {attribute} from {source} at {distanceFactor} meters per {distanceUnit}",
			nameof(TravelTimeMatrix),
			nameof(DistanceMatrix),
			DistanceFactor,
			Problem.DistanceUnit
		);
		foreach (var (a, fromNode) in Nodes.Enumerate())
		{
			foreach (var (b, toNode) in Nodes.Enumerate(a))
			{
				double travelTime = 0;
				var meters = DistanceMatrix[a, b];
				var noTravelTimeReason = NoCostReason(fromNode, toNode);
				if (noTravelTimeReason is null)
				{
					var distanceUnits = meters / DistanceFactor;
					travelTime = distanceUnits / Problem.DefaultTravelSpeed;
				}
				Log.Verbose(
					"{field} for {meters} meter{s} from #{a} {fromPlace} to #{b} {toPlace} is {travelTime} {timeUnit}",
					nameof(travelTime),
					meters,
					meters == 1 ? "" : "s",
					a,
					fromNode.Place,
					b,
					toNode.Place,
					travelTime,
					Problem.TimeUnit
				);
				matrix[a, b] = travelTime;
			}
		}
		return matrix.Multiply(ConvertTime.Factors[Problem.TimeUnit]);
	}

	/// <summary>
	/// When departing a node, simulate completing tasks and earning rewards.
	/// Rewards earned are the task's defaults, modified by the capability reward factor.
	/// </summary>
	/// <param name="vehicle">The vehicle visiting the node.</param>
	/// <param name="node">The node being visited by the vehicle.</param>
	/// <returns>A list of completed work.</returns>
	/// <exception cref="InvalidOperationException">If not all required data elements are available.</exception>
	private List<Completion> SimulateWork(Vehicle vehicle, Node node)
	{
		var completions = new List<Completion>();
		foreach (var task in node.Tasks)
		{
			var job = task.Job ?? throw new InvalidOperationException($"{task} does not have a {nameof(task.Job)}");
			var tool = task.Tool ?? throw new InvalidOperationException($"{task} does not have a {nameof(task.Tool)}");
			var capability = vehicle.Driver.CapabilitiesByTool.GetValueOrDefault(tool);

			// Get the default rewards for the task.
			var possibleRewards = task.RewardsByMetric.ToDictionary();
			var earnedRewards = new Dictionary<Metric, double>();

			// Determine whether the task was completed, and log earned/missed rewards.
			var completionChance = capability is null ? 0 : capability.CompletionChance ?? tool.DefaultCompletionChance;
			if (capability is not null && Random.Shared.NextDouble() < completionChance)
			{
				// Factor the work time.
				var workTime = capability.WorkTime ?? tool.DefaultWorkTime;
				var workSeconds = ConvertTime.ToSeconds(workTime, Problem.TimeUnit).AsLong();

				// Apply the vehicle's reward factor for the tool being used.
				foreach (var (metric, reward) in possibleRewards)
				{
					var rewardFactor = capability.RewardFactors.FirstOrDefault(rf => metric.Equals(rf.Metric));
					var factor = rewardFactor is null ? 1 : rewardFactor.Factor;
					earnedRewards[metric] = possibleRewards[metric] * factor;
				}
				Log.Verbose(
					"{worker} used {tool} for {workTime} second{s} to {task} at {place} and earned {rewards}",
					vehicle.Driver,
					tool,
					workSeconds,
					workSeconds is 1 ? "" : "s",
					task,
					job,
					earnedRewards.Count > 0 ? earnedRewards.Select(x => $"{x.Key.Id}:{x.Value}") : "no rewards"
				);
				completions.Add(
					new Completion
					{
						Worker = vehicle.Driver,
						Task = task,
						Job = job,
						WorkSeconds = workSeconds,
						EarnedRewards = earnedRewards,
					}
				);
			}
			else
			{
				var reason =
					completionChance > 0
						? $"completion chance of {completionChance:P}%"
						: $"lack of capability with {tool}";
				Log.Verbose(
					"{worker} skipped {task} at {place} due to {reason} and missed {rewards}",
					vehicle.Driver,
					task,
					job,
					reason,
					possibleRewards.Count > 0 ? possibleRewards.Select(x => $"{x.Key.Id}:{x.Value}") : "no rewards"
				);
			}
		}
		return completions;
	}

	public void PopulateVehicleMatrices(Vehicle vehicle)
	{
		// Initialize all metrics for this vehicle with blank matrices.
		vehicle.TimeMatrix.Clear();
		foreach (var metric in MetricWeights.Keys)
		{
			vehicle.MetricMatrices[metric] = Matrix<double>.Build.Dense(Size, Size, 0);
		}

		// Traverse the network, recording time spent and rewards earned.
		foreach (var (a, fromNode) in Nodes.Enumerate())
		{
			foreach (var (b, toNode) in Nodes.Enumerate(a))
			{
				// Populate the completion matrix with the tasks completed at A.
				var completions = SimulateWork(vehicle, fromNode);
				vehicle.WorkMatrix[a, b] = completions;

				foreach (var metric in MetricWeights.Keys)
				{
					// Get a reference to the matrix we'll be populating.
					var matrix = vehicle.MetricMatrices[metric];

					// Source the value for this transit based on the metric.
					if (MetricType.Distance.Equals(metric.Type))
					{
						matrix[a, b] += DistanceMatrix[a, b];
					}
					else if (MetricType.WorkTime.Equals(metric.Type))
					{
						// Track work time spent on completed work at A.
						if (completions is not null)
						{
							var workSeconds = completions.Sum(c => c.WorkSeconds);
							matrix[a, b] += workSeconds;
							vehicle.TimeMatrix[a, b] += workSeconds;
						}
					}
					else if (MetricType.TravelTime.Equals(metric.Type))
					{
						var travelTime = (long)Math.Round(TravelTimeMatrix[a, b] / vehicle.Driver.TravelSpeedFactor);
						matrix[a, b] += travelTime;
						vehicle.TimeMatrix[a, b] += travelTime;
					}
					else if (MetricType.Custom.Equals(metric.Type))
					{
						// Track rewards earned for completed work at A.
						if (completions is not null)
						{
							foreach (var completion in completions)
							{
								if (completion.EarnedRewards.TryGetValue(metric, out var reward))
								{
									matrix[a, b] += reward;
								}
							}
						}
					}
					else
					{
						throw new NotImplementedException($"{metric} is not a valid metric for vehicle matrices");
					}
				}
			}
		}
	}

	public void PopulateVehicleCostMatrix(Vehicle vehicle, Dictionary<Metric, double> vehicleMaximumsByMetric)
	{
		var costMatrix = Matrix<double>.Build.Dense(Size, Size, 0);
		foreach (var (metric, vehicleMaximum) in vehicleMaximumsByMetric.Where(x => x.Value > 0))
		{
			// Normalize the raw matrix to a scale of 1 for easier weighting.
			var rawMatrix = vehicle.MetricMatrices[metric];
			var normalizedMatrix = rawMatrix.Multiply(1 / vehicleMaximum);

			// If this is being maximized, the "cost" represents missing out on rewards.
			if (MetricMode.Maximize.Equals(metric.Mode))
			{
				// At this point the maximum value is 1, so we can subtract 1 from the matrix to invert it.
				normalizedMatrix = normalizedMatrix.Subtract(1).PointwiseAbs();
			}

			// Weight this slice of the cost pie and add it to any existing costs.
			costMatrix += normalizedMatrix.Multiply(MetricWeights[metric]);
		}

		// Now we need to scale and longify the costs before the matrix can be used in a transit callback.
		vehicle.CostMatrix.Clear();
		foreach (var (a, fromNode) in Nodes.Enumerate())
		{
			foreach (var (b, toNode) in Nodes.Enumerate(a))
			{
				vehicle.CostMatrix[a, b] = (long)Math.Round(costMatrix[a, b] * COST_FACTOR_SCALE);
			}
		}
	}

	public RoutingIndexManager BuildRoutingManager()
	{
		if (Nodes.Count < 1)
		{
			throw new ApplicationException($"{nameof(Nodes)} cannot be empty");
		}
		if (Vehicles.Count < 1)
		{
			throw new ApplicationException($"{nameof(Vehicles)} cannot be empty");
		}
		var starts = new List<int>();
		var ends = new List<int>();
		foreach (var v in Vehicles)
		{
			var start = Nodes.FindIndex(n => v.Driver.StartHubId == n.Place.Id);
			if (start == -1)
			{
				throw new ApplicationException(
					$"{nameof(v.Driver.StartHubId)} {v.Driver.StartHubId} not found for vehicle {v}"
				);
			}
			starts.Add(start);
			var end = Nodes.FindIndex(n => v.Driver.EndHubId == n.Place.Id);
			if (end == -1)
			{
				throw new ApplicationException(
					$"{nameof(v.Driver.EndHubId)} {v.Driver.EndHubId} not found for vehicle {v}"
				);
			}
			ends.Add(end);
		}
		return new RoutingIndexManager(Nodes.Count, Vehicles.Count, [.. starts], [.. ends]);
	}

	public RoutingSearchParameters BuildSearchParameters()
	{
		RoutingSearchParameters searchParameters =
			operations_research_constraint_solver.DefaultRoutingSearchParameters();
		searchParameters.FirstSolutionStrategy = FirstSolutionStrategy.Types.Value.Automatic;
		searchParameters.LocalSearchMetaheuristic = LocalSearchMetaheuristic.Types.Value.Automatic;
		searchParameters.TimeLimit = new() { Seconds = Problem.TimeoutSeconds };
		return searchParameters;
	}

	public RoutingModel BuildRoutingModel()
	{
		var routing = new RoutingModel(Manager);
		return routing;
	}

	public RoutingDimension BuildTimeDimension()
	{
		var transitCallbackIndices = new List<int>();
		foreach (var vehicle in Vehicles)
		{
			var timeTransitCallbackIndex = Routing.RegisterTransitCallback(
				(fromIndex, toIndex) =>
				{
					var a = Manager.IndexToNode(fromIndex);
					var b = Manager.IndexToNode(toIndex);
					return vehicle.TimeMatrix[a, b];
				}
			);
			transitCallbackIndices.Add(timeTransitCallbackIndex);
		}

		var maxIdleSeconds = ConvertTime.ToSeconds(Problem.MaxIdleTime, Problem.TimeUnit).AsLong();
		Routing.AddDimensionWithVehicleTransits(
			[.. transitCallbackIndices],
			maxIdleSeconds, // max wait time at each node for arrival window to open
			long.MaxValue, // max total time per vehicle
			false, // don't force all vehicles to start at the same time
			DIMENSION_TIME
		);
		var timeDimension = Routing.GetDimensionOrDie(DIMENSION_TIME);

		// Set arrival time windows for all jobs.
		foreach (var node in JobNodes)
		{
			if (node.TimeWindow is not null)
			{
				var open = node.TimeWindow.Value.Open;
				var close = node.TimeWindow.Value.Close;
				if (open >= 0 && close >= 0 && open < close)
				{
					var index = Manager.NodeToIndex(node.Id);
					timeDimension.CumulVar(index).SetRange(open, close);
				}
				else
				{
					throw new InvalidOperationException($"Invalid time window for {node}");
				}
			}
		}

		// Set arrival time windows for hubs.
		foreach (var vehicle in Vehicles)
		{
			var worker = vehicle.Driver;
			var workWindow = Window.From(
				worker.EarliestStartTime ?? TZero,
				worker.LatestEndTime ?? DateTimeOffset.MaxValue
			);
			var (workWindowOpenTime, workWindowCloseTime) = workWindow.RelativeTo(TZero);

			if (worker.EarliestStartTime is not null)
			{
				var startIndex = Routing.Start(vehicle.Id);
				timeDimension.CumulVar(startIndex).SetRange(workWindowOpenTime, workWindowCloseTime);
			}

			if (worker.LatestEndTime is not null)
			{
				var endIndex = Routing.End(vehicle.Id);
				timeDimension.CumulVar(endIndex).SetRange(workWindowOpenTime, workWindowCloseTime);
			}
		}

		return timeDimension;
	}

	public void ApplyVehicleVisitRules()
	{
		foreach (var node in JobNodes)
		{
			if (node.Place is not Job job)
			{
				continue;
			}
			var nodeIndex = Manager.NodeToIndex(node.Id);

			// Assume all vehicles can visit to start with.
			var validVehicles = Vehicles.ToList();

			// Exclude vehicles where the driver lacks capability with any required tool.
			var requiredTools = job.Tasks.Where(x => !x.Optional).Select(x => x.Tool).Distinct().ToList();
			Log.Verbose("required tools for {job}: {requiredTools}", job, requiredTools);
			validVehicles = validVehicles
				.Where(v =>
					requiredTools.All(t => v.Driver.CapabilitiesByTool.GetValueOrDefault(t!)?.CompletionChance > 0)
				)
				.ToList();

			// There must be at least one valid vehicle remaining, or the problem is unsolvable.
			if (validVehicles.Count == 0)
			{
				throw new NoViableWorkerException(job);
			}

			// Set the allowed vehicles for the node.
			var validVehicleIds = validVehicles.Select(v => v.Id).ToArray();
			Routing.SetAllowedVehiclesForIndex(validVehicleIds, nodeIndex);
			Log.Verbose("validVehicleIds for {job}: {validVehicleIds}", job, validVehicleIds);

			// Add disjunctions for any optional nodes, allowing them to be skipped at a cost.
			if (node.IsSkippable)
			{
				var penalty = COST_FACTOR_SCALE * (node.Tasks.Count + 1);
				Log.Debug("penalty for skipping {node}: {penalty}", node, penalty);
				Routing.AddDisjunction([nodeIndex], penalty);
			}
		}
	}

	public void ApplyObjectiveFunction()
	{
		foreach (var vehicle in Vehicles)
		{
			var transitCallbackIndex = Routing.RegisterTransitCallback(
				(fromIndex, toIndex) =>
				{
					var a = Manager.IndexToNode(fromIndex);
					var b = Manager.IndexToNode(toIndex);
					return vehicle.CostMatrix[a, b];
				}
			);
			Log.Verbose("transitCallbackIndex for {vehicle}: {transitCallbackIndex}", vehicle, transitCallbackIndex);
			Routing.SetArcCostEvaluatorOfVehicle(transitCallbackIndex, vehicle.Id);
		}
	}

	/// <summary>
	/// A worker must visit the first node in a sequence (when there are multiple tasks at a place)
	/// before they are allowed to visit any other nodes in the sequence.
	/// Further, they must visit nodes in ascending order of their index in the sequence.
	/// </summary>
	public void ApplyPrecedenceRules()
	{
		var costs = InvalidTransitMatrix.AsLongArray();
		var transitCallbackIndex = Routing.RegisterTransitCallback(
			(fromIndex, toIndex) =>
			{
				var a = Manager.IndexToNode(fromIndex);
				var b = Manager.IndexToNode(toIndex);
				return costs[a, b];
			}
		);
		Routing.AddDimension(transitCallbackIndex, 0, 0, true, "Precedence");
	}

	/// <summary>
	/// Create a matrix of costs for precedence rules.
	/// Any invalid transitions are assigned a cost of 1.
	/// everything else is assigned a cost of 0.
	/// </summary>
	/// <returns></returns>
	internal Matrix<double> BuildInvalidTransitMatrix()
	{
		// All transits start out valid
		var matrix = Matrix<double>.Build.Dense(Nodes.Count, Nodes.Count, 0);
		foreach (var (a, fromNode) in Nodes.Enumerate())
		{
			foreach (var (b, toNode) in Nodes.Enumerate(a))
			{
				matrix[a, b] = IsValidTransit(fromNode, toNode) ? 0 : 1;
			}
		}
		return matrix;
	}

	private static bool IsValidTransit(Node fromNode, Node toNode)
	{
		var fromPlace = fromNode.Place;
		var fromTask = fromNode.Tasks.FirstOrDefault();
		var toPlace = toNode.Place;
		var toTask = toNode.Tasks.FirstOrDefault();

		// Going to a place with no tasks is always valid.
		if (toPlace is Hub || toTask is null)
		{
			return true;
		}

		// If going to the first task at a place, the worker must be coming from a different place.
		if (toTask.Order == 0)
		{
			return !fromPlace.Equals(toPlace);
		}

		// If going to a task other than the first, the worker must be coming from an earlier task at the same place.
		if (fromPlace.Equals(toPlace) && fromTask is not null && fromTask.Order < toTask.Order)
		{
			return true;
		}

		return false;
	}

	public Solution Solve()
	{
		var solution = new Solution();
		solution.SkippedJobs.AddRange(Problem.Jobs);
		foreach (var metric in MetricWeights.Keys)
		{
			solution.TotalMetrics[metric] = 0;
		}

		// Look for a solution!
		Log.Information("solving problem...");
		var assignment = Routing.SolveWithParameters(SearchParameters);

		// Exit early if no solution was found
		if (assignment is null)
		{
			Log.Warning("no solution found");
			return solution;
		}

		solution.TotalCost = assignment.ObjectiveValue();
		Log.Information("cheapest route found costs {totalCost}", solution.TotalCost);

		// Extract the routes for all vehicles from the solution and determine cost per vehicle.
		var transitsByVehicle = new Dictionary<Vehicle, List<(Node a, Node b, long c, long t)>>();
		var routeEndTimes = new Dictionary<Vehicle, long>();
		foreach (var vehicle in Vehicles)
		{
			transitsByVehicle.Add(vehicle, []);
			var index = Routing.Start(vehicle.Id);
			while (!Routing.IsEnd(index))
			{
				var arrivalTime = assignment.GetDimensionValueAt(index, TimeDimension);
				var node = Nodes[Manager.IndexToNode(index)];
				var nextIndex = assignment.Value(Routing.NextVar(index));
				var nextNode = Nodes[Manager.IndexToNode(nextIndex)];
				var transitCost = Routing.GetArcCostForVehicle(index, nextIndex, vehicle.Id);
				transitsByVehicle[vehicle].Add((node, nextNode, transitCost, arrivalTime));
				index = nextIndex;
			}
			routeEndTimes[vehicle] = assignment.GetDimensionValueAt(index, TimeDimension);
		}

		// Build the visit list from all the transits.
		foreach (var (vehicle, transits) in transitsByVehicle)
		{
			Visit? visit = null;
			foreach (var (transitIndex, (a, b, c, t)) in transits.Enumerate())
			{
				var time = TZero.AddSeconds(t);

				// Accrue all tracked metrics.
				foreach (var metric in MetricWeights.Keys)
				{
					var amount = vehicle.MetricMatrices[metric][a.Id, b.Id];
					switch (metric.Type)
					{
						case MetricType.Distance:
							amount = ConvertDistance.FromMeters(amount, Problem.DistanceUnit);
							break;
						case MetricType.TravelTime:
						case MetricType.WorkTime:
							amount = ConvertTime.FromSeconds(amount, Problem.TimeUnit);
							break;
					}
					solution.TotalMetrics[metric] += amount;
				}

				// Make a new visit as the vehicle arrives at each new place.
				var leavingStartHub = transitIndex == 0;
				var leavingPlace = visit is not null && !visit.Place.Equals(a.Place);
				if (leavingStartHub || leavingPlace)
				{
					visit = new Visit
					{
						Worker = vehicle.Driver,
						Place = a.Place,
						ArrivalTime = time,
					};

					// Only a departure time is necessary when leaving the start hub.
					if (leavingStartHub)
					{
						visit.ArrivalTime = null;
						visit.DepartureTime = time;
					}

					// When leaving a job, remove it from the list of skipped ones.
					if (a.Place is Job job)
					{
						solution.SkippedJobs.Remove(job);
					}

					solution.Visits.Add(visit);
				}

				// We shouldn't be able to get here without a visit.
				ArgumentNullException.ThrowIfNull(visit);

				// Record all tasks completed at this node, and accrue work time.
				var completions = vehicle.WorkMatrix[a.Id, b.Id];
				foreach (var completion in completions)
				{
					var task = completion.Task;
					visit.WorkSeconds += completion.WorkSeconds;
					foreach (var (metric, reward) in completion.EarnedRewards)
					{
						visit.EarnedRewards.TryAdd(metric, 0);
						visit.EarnedRewards[metric] += reward;
					}
					visit.CompletedTasks.Add(task);
				}

				// Update departure time.
				if (visit.ArrivalTime is not null)
				{
					visit.DepartureTime = visit.ArrivalTime.Value.AddSeconds(visit.WorkSeconds);
				}
			}

			// Add a visit for the last leg, with only an arrival time.
			var home = new Visit
			{
				Worker = vehicle.Driver,
				Place = transits[^1].b.Place,
				ArrivalTime = TZero.AddSeconds(routeEndTimes[vehicle]),
			};
			solution.Visits.Add(home);
		}

		return solution;
	}
}
