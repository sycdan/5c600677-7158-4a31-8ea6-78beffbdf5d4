using Google.OrTools.ConstraintSolver;
using Serilog;
using Sycdan.RoverTwo.Models;

namespace Sycdan.RoverTwo;

public class Solver
{
	public RoutingIndexManager Manager { get; init; }
	public RoutingModel Routing { get; init; }
	public Dictionary<int, Worker> Vehicles { get; init; }
	public Dictionary<int, Place> Nodes { get; init; }

	// public required List<Worker> Workers { get; set; }
	// public required List<Job> Jobs { get; set; }
	// public required List<Hub> Hubs { get; set; }
	// public required List<Tool> Tools { get; set; }

	public Solver(Request request)
	{
		// Index the hubs and jobs
		Nodes = new();
		foreach (var hub in request.Hubs)
		{
			Nodes[Nodes.Count] = hub;
		}
		foreach (var job in request.Jobs)
		{
			Nodes[Nodes.Count] = job;
		}

		// Index the vehicles
		Vehicles = new();
		foreach (var worker in request.Workers)
		{
			Vehicles[Vehicles.Count] = worker;
		}

		// Build the routing manager & model
		Manager = new RoutingIndexManager(
			Nodes.Count,
			Vehicles.Count,
			Vehicles.Values.Select(w => Nodes.Values.ToList().FindIndex(n => w.HubId == n.Id)).ToArray(),
			Vehicles.Values.Select(w => Nodes.Values.ToList().FindIndex(n => w.HubId == n.Id)).ToArray()
		);
		Routing = new RoutingModel(Manager);

		// TODO: Use T0 as a reference point to covert arrival windows to simple integers
	}

	public void Solve()
	{
		foreach (var (vehicleIndex, worker) in Vehicles)
		{
			var transitCallbackIndex = Routing.RegisterTransitCallback(
				(fromIndex, toIndex) =>
				{
					var fromNode = Nodes[Manager.IndexToNode(fromIndex)];
					var toNode = Nodes[Manager.IndexToNode(toIndex)];

					// Log.Debug("{worker} [{vehicleIndex}] from {fromNode} [{fromIndex}] to {toNode} [{toIndex}]", worker, vehicleIndex, fromNode, fromIndex, toNode, toIndex);

					return (long)fromNode.Location.StraightDistanceTo(toNode.Location);
				}
			);
			Routing.SetArcCostEvaluatorOfVehicle(transitCallbackIndex, vehicleIndex);
		}

		// TODO: routing.SetAllowedVehiclesForIndex for blacklists

		RoutingSearchParameters searchParameters =
			operations_research_constraint_solver.DefaultRoutingSearchParameters();
		searchParameters.FirstSolutionStrategy = FirstSolutionStrategy.Types.Value.PathCheapestArc;

		Assignment solution = Routing.SolveWithParameters(searchParameters);

		Console.WriteLine($"Objective {solution.ObjectiveValue()}:");

		// Inspect solution.
		long maxRouteCost = 0;
		foreach (var (vehicleIndex, worker) in Vehicles)
		{
			Console.WriteLine($"Route for {worker} [{vehicleIndex}]:");
			long routeCost = 0;
			var index = Routing.Start(vehicleIndex);
			while (Routing.IsEnd(index) == false)
			{
				Console.Write("{0} -> ", Nodes[Manager.IndexToNode((int)index)]);
				var previousIndex = index;
				index = solution.Value(Routing.NextVar(index));
				routeCost += Routing.GetArcCostForVehicle(previousIndex, index, vehicleIndex);
			}
			Console.WriteLine("{0}", Nodes[Manager.IndexToNode((int)index)]);
			Console.WriteLine("Distance of the route: {0}m", routeCost);
			maxRouteCost = Math.Max(routeCost, maxRouteCost);
		}
		Console.WriteLine("Maximum distance of the routes: {0}m", maxRouteCost);
	}

	public void Render(int frameDelay)
	{
		Solve();
		// Console.Clear();
	}
}
