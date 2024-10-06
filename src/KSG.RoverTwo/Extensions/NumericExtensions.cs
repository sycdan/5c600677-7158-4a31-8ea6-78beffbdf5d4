using System;
using Serilog;

namespace KSG.RoverTwo.Extensions;

public static class NumericExtensions
{
	public static long AsLong(this double value)
	{
		return (long)Math.Round(value, 0);
	}
}
