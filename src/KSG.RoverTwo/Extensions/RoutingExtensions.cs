using Google.OrTools.ConstraintSolver;
using Serilog;

namespace KSG.RoverTwo.Extensions;

public static class RoutingExtensions
{
	public static long GetDimensionValueAt(this Assignment solution, long index, RoutingDimension dimension)
	{
		IntVar cumulVar = dimension.CumulVar(index);
		var value = solution.Value(cumulVar);
		Log.Verbose("{a} {b} @ {index} is {value}", nameof(dimension.CumulVar), dimension.Name(), index, value);
		return value;
	}
}
