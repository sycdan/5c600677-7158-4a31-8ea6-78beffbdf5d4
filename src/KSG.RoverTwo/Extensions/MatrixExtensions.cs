using MathNet.Numerics.LinearAlgebra;
using Serilog;

namespace KSG.RoverTwo.Extensions;

public static class MatrixExtensions
{
	public static IEnumerable<(int Index, T Item)> Enumerate<T>(this IEnumerable<T> source, params int[] except)
	{
		int index = 0;
		foreach (var item in source)
		{
			if (!except.Contains(index))
			{
				yield return (index, item);
			}
			index++;
		}
	}

	public static double[,] ToArray2D(this double[][] jaggedArray)
	{
		int rows = jaggedArray.Length;
		int cols = jaggedArray[0].Length;
		var twoDArray = new double[rows, cols];

		for (int i = 0; i < rows; i++)
		{
			for (int j = 0; j < cols; j++)
			{
				twoDArray[i, j] = jaggedArray[i][j];
			}
		}

		return twoDArray;
	}

	public static long[,] AsLongArray(this Matrix<double> self, double factor = 1)
	{
		int rows = self.RowCount;
		int cols = self.ColumnCount;
		var matrix = new long[rows, cols];

		for (int i = 0; i < rows; i++)
		{
			for (int j = 0; j < cols; j++)
			{
				matrix[i, j] = (long)Math.Round(self[i, j] * factor);
			}
		}

		return matrix;
	}

	public static long[,] Clear(this long[,] self)
	{
		Array.Clear(self, 0, self.Length);
		return self;
	}

	public static double[,] ToArray2D(this List<double> list)
	{
		int items = list.Count;
		var twoDArray = new double[1, items];

		for (int i = 0; i < items; i++)
		{
			twoDArray[0, i] = list[i];
		}

		return twoDArray;
	}

	/// <summary>
	/// Applies a factor to every value in the matrix.
	/// </summary>
	/// <param name="matrix"></param>
	/// <param name="factor"></param>
	/// <returns></returns>
	public static long[,] Factor(this double[,] matrix, double factor = 1)
	{
		var size1 = matrix.GetLength(0);
		var size2 = matrix.GetLength(1);
		var newMatrix = new long[size1, size2];

		for (int i = 0; i < size1; i++)
		{
			for (int j = 0; j < size2; j++)
			{
				var x = matrix[i, j];
				newMatrix[i, j] = (long)Math.Round(x * factor);
			}
		}

		return newMatrix;
	}

	public static List<double> Normalize(this List<double> values, long scale = 1)
	{
		if (null == values || 0 == values.Count)
		{
			Log.Verbose("[{func}] Empty input", nameof(Normalize));
			return [];
		}

		double maxValue = values.Max();

		if (0 == maxValue)
		{
			return values.Select(v => maxValue > 0 ? scale : 0.0).ToList();
		}

		double scaleFactor = scale / maxValue;

		return values.Select(v => v * scaleFactor).ToList();
	}

	public static double MaxValue(this Matrix<double> matrix)
	{
		return matrix.Enumerate().Max();
	}
}
