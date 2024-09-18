using MathNet.Numerics.LinearAlgebra;

namespace KSG.RoverTwo.Models;

public class Vehicle(int id, Worker driver, int matrixSize) : Entity(id)
{
	public Worker Driver { get; private init; } = driver;

	/// <summary>
	/// The actual values for each factor, which will be used to build the cost matrix.
	/// </summary>
	public Dictionary<CostFactor, Matrix<double>> CostFactorMatrices { get; private init; } = [];

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

	public override string ToString()
	{
		return $"{Driver.Id} <{Id}>";
	}
}
