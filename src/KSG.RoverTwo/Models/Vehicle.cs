using MathNet.Numerics.LinearAlgebra;

namespace KSG.RoverTwo.Models;

public class Vehicle(int id, Worker driver, int matrixSize) : Entity(id)
{
	public Worker Driver { get; private init; } = driver;

	/// <summary>
	/// How many seconds it takes for the Driver to use each tool.
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
	/// Assumptions about what work will be done for which rewards when we visit each place.
	/// </summary>
	public Visit[,] VisitMatrix { get; private init; } = new Visit[matrixSize, matrixSize];

	/// <summary>
	/// Reward factors for each metric and tool.
	/// </summary>
	public Dictionary<Metric, Dictionary<Tool, double>> RewardFactors { get; private init; } = [];

	public double RewardFactor(Tool tool, Metric metric)
	{
		if (RewardFactors.TryGetValue(metric, out var factors))
		{
			return factors.GetValueOrDefault(tool, 1);
		}
		return 1;
	}

	public override string ToString()
	{
		return $"{Driver.Id} <{Id}>";
	}
}
