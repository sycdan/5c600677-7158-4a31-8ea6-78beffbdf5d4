using KSG.RoverTwo.Enums;

namespace KSG.RoverTwo.Helpers;

public static class ConvertTime
{
	internal static readonly Dictionary<TimeUnit, double> Factors =
		new()
		{
			{ TimeUnit.Second, 1 },
			{ TimeUnit.Minute, 60 },
			{ TimeUnit.Hour, 3600 },
		};

	/// <summary>
	/// Converts a value from a specific unit to seconds, which are used internally.
	/// </summary>
	/// <param name="value"></param>
	/// <param name="timeUnit"></param>
	/// <returns>Duration in seconds.</returns>
	/// <exception cref="NotImplementedException">If no factor is defined for the requested unit.</exception>
	public static double ToSeconds(double value, TimeUnit timeUnit)
	{
		if (Factors.TryGetValue(timeUnit, out double factor))
		{
			return value * factor;
		}
		throw new NotImplementedException($"{nameof(Factors)} does not contain {timeUnit}.");
	}

	/// <summary>
	/// Converts a value in seconds to a specific time unit.
	/// </summary>
	/// <param name="value"></param>
	/// <param name="timeUnit"></param>
	/// <returns>Duration in seconds.</returns>
	/// <exception cref="NotImplementedException">If no factor is defined for the requested unit.</exception>
	public static double FromSeconds(double seconds, TimeUnit timeUnit)
	{
		if (Factors.TryGetValue(timeUnit, out double factor))
		{
			return seconds / factor;
		}
		throw new NotImplementedException($"{nameof(Factors)} does not contain {timeUnit}.");
	}
}
