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
	public Dictionary<string, Tool> ToolsById { get; init; } = [];
	public Dictionary<CostFactor, double> Weights { get; private init; } = [];

	/// <summary>
	/// Determines the decimal precision we carry over from the double matrices.
	/// </summary>
	private const long COST_FACTOR_SCALE = 1000000;

	public Solver(Request request)
	{
		request.Validate();

		TZero = request.TZero;
		PopulateNodes(request);
		PopulateWeights(request);
		PopulateTools(request);
		DistanceMatrix = BuildDistanceMatrix(request);
		TravelTimeMatrix = BuildTravelTimeMatrix(request);
		PopulateVehicles(request);

		SearchParameters = BuildSearchParameters(request);
		Manager = BuildRoutingManager(Nodes, Vehicles);
		Routing = BuildRoutingModel(Manager);
		TimeDimension = BuildTimeDimension(request, Manager, Routing, Nodes, Vehicles);
		ApplyCostFunction(Manager, Routing, Vehicles);
		ApplyVehicleVisitRules(request);
	}

	private void PopulateVehicles(Request request)
	{
		// Collect all the cost factor values and build a cost matrix for each vehicle
		foreach (var worker in request.Workers)
		{
			var vehicle = new Vehicle(Vehicles.Count, worker, Size);
			PopulateVehicleMatrices(vehicle);
			Vehicles.Add(vehicle);
		}

		// Find the maximum value for each cost factor across all vehicles
		var vehicleMaximumsByCostFactor = Weights.Keys.ToDictionary(
			cf => cf,
			cf => Vehicles.Select(v => v.CostFactorMatrices[cf].MaxValue()).Max()
		);

		// Finally build the cost matrix for each vehicle
		foreach (var vehicle in Vehicles)
		{
			PopulateVehicleCostMatrix(vehicle, vehicleMaximumsByCostFactor);
		}
	}

	private void PopulateTools(Request request)
	{
		foreach (var tool in request.Tools)
		{
			tool.NormalizeTime(request.TimeFactor);
			ToolsById[tool.Id] = tool;
		}
	}

	private void PopulateNodes(Request request)
	{
		Nodes.Clear();
		foreach (var (id, place) in request.Places.Enumerate())
		{
			Nodes.Add(new Node(id, place, place.ArrivalWindow.RelativeTo(TZero)));
		}
	}

	/// <summary>
	/// Normalizes the cost factor weights in the request so that their sum equals 1.
	/// </summary>
	/// <param name="request"></param>
	private void PopulateWeights(Request request)
	{
		double totalWeight = request.CostFactors.Sum(f => f.Weight);
		foreach (var costFactor in request.CostFactors)
		{
			Weights[costFactor] = costFactor.Weight / totalWeight;
		}
	}

	/// <summary>
	/// Distance is tracked in meters, so we need to potentially convert from what the problem has.
	/// </summary>
	/// <param name="request"></param>
	/// <param name="nodes"></param>
	/// <returns></returns>
	public Matrix<double> BuildDistanceMatrix(Request request)
	{
		if (Size.Equals(0))
		{
			throw new ApplicationException($"cannot build {nameof(DistanceMatrix)} of {nameof(Size)} 0");
		}
		var matrix = Matrix<double>.Build.Dense(Size, Size, 0);
		if (!request.IsDistanceMatrixRequired)
		{
			Log.Information("{attribute} not required", nameof(DistanceMatrix));
			return matrix;
		}
		Log.Information("building {attribute} from location coordinates", nameof(DistanceMatrix));
		if (!request.DoAllPlacesHaveLocations)
		{
			throw new ApplicationException($"cannot build {nameof(DistanceMatrix)} when not all places have locations");
		}
		foreach (var (a, fromNode) in Nodes.Enumerate())
		{
			foreach (var (b, toNode) in Nodes.Enumerate())
			{
				if (a == b)
				{
					// There is no distance between a place and the same place
					continue;
				}
				if (RoutingEngine.Internal.Equals(request.RoutingEngine))
				{
					matrix[a, b] = fromNode.Location!.ManhattanDistanceTo(toNode.Location!);
				}
				else
				{
					throw new NotImplementedException(nameof(request.RoutingEngine));
				}
				Log.Verbose(
					"distance from {fromNode} ({fromLocation}) to {toNode} ({toLocation}) is {distance} {distanceUnit}s",
					fromNode.Place.Name,
					fromNode.Location,
					toNode.Place.Name,
					toNode.Location,
					matrix[a, b],
					request.DistanceUnit
				);
			}
		}
		return matrix.Multiply(request.DistanceFactor);
	}

	/// <summary>
	/// Time is expected in seconds, so we need to potentially convert from what the problem has.
	/// </summary>
	/// <param name="request"></param>
	/// <returns>values in seconds</returns>
	public Matrix<double> BuildTravelTimeMatrix(Request request)
	{
		if (Size.Equals(0))
		{
			throw new ApplicationException($"cannot build {nameof(TravelTimeMatrix)} of {nameof(Size)} 0");
		}
		var matrix = Matrix<double>.Build.Dense(Size, Size, 0);
		if (!request.IsTravelTimeMatrixRequired)
		{
			Log.Information("{attribute} matrix not required", nameof(TravelTimeMatrix));
			return matrix;
		}
		Log.Information(
			"building {attribute} from {source} at {distanceFactor} meters per {timeUnit}",
			nameof(TravelTimeMatrix),
			nameof(DistanceMatrix),
			request.DistanceFactor,
			request.DistanceUnit,
			request.TimeUnit
		);
		foreach (var (a, fromNode) in Nodes.Enumerate())
		{
			foreach (var (b, toNode) in Nodes.Enumerate())
			{
				if (a == b)
				{
					// There is no travel time between a place and the same place
					continue;
				}
				var meters = DistanceMatrix[a, b];
				var distanceUnits = meters / request.DistanceFactor;
				matrix[a, b] = distanceUnits / request.DefaultTravelSpeed;
				Log.Verbose(
					"Travel time from {a} to {b} is {travelTime} {timeUnit}s to cover {meters} meters ({distanceUnits} {distanceUnit}s)",
					fromNode.Place.Name,
					toNode.Place.Name,
					matrix[a, b],
					request.TimeUnit,
					meters,
					distanceUnits,
					request.DistanceUnit
				);
			}
		}
		return matrix.Multiply(request.TimeFactor);
	}

	public void PopulateVehicleMatrices(Vehicle vehicle)
	{
		// Initialize all cost factors for this vehicle with blank matrices
		vehicle.TimeMatrix.Clear();
		foreach (var factor in Weights.Keys)
		{
			vehicle.CostFactorMatrices[factor] = Matrix<double>.Build.Dense(Size, Size, 0);
		}

		// Traverse the network, recording rewards and time spent
		foreach (var (a, fromNode) in Nodes.Enumerate())
		{
			foreach (var (b, toNode) in Nodes.Enumerate(a))
			{
				// Simulate a visit at the starting node
				var visit = new Visit { Place = fromNode.Place, Worker = vehicle.Driver };
				visit.SimulateWork(ToolsById);
				vehicle.VisitMatrix[a, b] = visit;

				foreach (var costFactor in Weights.Keys)
				{
					// Get a reference to the matrix we'll be populating
					var matrix = vehicle.CostFactorMatrices[costFactor];

					// Source the value for this transit based on the metric
					if (Metric.Distance.Equals(costFactor.Metric))
					{
						matrix[a, b] += DistanceMatrix[a, b];
					}
					else if (Metric.WorkTime.Equals(costFactor.Metric))
					{
						matrix[a, b] += visit.WorkSeconds;
						vehicle.TimeMatrix[a, b] += visit.WorkSeconds;
					}
					else if (Metric.TravelTime.Equals(costFactor.Metric))
					{
						var travelTime = (long)Math.Round(TravelTimeMatrix[a, b] / vehicle.Driver.TravelSpeedFactor);
						matrix[a, b] += travelTime;
						vehicle.TimeMatrix[a, b] += travelTime;
					}
					else if (Metric.Custom.Equals(costFactor.Metric))
					{
						// Get any applicable rewards for completed work at A
						if (visit.Rewards.TryGetValue(costFactor.Id, out var reward))
						{
							matrix[a, b] += reward;
						}

						// Get any applicable custom metrics for B
						if (vehicle.Driver.VisitCosts.TryGetValue(costFactor.Id, out var values))
						{
							if (values.TryGetValue(toNode.Place.Id, out var value))
							{
								matrix[a, b] += value;
							}
						}
					}
					else
					{
						throw new NotImplementedException($"Unknown cost factor {costFactor}");
					}
				}
			}
		}
	}

	public void PopulateVehicleCostMatrix(Vehicle vehicle, Dictionary<CostFactor, double> vehicleMaximumsByCostFactor)
	{
		var costMatrix = Matrix<double>.Build.Dense(Size, Size, 0);
		foreach (var (costFactor, vehicleMaximum) in vehicleMaximumsByCostFactor.Where(x => x.Value > 0))
		{
			// Normalize the raw matrix to a scale of 1 for easier weighting
			var rawMatrix = vehicle.CostFactorMatrices[costFactor];
			var normalizedMatrix = rawMatrix.Multiply(1 / vehicleMaximum);

			// If this is a negative cost factor, the cost must represent missing out on rewards
			if (costFactor.IsBenefit)
			{
				// At this point the maximum value is 1, so we can subtract 1 from the matrix to invert it
				normalizedMatrix = normalizedMatrix.Subtract(1).PointwiseAbs();
			}

			// Weight this slice of the cost pie and add it to any existing costs
			costMatrix += normalizedMatrix.Multiply(Weights[costFactor]);
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

	public static RoutingSearchParameters BuildSearchParameters(Request request)
	{
		RoutingSearchParameters searchParameters =
			operations_research_constraint_solver.DefaultRoutingSearchParameters();
		searchParameters.FirstSolutionStrategy = FirstSolutionStrategy.Types.Value.PathCheapestArc;
		searchParameters.TimeLimit = new() { Seconds = request.TimeoutSeconds };
		return searchParameters;
	}

	public static RoutingModel BuildRoutingModel(RoutingIndexManager manager)
	{
		var routing = new RoutingModel(manager);
		return routing;
	}

	public static RoutingDimension BuildTimeDimension(
		Request request,
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

		routing.AddDimensionWithVehicleTransits(
			transitCallbackIndices,
			request.MaxIdleSeconds, // Max wait time at each node for arrival window to open
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
			var earliestStartTime = worker.EarliestStartTime ?? request.TZero;
			if (earliestStartTime < request.TZero)
			{
				earliestStartTime = request.TZero;
			}
			var latestEndTime = worker.LatestEndTime ?? DateTimeOffset.MaxValue;
			if (latestEndTime <= earliestStartTime)
			{
				latestEndTime = DateTimeOffset.MaxValue;
			}
			var workWindow = Window.From(earliestStartTime, latestEndTime);
			var (workWindowOpenTime, workWindowCloseTime) = workWindow.RelativeTo(request.TZero);

			var startIndex = routing.Start(vehicle.Id);
			timeDimension.CumulVar(startIndex).SetRange(workWindowOpenTime, workWindowCloseTime);
			var endIndex = routing.End(vehicle.Id);
			timeDimension.CumulVar(endIndex).SetRange(workWindowOpenTime, workWindowCloseTime);
		}

		return timeDimension;
	}

	public void ApplyVehicleVisitRules(Request request)
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

			// Exclude vehicles where the driver lacks capability with and required tool
			var job = node.Place;
			var requiredToolIds = job
				.Tasks.Where(x => ToolsById[x.ToolId].CompletionRate >= 1)
				.Select(x => x.ToolId)
				.ToList();
			Log.Verbose("requiredToolIds for {job}: {requiredToolIds}", job, requiredToolIds);
			validVehicles = validVehicles
				.Except(Vehicles.Where(v => !requiredToolIds.All(t => v.Driver.TimeToUse(ToolsById[t]) > 0)))
				.ToList();

			// Apply any guarantees from the request
			var mustNotVisits = request.Guarantees.Where(g => g.MustVisit == false && g.PlaceId == job.Id).ToList();
			foreach (var mustNotVisit in mustNotVisits)
			{
				var excludedVehicle = Vehicles.Where(v => v.Driver.Id == mustNotVisit.WorkerId).First();
				Log.Verbose("{excludedVehicle} must not visit {job}", excludedVehicle, job);
				validVehicles = validVehicles.Except([excludedVehicle]).ToList();
			}
			var mustVisit = request.Guarantees.Where(g => g.MustVisit && g.PlaceId == job.Id).FirstOrDefault();
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

	public Response Solve()
	{
		var response = new Response();
		response.Skipped.AddRange(Nodes.Select(n => n.Place.Id));

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

		// Build the visit list
		foreach (var (vehicle, transits) in transitsByVehicle)
		{
			var visitCount = 0;
			response.CostPerVisit[vehicle.Driver.Id] = 0;
			foreach (var (a, b, c, t) in transits)
			{
				var visit = vehicle.VisitMatrix[a.Id, b.Id];
				visit.ArrivalTime = TZero.AddSeconds(t);
				response.CostPerVisit[vehicle.Driver.Id] += c;
				response.Visits.Add(visit);
				response.Skipped.Remove(visit.PlaceId);
				visitCount++;
			}
			response.CostPerVisit[vehicle.Driver.Id] /= visitCount;

			// Add a visit for the last leg, with enough work time to make the departure time zero
			var home = new Visit
			{
				Place = transits[^1].Item2.Place,
				Worker = vehicle.Driver,
				ArrivalTime = TZero.AddSeconds(routeEndTimes[vehicle]),
			};
			home.WorkSeconds = -home.ArrivalTime.ToUnixTimeSeconds();
			response.Visits.Add(home);
			response.Skipped.Remove(home.PlaceId);
		}

		// Calculate the claimed reward percentages for each cost factor
		var benefitFactors = Weights.Keys.Where(cfm => cfm.IsBenefit);
		foreach (var bf in benefitFactors)
		{
			var beneficialTasks = Nodes.SelectMany(n => n.Place.Tasks).Where(t => t.Rewards.ContainsKey(bf.Id));
			var availableReward = beneficialTasks.Sum(t => t.Rewards[bf.Id]);
			var claimedReward = response.Visits.Sum(v => v.Rewards.GetValueOrDefault(bf.Id));
			if (availableReward > 0)
			{
				response.RewardRate[bf.Id] = claimedReward / availableReward;
			}
		}

		return response;
	}

	public static void Render(Response response, bool pretty = false)
	{
		var settings = new JsonSerializerSettings
		{
			ContractResolver = new CamelCasePropertyNamesContractResolver(),
			Formatting = pretty ? Formatting.Indented : Formatting.None,
		};
		Console.WriteLine(JsonConvert.SerializeObject(response, settings));
	}
}
