using Google.OrTools.ConstraintSolver;
using Google.Protobuf.WellKnownTypes;
using KSG.RoverTwo.Enums;
using KSG.RoverTwo.Extensions;
using KSG.RoverTwo.Models;
using MathNet.Numerics.LinearAlgebra;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Serilog;

namespace KSG.RoverTwo;

public class Solver
{
	public DateTimeOffset TZero { get; private init; }
	private const string DIMENSION_TIME = "SecondsSinceTZero";

	/// <summary>
	/// Meters between nodes.
	/// </summary>
	public Matrix<double> DistanceMatrix { get; private init; }

	/// <summary>
	/// Seconds between nodes.
	/// </summary>
	public Matrix<double> TravelTimeMatrix { get; private init; }
	private RoutingSearchParameters SearchParameters { get; init; }
	private RoutingDimension TimeDimension { get; init; }
	public RoutingIndexManager Manager { get; private init; }
	public RoutingModel Routing { get; private init; }
	public List<Node> Nodes { get; private init; } = [];
	public int Size
	{
		get => Nodes.Count;
	}
	public List<Vehicle> Vehicles { get; private init; } = [];
	public Dictionary<Tool, double> CompletionRates { get; init; } = [];
	public Dictionary<Metric, double> MetricWeights { get; private init; } = [];

	/// <summary>
	/// Determines the decimal precision we carry over from the double matrices.
	/// </summary>
	private const long COST_FACTOR_SCALE = 1000000;

	public Solver(Problem problem)
	{
		problem.Validate();

		TZero = problem.TZero;
		PopulateNodes(problem);
		PopulateMetricWeights(problem);
		PopulateCompletionRates(problem);
		DistanceMatrix = BuildDistanceMatrix(problem);
		TravelTimeMatrix = BuildTravelTimeMatrix(problem);
		PopulateVehicles(problem);

		SearchParameters = BuildSearchParameters(problem);
		Manager = BuildRoutingManager(Nodes, Vehicles);
		Routing = BuildRoutingModel(Manager);
		TimeDimension = BuildTimeDimension(problem, Manager, Routing, Nodes, Vehicles);
		ApplyCostFunction(Manager, Routing, Vehicles);
		ApplyVehicleVisitRules(problem);
	}

	private void PopulateVehicles(Problem problem)
	{
		if (CompletionRates.Count.Equals(0))
		{
			throw new InvalidOperationException(
				$"call {nameof(PopulateCompletionRates)} before {nameof(PopulateVehicles)}"
			);
		}

		// Collect all the metric values and build a cost matrix for each vehicle
		foreach (var worker in problem.Workers)
		{
			var vehicle = new Vehicle(Vehicles.Count, worker, Size);
			PopulateToolTimes(vehicle, problem.TimeFactor);
			PopulateVehicleMatrices(vehicle);
			Vehicles.Add(vehicle);
		}

		// Find the maximum value for each metric across all vehicles
		var vehicleMaximumsByMetric = MetricWeights.Keys.ToDictionary(
			cf => cf,
			cf => Vehicles.Select(v => v.MetricMatrices[cf].MaxValue()).Max()
		);

		// Finally build the cost matrix for each vehicle
		foreach (var vehicle in Vehicles)
		{
			PopulateVehicleCostMatrix(vehicle, vehicleMaximumsByMetric);
		}
	}

	private void PopulateToolTimes(Vehicle vehicle, double timeFactor)
	{
		foreach (var tool in CompletionRates.Keys)
		{
			long toolTime = 0;
			vehicle.Driver.CapabilitiesByTool.TryGetValue(tool, out Capability? capability);
			if (capability is not null)
			{
				toolTime = (long)Math.Round(tool.Delay * capability.DelayFactor * timeFactor);
			}
			// tool.Delay * capability.DelayFactor;
			vehicle.ToolTimes[tool] = toolTime;
		}
	}

	private void PopulateCompletionRates(Problem problem)
	{
		foreach (var tool in problem.Tools)
		{
			CompletionRates[tool] = tool.CompletionRate;
		}
	}

