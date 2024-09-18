using MathNet.Numerics.LinearAlgebra;

namespace KSG.RoverTwo.Models;

/// <summary>
/// Used to compose cost matrices.
/// </summary>
public class Layer()
{
	public required CostFactor CostFactor { get; init; }

	public required long[,] Matrix { get; init; }
	public Worker? Worker { get; init; }

	public override string ToString()
	{
		return $"{CostFactor} {Worker}";
	}
}
