using KSG.RoverTwo.Extensions;
using MathNet.Numerics.LinearAlgebra;

namespace KSG.RoverTwo.Tests;

public class ExtensionTests
{
	[Theory]
	[InlineData(0, 3, 1, 2, 3)]
	public void MaxValue_DoubleMatrix_CalculatesExpectedValue(double v1, double v2, double v3, double v4, double max)
	{
		var matrix = Matrix<double>.Build.DenseOfArray(
			new double[,]
			{
				{ v1, v2 },
				{ v3, v4 },
			}
		);
		Assert.Equal(max, matrix.MaxValue());
	}
}
