namespace Sycdan.RoverTwo;

public static class Extensions
{
	public static IEnumerable<(T Item, int Index)> WithIndex<T>(this IEnumerable<T> source)
	{
		int index = 0;
		foreach (var item in source)
		{
			yield return (item, index++);
		}
	}

	// public static long SecondsSince(this DateTimeOffset date, DateTimeOffset tZero)
	// {
	// 	return (long)(date - tZero).TotalSeconds;
	// }
}
