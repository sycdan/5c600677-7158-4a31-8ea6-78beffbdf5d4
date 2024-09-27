using Google.OrTools.ConstraintSolver;
using KSG.RoverTwo.Enums;
using KSG.RoverTwo.Extensions;
using KSG.RoverTwo.Models;
using MathNet.Numerics.LinearAlgebra;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Serilog;
using Task = KSG.RoverTwo.Models.Task;

namespace KSG.RoverTwo;

public class Solver
{
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

	/// <summary>
	/// How many rows and columns should each matrix have.
	/// </summary>
	public int Size => Nodes.Count;

	/// <summary>
	/// Every vehicle being considered in the problem.
	/// </summary>
	public List<Vehicle> Vehicles { get; private init; } = [];

	/// <summary>
	/// The global chance of a worker successfully using a tool to complete a task.
	/// </summary>
	public Dictionary<Tool, double> CompletionChances { get; private init; } = [];

	/// <summary>
	/// Every tool that can be used to complete a task.
	/// </summary>
	private IEnumerable<Tool> Tools => CompletionChances.Keys;

	/// <summary>
	/// How much to weight each metric in the objective function.
	/// </summary>
	public Dictionary<Metric, double> MetricWeights { get; private init; } = [];

	/// <summary>
	/// An internal tool used to track visits to places.
	/// </summary>
	internal Tool ArrivalTool { get; private init; } = new Tool { Name = "Vehicle", Delay = 0 };
	internal const string ARRIVAL_TASK_NAME = "Arrive";

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
		problem.Validate();

		TZero = problem.TZero;
		PopulateNodes(problem);
		PopulateMetricWeights(problem);
		PopulateCompletionChances(problem);
		DistanceMatrix = BuildDistanceMatrix(problem);
		TravelTimeMatrix = BuildTravelTimeMatrix(problem);
		InvalidTransitMatrix = BuildInvalidTransitMatrix();
		PopulateVehicles(problem);

