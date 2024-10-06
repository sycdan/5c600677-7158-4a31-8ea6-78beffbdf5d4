using KSG.RoverTwo.Enums;

namespace KSG.RoverTwo.Helpers;

public static class ConvertDistance
{
	internal static readonly Dictionary<DistanceUnit, double> Factors =
		new()
		{
			{ DistanceUnit.Foot, 0.3048 },
			{ DistanceUnit.Metre, 1 },
			{ DistanceUnit.Ell, 1.143 },
			{ DistanceUnit.Fathom, 1.8288 },
			{ DistanceUnit.Peninkulma, 6000 },
			{ DistanceUnit.Rast, 10000 },
		};

	/// <summary>
	/// Converts a value from a specific unit to meters, which are used internally.
	/// </summary>
	/// <param name="value"></param>
	/// <param name="distanceUnit"></param>
	/// <returns>Distance in meters.</returns>
	/// <exception cref="NotImplementedException">If no factor is defined for the requested unit..</exception>
	public static double ToMeters(double value, DistanceUnit distanceUnit)
	{
		if (Factors.TryGetValue(distanceUnit, out double factor))
		{
			return value * factor;
		}
		throw new NotImplementedException($"{nameof(Factors)} does not contain {distanceUnit}.");
	}

	/// <summary>
	/// Converts a value in meters to a specific distance unit.
	/// </summary>
	/// <param name="value"></param>
	/// <param name="distanceUnit"></param>
	/// <returns>Distance in the requested unit.</returns>
	/// <exception cref="NotImplementedException">If no factor is defined for the requested unit.</exception>
	public static double FromMeters(double meters, DistanceUnit distanceUnit)
	{
		if (Factors.TryGetValue(distanceUnit, out double factor))
		{
			return meters / factor;
		}
		throw new NotImplementedException($"{nameof(Factors)} does not contain {distanceUnit}.");
	}
}