	private void PopulateNodes(Problem problem)
	{
		Nodes.Clear();
		foreach (var (id, place) in problem.Places.Enumerate())
		{
			Nodes.Add(new Node(id, place, place.ArrivalWindow.RelativeTo(TZero)));
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

	public void PopulateVehicleMatrices(Vehicle vehicle)
	{
		// Initialize all metrics for this vehicle with blank matrices
		vehicle.TimeMatrix.Clear();
		foreach (var factor in MetricWeights.Keys)
		{
			vehicle.MetricMatrices[factor] = Matrix<double>.Build.Dense(Size, Size, 0);
		}

		// Traverse the network, recording rewards and time spent
		foreach (var (a, fromNode) in Nodes.Enumerate())
		{
			foreach (var (b, toNode) in Nodes.Enumerate(a))
			{
				// Simulate a visit at the starting node
				var visit = new Visit { Place = fromNode.Place, Worker = vehicle.Driver };
				visit.SimulateWork(vehicle);
				vehicle.VisitMatrix[a, b] = visit;

				foreach (var metric in MetricWeights.Keys)
				{
					// Get a reference to the matrix we'll be populating
					var matrix = vehicle.MetricMatrices[metric];

					// Source the value for this transit based on the metric
					if (MetricType.Distance.Equals(metric.Type))
					{
						matrix[a, b] += DistanceMatrix[a, b];
					}
					else if (MetricType.WorkTime.Equals(metric.Type))
					{
						matrix[a, b] += visit.WorkSeconds;
						vehicle.TimeMatrix[a, b] += visit.WorkSeconds;
					}
					else if (MetricType.TravelTime.Equals(metric.Type))
					{
						var travelTime = (long)Math.Round(TravelTimeMatrix[a, b] / vehicle.Driver.TravelSpeedFactor);
						matrix[a, b] += travelTime;
						vehicle.TimeMatrix[a, b] += travelTime;
					}
					else if (MetricType.Custom.Equals(metric.Type))
					{
						// Get any applicable rewards for completed work at A
						if (visit.EarnedRewards.TryGetValue(metric, out var reward))
						{
							matrix[a, b] += reward;
						}

						// Get any applicable custom metrics for B
						if (vehicle.Driver.VisitCostsByMetric.TryGetValue(metric, out var values))
						{
							if (values.TryGetValue(toNode.Place, out var value))
							{
								matrix[a, b] += value;
							}
						}
					}
					else
					{
						throw new NotImplementedException($"Unknown metric {metric}");
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
			// Normalize the raw matrix to a scale of 1 for easier weighting
			var rawMatrix = vehicle.MetricMatrices[metric];
			var normalizedMatrix = rawMatrix.Multiply(1 / vehicleMaximum);

			// If this is being maximized, the "cost" represents missing out on rewards
			if (MetricMode.Maximize.Equals(metric.Mode))
			{
				// At this point the maximum value is 1, so we can subtract 1 from the matrix to invert it
				normalizedMatrix = normalizedMatrix.Subtract(1).PointwiseAbs();
			}

			// Weight this slice of the cost pie and add it to any existing costs
			costMatrix += normalizedMatrix.Multiply(MetricWeights[metric]);
		}

		// Now we need to scale and longify the costs before the matrix can be used in a transit callback
		vehicle.CostMatrix.Clear();
		foreach (var (a, fromNode) in Nodes.Enumerate())
		{
			foreach (var (b, toNode) in Nodes.Enumerate(a))
			{
				vehicle.CostMatrix[a, b] = (long)Math.Round(costMatrix[a, b] * COST_FACTOR_SCALE);
			}
		}
	}

	public static RoutingIndexManager BuildRoutingManager(List<Node> nodes, List<Vehicle> vehicles)
	{
		if (nodes.Count < 1)
		{
			throw new ApplicationException($"{nameof(nodes)} cannot be empty");
		}
		if (vehicles.Count < 1)
		{
			throw new ApplicationException($"{nameof(vehicles)} cannot be empty");
		}
		var starts = new List<int>();
		var ends = new List<int>();
		foreach (var v in vehicles)
		{
			var start = nodes.FindIndex(n => v.Driver.StartPlaceId == n.Place.Id);
			if (start == -1)
			{
				throw new ApplicationException(
					$"{nameof(v.Driver.StartPlaceId)} {v.Driver.StartPlaceId} not found for vehicle {v}"
				);
			}
			starts.Add(start);
			var end = nodes.FindIndex(n => v.Driver.EndPlaceId == n.Place.Id);
			if (end == -1)
			{
				throw new ApplicationException(
					$"{nameof(v.Driver.EndPlaceId)} {v.Driver.EndPlaceId} not found for vehicle {v}"
				);
			}
			ends.Add(end);
		}
		return new RoutingIndexManager(nodes.Count, vehicles.Count, [.. starts], [.. ends]);
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

	public static RoutingModel BuildRoutingModel(RoutingIndexManager manager)
	{
		var routing = new RoutingModel(manager);
		return routing;
	}

	public static RoutingDimension BuildTimeDimension(
		Problem problem,
		RoutingIndexManager manager,
		RoutingModel routing,
		List<Node> nodes,
		List<Vehicle> vehicles
	)
	{
		var transitCallbackIndices = new int[vehicles.Count];
		foreach (var (vehicleIndex, vehicle) in vehicles.Enumerate())
		{
			var timeTransitCallbackIndex = routing.RegisterTransitCallback(
				(fromIndex, toIndex) =>
				{
					var a = manager.IndexToNode(fromIndex);
					var b = manager.IndexToNode(toIndex);
					return vehicle.TimeMatrix[a, b];
				}
			);
			transitCallbackIndices[vehicleIndex] = timeTransitCallbackIndex;
		}

		var maxIdleSeconds = (long)Math.Round(problem.MaxIdleTime * problem.TimeFactor);
		routing.AddDimensionWithVehicleTransits(
			transitCallbackIndices,
			maxIdleSeconds, // Max wait time at each node for arrival window to open
			long.MaxValue, // Max total time per vehicle
			false, // Don't force all vehicles to start at the same time
			DIMENSION_TIME
		);
		var timeDimension = routing.GetDimensionOrDie(DIMENSION_TIME);

		// Set arrival time windows for all jobs
		foreach (var node in nodes.Where(n => n.IsJob))
		{
			var index = manager.NodeToIndex(node.Id);
			var (open, close) = node.TimeWindow;
			if (open > 0 && close > 0 && open < close)
			{
				timeDimension.CumulVar(index).SetRange(open, close);
			}
		}

		// Set arrival time windows for hubs
		foreach (var vehicle in vehicles)
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

			var startIndex = routing.Start(vehicle.Id);
			timeDimension.CumulVar(startIndex).SetRange(workWindowOpenTime, workWindowCloseTime);
			var endIndex = routing.End(vehicle.Id);
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

			// Assume all vehicles can visit to start with
			var validVehicles = Vehicles.ToList();

			// Exclude vehicles where the driver lacks capability with any required tool
			var job = node.Place;
			var requiredTools = job
				.Tasks.Where(x => CompletionRates[x.Tool!] >= 1)
				.Select(x => x.Tool)
				.Distinct()
				.ToList();
			Log.Verbose("required tools for {job}: {requiredTools}", job, requiredTools);
			validVehicles = validVehicles
				.Except(Vehicles.Where(v => !requiredTools.All(t => v.ToolTimes[t!] > 0)))
				.ToList();

			// Apply any guarantees defined in the problem
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
				// Add disjunctions for any non-guaranteed nodes, allowing them to be skipped at a cost
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

			// There must be at least one valid vehicle remaining, or the problem is unsolvable
			if (0 == validVehicles.Count)
			{
				throw new ApplicationException($"No viable workers found for {job}");
			}
			var validVehicleIds = validVehicles.Select(v => v.Id).ToArray();
			Routing.SetAllowedVehiclesForIndex(validVehicleIds, nodeIndex);
			Log.Verbose("validVehicleIds for {job}: {validVehicleIds}", job, validVehicleIds);
		}
	}

	public static void ApplyCostFunction(RoutingIndexManager manager, RoutingModel routing, List<Vehicle> vehicles)
	{
		foreach (var vehicle in vehicles)
		{
			var transitCallbackIndex = routing.RegisterTransitCallback(
				(fromIndex, toIndex) =>
				{
					var a = manager.IndexToNode(fromIndex);
					var b = manager.IndexToNode(toIndex);
					return vehicle.CostMatrix[a, b];
				}
			);
			Log.Verbose(
				"transitCallbackIndex for {vehicle}: {transitCallbackIndex}",
				vehicle,
				vehicle.Id,
				transitCallbackIndex
			);
			routing.SetArcCostEvaluatorOfVehicle(transitCallbackIndex, vehicle.Id);
		}
	}

	public Solution Solve()
	{
		var response = new Solution();
		response.SkippedPlaces.AddRange(Nodes.Select(n => n.Place));

		// Look for a solution!
		Log.Information("solving problem...");
		var solution = Routing.SolveWithParameters(SearchParameters);

		// Exit early if no solution was found
		if (solution is null)
		{
			Log.Warning("no solution found");
			return response;
		}

		// Extract the routes for all vehicles from the solution and determine cost per vehicle
		Log.Information("cheapest route found costs {totalCost}", solution.ObjectiveValue());
		var transitsByVehicle = new Dictionary<Vehicle, List<(Node, Node, long, long)>>();
		var routeEndTimes = new Dictionary<Vehicle, long>();
		foreach (var vehicle in Vehicles)
		{
			transitsByVehicle.Add(vehicle, []);
			var index = Routing.Start(vehicle.Id);
			while (!Routing.IsEnd(index))
			{
				var arrivalTime = solution.GetDimensionValueAt(index, TimeDimension);
				var node = Nodes[Manager.IndexToNode(index)];
				var nextIndex = solution.Value(Routing.NextVar(index));
				var nextNode = Nodes[Manager.IndexToNode(nextIndex)];
				var transitCost = Routing.GetArcCostForVehicle(index, nextIndex, vehicle.Id);
				transitsByVehicle[vehicle].Add((node, nextNode, transitCost, arrivalTime));
				index = nextIndex;
			}
			routeEndTimes[vehicle] = solution.GetDimensionValueAt(index, TimeDimension);
		}

		// Find the total rewards, so we can subtract the claimed ones and be left with the missed ones
		foreach (var node in Nodes)
		{
			foreach (var kvp in node.AvailableRewards)
			{
				var metric = kvp.Key;
				var reward = kvp.Value;
				response.MissedRewards.TryAdd(metric, 0);
				response.MissedRewards[metric] += reward;
			}
		}

		// Build the visit list
		foreach (var (vehicle, transits) in transitsByVehicle)
		{
			var visitCount = 0;
			response.CostPerVisit[vehicle.Driver] = 0;
			foreach (var (a, b, c, t) in transits)
			{
				var visit = vehicle.VisitMatrix[a.Id, b.Id];
				visit.ArrivalTime = TZero.AddSeconds(t);
				response.CostPerVisit[vehicle.Driver] += c;
				response.Visits.Add(visit);
				response.SkippedPlaces.Remove(visit.Place);
				visitCount++;
			}
			response.CostPerVisit[vehicle.Driver] /= visitCount;

			// Add a visit for the last leg, with enough work time to make the departure time zero
			var home = new Visit
			{
				Place = transits[^1].Item2.Place,
				Worker = vehicle.Driver,
				ArrivalTime = TZero.AddSeconds(routeEndTimes[vehicle]),
			};
			home.WorkSeconds = -home.ArrivalTime.ToUnixTimeSeconds();
			response.Visits.Add(home);
			response.SkippedPlaces.Remove(home.Place);
		}

		// Calculate the missed rewards
		foreach (var v in response.Visits)
		{
			foreach (var kvp in v.EarnedRewards)
			{
				var metric = kvp.Key;
				var reward = kvp.Value;
				response.MissedRewards[metric] -= reward;
			}
		}

		return response;
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