		SearchParameters = BuildSearchParameters(problem);
		Manager = BuildRoutingManager();
		Routing = BuildRoutingModel();
		TimeDimension = BuildTimeDimension(problem);
		ApplyObjectiveFunction();
		ApplyVehicleVisitRules(problem);
		ApplyPrecedenceRules();
	}

	private void PopulateCompletionChances(Problem problem)
	{
		foreach (var tool in problem.Tools)
		{
			CompletionChances[tool] = tool.CompletionRate;
		}
	}

	private void PopulateVehicles(Problem problem)
	{
		if (CompletionChances.Count is 0)
		{
			throw new InvalidOperationException(
				$"call {nameof(PopulateCompletionChances)} before {nameof(PopulateVehicles)}"
			);
		}

		// Collect all the metric values and build a cost matrix for each vehicle.
		foreach (var worker in problem.Workers)
		{
			var vehicle = new Vehicle(Vehicles.Count, worker, Size);
			PopulateToolTimes(vehicle, problem);
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

	private void PopulateToolTimes(Vehicle vehicle, Problem problem)
	{
		foreach (var tool in Tools)
		{
			long toolTime = 0;
			vehicle.Driver.CapabilitiesByTool.TryGetValue(tool, out Capability? capability);
			if (capability is not null)
			{
				toolTime = (long)Math.Round(tool.Delay * capability.DelayFactor * problem.TimeFactor);
			}
			vehicle.ToolTimes[tool] = toolTime;
		}
	}

	/// <summary>
	/// Create nodes for each place in the problem.
	/// The initial node will have an arrival window and all required tasks.
	/// Subsequent nodes will each have one optional task, so each may be skipped.
	/// </summary>
	/// <param name="problem"></param>
	private void PopulateNodes(Problem problem)
	{
		Nodes.Clear();
		foreach (var place in problem.Places)
		{
			// Create the required arrival task for the place.
			var requiredTasks = new List<Task>
			{
				new()
				{
					Order = 0,
					Place = place,
					Tool = ArrivalTool,
					ToolId = ArrivalTool.Id,
					Name = ARRIVAL_TASK_NAME,
					Optional = false,
					Rewards = [],
				},
			};

			// Add all the required tasks from the problem.
			requiredTasks.AddRange(place.Tasks.Where(t => !t.Optional));

			// Add the required tasks to the initial node for the place, with its arrival time window.
			Nodes.Add(new Node(Nodes.Count, place, requiredTasks, place.ArrivalWindow.RelativeTo(TZero)));

			// Add a node for each optional task, so that any number of them may be skipped.
			foreach (var task in place.Tasks.Where(t => t.Optional))
			{
				Nodes.Add(new Node(Nodes.Count, place, [task]));
			}
		}
	}

	/// <summary>
	/// Normalizes the metric weights in the problem so that their sum equals 1.
	/// </summary>
	/// <param name="problem"></param>
	private void PopulateMetricWeights(Problem problem)
	{
		double totalWeight = problem.Metrics.Sum(f => f.Weight);
		foreach (var metric in problem.Metrics)
		{
			MetricWeights[metric] = metric.Weight / totalWeight;
		}
	}

	/// <summary>
	/// Distance is tracked in meters, so we need to potentially convert from what the problem has.
	/// </summary>
	/// <param name="problem"></param>
	/// <param name="nodes"></param>
	/// <returns></returns>
	public Matrix<double> BuildDistanceMatrix(Problem problem)
	{
		if (Size.Equals(0))
		{
			throw new ApplicationException($"cannot build {nameof(DistanceMatrix)} of {nameof(Size)} 0");
		}
		var matrix = Matrix<double>.Build.Dense(Size, Size, 0);
		if (!problem.IsDistanceMatrixRequired)
		{
			Log.Information("{attribute} not required", nameof(DistanceMatrix));
			return matrix;
		}
		Log.Information("building {attribute} from location coordinates", nameof(DistanceMatrix));
		if (!problem.DoAllPlacesHaveLocations)
		{
			throw new ApplicationException($"cannot build {nameof(DistanceMatrix)} unless all places have locations");
		}
		foreach (var (a, fromNode) in Nodes.Enumerate())
		{
			foreach (var (b, toNode) in Nodes.Enumerate(a))
			{
				if (RoutingEngine.Simple.Equals(problem.Engine))
				{
					matrix[a, b] = fromNode.Location!.ManhattanDistanceTo(toNode.Location!);
				}
				else
				{
					throw new NotImplementedException(nameof(problem.Engine));
				}
				Log.Verbose(
					"distance from {fromNode} ({fromLocation}) to {toNode} ({toLocation}) is {distance} {distanceUnit}s",
					fromNode.Place.Name,
					fromNode.Location,
					toNode.Place.Name,
					toNode.Location,
					matrix[a, b],
					problem.DistanceUnit
				);
			}
		}
		return matrix.Multiply(problem.DistanceFactor);
	}

	/// <summary>
	/// Time is expected in seconds, so we need to potentially convert from what the problem has.
	/// </summary>
	/// <param name="problem"></param>
	/// <returns>values in seconds</returns>
	public Matrix<double> BuildTravelTimeMatrix(Problem problem)
	{
		if (Size.Equals(0))
		{
			throw new ApplicationException($"cannot build {nameof(TravelTimeMatrix)} of {nameof(Size)} 0");
		}
		var matrix = Matrix<double>.Build.Dense(Size, Size, 0);
		if (!problem.IsTravelTimeMatrixRequired)
		{
			Log.Information("{attribute} matrix not required", nameof(TravelTimeMatrix));
			return matrix;
		}
		Log.Information(
			"building {attribute} from {source} at {distanceFactor} meters per {timeUnit}",
			nameof(TravelTimeMatrix),
			nameof(DistanceMatrix),
			problem.DistanceFactor,
			problem.DistanceUnit,
			problem.TimeUnit
		);
		foreach (var (a, fromNode) in Nodes.Enumerate())
		{
			foreach (var (b, toNode) in Nodes.Enumerate(a))
			{
				var meters = DistanceMatrix[a, b];
				var distanceUnits = meters / problem.DistanceFactor;
				matrix[a, b] = distanceUnits / problem.DefaultTravelSpeed;
				Log.Verbose(
					"Travel time from {a} to {b} is {travelTime} {timeUnit}s to cover {meters} meters ({distanceUnits} {distanceUnit}s)",
					fromNode.Place.Name,
					toNode.Place.Name,
					matrix[a, b],
					problem.TimeUnit,
					meters,
					distanceUnits,
					problem.DistanceUnit
				);
			}
		}
		return matrix.Multiply(problem.TimeFactor);
	}

	/// <summary>
	/// When departing a node, simulate completing tasks and earning rewards.
	///
	/// Possible rewards are a combination of the task's default rewards
	/// plus any vehicle-driver-specific rewards for visiting the place.
	/// The resulting total is factored by the vehicle's reward factor.
	///
	/// Note that this occurs when the vehicle is transiting away from the node.
	/// </summary>
	/// <param name="vehicle">The vehicle visiting the node.</param>
	/// <param name="node">The node being visited by the vehicle.</param>
	/// <returns></returns>
	/// <exception cref="InvalidOperationException">If not all required data elements are available.</exception>
	private List<Completion> SimulateWork(Vehicle vehicle, Node node)
	{
		var completions = new List<Completion>();
		foreach (var task in node.Tasks)
		{
			var tool = task.Tool ?? throw new InvalidOperationException($"{task} does not have a {nameof(task.Tool)}");
			var place =
				task.Place ?? throw new InvalidOperationException($"{task} does not have a {nameof(task.Place)}");
			var isArrivalTask = tool.Equals(ArrivalTool);
			// TODO allow custom arrival task time
			var workSeconds = isArrivalTask ? 1 : vehicle.ToolTimes.GetValueOrDefault(tool);
			var completionChance = isArrivalTask ? 1 : CompletionChances.GetValueOrDefault(tool);
			var possibleRewards = new Dictionary<Metric, double>();
			var earnedRewards = new Dictionary<Metric, double>();
			// TODO regroup/index reward modifiers on vehicle
			var rewardModifiers = vehicle.Driver.RewardModifiers;

			// Get the default rewards for the task.
			if (isArrivalTask)
			{
				// Add place-specific visit reward modifiers from the vehicle's driver.
				foreach (var rewardModifier in rewardModifiers)
				{
					if (place.Equals(rewardModifier.Place) && rewardModifier.Metric is { } metric)
					{
						possibleRewards.TryAdd(metric, 0);
						possibleRewards[metric] += rewardModifier.Amount ?? 0;
					}
				}
			}
			else
			{
				possibleRewards = task.RewardsByMetric.ToDictionary();
			}

			// Determine whether the task was completed, and log earned/missed rewards.
			if (workSeconds > 0 && Random.Shared.NextDouble() < completionChance)
			{
				// Apply the vehicle's reward factor for the tool being used.
				foreach (var (metric, reward) in possibleRewards)
				{
					var factor =
						rewardModifiers
							.Where(rm => metric.Equals(rm.Metric))
							.FirstOrDefault(rm => tool.Equals(rm.Tool))
							?.Factor ?? 1;
					earnedRewards[metric] = Math.Max(possibleRewards[metric], 0) * factor;
				}
				Log.Verbose(
					"{worker} used {tool} for {workTime} second{s} to {task} at {place} and earned {rewards}",
					vehicle.Driver,
					tool,
					workSeconds,
					workSeconds is 1 ? "" : "s",
					task,
					place,
					earnedRewards.Count > 0 ? earnedRewards.Select(x => $"{x.Key.Id}:{x.Value}") : "no rewards"
				);
			}
			else
			{
				var reason = workSeconds.Equals(0)
					? $"lack of capability with {tool}"
					: $"completion chance of {completionChance:P}%";
				Log.Verbose(
					"{worker} skipped {task} at {place} due to {reason} and missed {rewards}",
					vehicle.Driver,
					task,
					place,
					reason,
					possibleRewards.Count > 0 ? possibleRewards.Select(x => $"{x.Key.Id}:{x.Value}") : "no rewards"
				);
			}

			completions.Add(
				new Completion
				{
					Worker = vehicle.Driver,
					Place = place,
					Task = task,
					WorkSeconds = workSeconds,
					EarnedRewards = earnedRewards,
				}
			);
		}
		return completions;
	}

	public void PopulateVehicleMatrices(Vehicle vehicle)
	{
		// Initialize all metrics for this vehicle with blank matrices.
		vehicle.TimeMatrix.Clear();
		foreach (var factor in MetricWeights.Keys)
		{
			vehicle.MetricMatrices[factor] = Matrix<double>.Build.Dense(Size, Size, 0);
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
							var workTime = completions.Sum(c => c.WorkSeconds);
							matrix[a, b] += workTime;
							vehicle.TimeMatrix[a, b] += workTime;
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
			var start = Nodes.FindIndex(n => v.Driver.StartPlaceId == n.Place.Id);
			if (start == -1)
			{
				throw new ApplicationException(
					$"{nameof(v.Driver.StartPlaceId)} {v.Driver.StartPlaceId} not found for vehicle {v}"
				);
			}
			starts.Add(start);
			var end = Nodes.FindIndex(n => v.Driver.EndPlaceId == n.Place.Id);
			if (end == -1)
			{
				throw new ApplicationException(
					$"{nameof(v.Driver.EndPlaceId)} {v.Driver.EndPlaceId} not found for vehicle {v}"
				);
			}
			ends.Add(end);
		}
		return new RoutingIndexManager(Nodes.Count, Vehicles.Count, [.. starts], [.. ends]);
	}

	public static RoutingSearchParameters BuildSearchParameters(Problem problem)
	{
		RoutingSearchParameters searchParameters =
			operations_research_constraint_solver.DefaultRoutingSearchParameters();
		searchParameters.FirstSolutionStrategy = FirstSolutionStrategy.Types.Value.Automatic;
		searchParameters.LocalSearchMetaheuristic = LocalSearchMetaheuristic.Types.Value.Automatic;
		searchParameters.TimeLimit = new() { Seconds = problem.TimeoutSeconds };
		return searchParameters;
	}

	public RoutingModel BuildRoutingModel()
	{
		var routing = new RoutingModel(Manager);
		return routing;
	}

	public RoutingDimension BuildTimeDimension(Problem problem)
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

		var maxIdleSeconds = (long)Math.Round(problem.MaxIdleTime * problem.TimeFactor);
		Routing.AddDimensionWithVehicleTransits(
			[.. transitCallbackIndices],
			maxIdleSeconds, // max wait time at each node for arrival window to open
			long.MaxValue, // max total time per vehicle
			false, // don't force all vehicles to start at the same time
			DIMENSION_TIME
		);
		var timeDimension = Routing.GetDimensionOrDie(DIMENSION_TIME);

		// Set arrival time windows for all jobs
		foreach (var node in Nodes.Where(n => n.IsJob))
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
			var earliestStartTime = worker.EarliestStartTime ?? problem.TZero;
			if (earliestStartTime < problem.TZero)
			{
				earliestStartTime = problem.TZero;
			}
			var latestEndTime = worker.LatestEndTime ?? DateTimeOffset.MaxValue;
			if (latestEndTime <= earliestStartTime)
			{
				latestEndTime = DateTimeOffset.MaxValue;
			}
			var workWindow = Window.From(earliestStartTime, latestEndTime);
			var (workWindowOpenTime, workWindowCloseTime) = workWindow.RelativeTo(problem.TZero);

			var startIndex = Routing.Start(vehicle.Id);
			timeDimension.CumulVar(startIndex).SetRange(workWindowOpenTime, workWindowCloseTime);
			var endIndex = Routing.End(vehicle.Id);
			timeDimension.CumulVar(endIndex).SetRange(workWindowOpenTime, workWindowCloseTime);
		}

		return timeDimension;
	}

	public void ApplyVehicleVisitRules(Problem problem)
	{
		foreach (var node in Nodes)
		{
			if (!node.IsJob)
			{
				continue;
			}
			var nodeIndex = Manager.NodeToIndex(node.Id);

			// Assume all vehicles can visit to start with.
			var validVehicles = Vehicles.ToList();

			// Exclude vehicles where the driver lacks capability with any required tool.
			// TODO base this on required tasks not completion chances.
			var job = node.Place;
			var requiredTools = job
				.Tasks.Where(x => CompletionChances[x.Tool!] >= 1)
				.Select(x => x.Tool)
				.Distinct()
				.ToList();
			Log.Verbose("required tools for {job}: {requiredTools}", job, requiredTools);
			validVehicles = validVehicles
				.Except(Vehicles.Where(v => !requiredTools.All(t => v.ToolTimes[t!] > 0)))
				.ToList();

			// Apply any guarantees defined in the problem.
			var mustNotVisits = problem.Guarantees.Where(g => g.MustVisit == false && g.PlaceId == job.Id).ToList();
			foreach (var mustNotVisit in mustNotVisits)
			{
				var excludedVehicle = Vehicles.Where(v => v.Driver.Id == mustNotVisit.WorkerId).First();
				Log.Verbose("{excludedVehicle} must not visit {job}", excludedVehicle, job);
				validVehicles = validVehicles.Except([excludedVehicle]).ToList();
			}
			var mustVisit = problem.Guarantees.Where(g => g.MustVisit && g.PlaceId == job.Id).FirstOrDefault();
			if (null == mustVisit)
			{
				// Add disjunctions for any non-guaranteed nodes, allowing them to be skipped at a cost.
				var penalty = COST_FACTOR_SCALE * 2;
				Log.Debug("penalty for missing {job}: {penalty}", job, penalty);
				Routing.AddDisjunction([nodeIndex], penalty);
			}
			else
			{
				Log.Verbose("{workerId} must visit {placeId}", mustVisit.WorkerId, mustVisit.PlaceId);
				var requiredVehicle = Vehicles.Where(v => v.Driver.Id == mustVisit.WorkerId).First();
				Log.Verbose("requiredVehicleId: {requiredVehicleId}", requiredVehicle.Id);
				validVehicles = validVehicles.Where(v => v.Id == requiredVehicle.Id).ToList();
			}

			// There must be at least one valid vehicle remaining, or the problem is unsolvable.
			if (validVehicles.Count.Equals(0))
			{
				throw new ApplicationException($"No viable workers found for {job}");
			}
			var validVehicleIds = validVehicles.Select(v => v.Id).ToArray();
			Routing.SetAllowedVehiclesForIndex(validVehicleIds, nodeIndex);
			Log.Verbose("validVehicleIds for {job}: {validVehicleIds}", job, validVehicleIds);
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
		var fromTask = fromNode.Tasks.FirstOrDefault();
		var fromPlace = fromNode.Place;
		var toTask = toNode.Tasks.FirstOrDefault();
		var toPlace = toNode.Place;

		// Going to a place with no tasks is always valid.
		if (toTask is null)
		{
			return true;
		}

		// If going to the first task at a place, the worker must be coming from a different place.
		if (toTask.Order.Equals(0))
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
		solution.SkippedPlaces.AddRange(Nodes.Select(n => n.Place).Distinct());

		// Look for a solution!
		Log.Information("solving problem...");
		var assignment = Routing.SolveWithParameters(SearchParameters);

		// Exit early if no solution was found
		if (assignment is null)
		{
			Log.Warning("no solution found");
			return solution;
		}

		// Extract the routes for all vehicles from the solution and determine cost per vehicle.
		Log.Information("cheapest route found costs {totalCost}", assignment.ObjectiveValue());
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
			foreach (var (a, b, c, t) in transits)
			{
				// Make a new visit as the vehicle arrives at each new place.
				if (a.Tasks.Any(t => ArrivalTool.Equals(t.Tool)))
				{
					visit = new Visit
					{
						Place = a.Place,
						Worker = vehicle.Driver,
						ArrivalTime = TZero.AddSeconds(t),
					};
					solution.Visits.Add(visit);
					solution.SkippedPlaces.Remove(a.Place);
				}

				// We shouldn't be able to get here without a visit.
				ArgumentNullException.ThrowIfNull(visit);

				// Record all tasks completed at this node.
				var completions = vehicle.WorkMatrix[a.Id, b.Id];
				foreach (var completion in completions)
				{
					var task = completion.Task;
					if (!ArrivalTool.Equals(task.Tool))
					{
						visit.CompletedTasks.Add(task);
					}
					visit.WorkSeconds += completion.WorkSeconds;
					foreach (var (metric, reward) in completion.EarnedRewards)
					{
						visit.EarnedRewards.TryAdd(metric, 0);
						visit.EarnedRewards[metric] += reward;
					}
				}
			}

			// Add a visit for the last leg, with enough work time to make the departure time zero.
			var home = new Visit
			{
				Place = transits[^1].b.Place,
				Worker = vehicle.Driver,
				ArrivalTime = TZero.AddSeconds(routeEndTimes[vehicle]),
			};
			home.WorkSeconds = -home.ArrivalTime.ToUnixTimeSeconds();
			solution.Visits.Add(home);
			solution.SkippedPlaces.Remove(home.Place);
		}

		return solution;
	}

	public static void Render(Solution solution, bool pretty = false)
	{
		var settings = new JsonSerializerSettings
		{
			ContractResolver = new CamelCasePropertyNamesContractResolver(),
			Formatting = pretty ? Formatting.Indented : Formatting.None,
		};
		Console.WriteLine(JsonConvert.SerializeObject(solution.BuildResponse(), settings));
	}
}
