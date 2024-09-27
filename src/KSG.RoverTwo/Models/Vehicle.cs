using MathNet.Numerics.LinearAlgebra;

namespace KSG.RoverTwo.Models;

public class Vehicle(int id, Worker driver, int matrixSize) : Entity(id)
{
	public Worker Driver { get; private init; } = driver;

	/// <summary>
	/// How many seconds it takes for the driver to use each tool, on average.
	/// Only tools in the driver's capabilities can be used to complete tasks.
	/// </summary>
	public Dictionary<Tool, long> ToolTimes { get; init; } = [];

	/// <summary>
	/// The actual values for each factor, which will be used to build the cost matrix.
	/// </summary>
	public Dictionary<Metric, Matrix<double>> MetricMatrices { get; private init; } = [];

	/// <summary>
	/// Actual time spent on each transit.
	/// </summary>
	public long[,] TimeMatrix { get; private init; } = new long[matrixSize, matrixSize];

	/// <summary>
	/// Combined cost of each transit.
	/// </summary>
	public long[,] CostMatrix { get; private init; } = new long[matrixSize, matrixSize];

	/// <summary>
	/// Assumptions about which tasks will be performed at which nodes.
	/// </summary>
	public List<Completion>[,] WorkMatrix { get; private init; } = new List<Completion>[matrixSize, matrixSize];

	public override string ToString()
	{
		return $"{nameof(Vehicle)}{Id}:{Driver}";
	}
}
