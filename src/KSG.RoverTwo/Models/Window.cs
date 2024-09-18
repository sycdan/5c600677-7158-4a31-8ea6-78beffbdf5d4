namespace KSG.RoverTwo.Models;

public class Window()
{
	public DateTimeOffset Open { get; set; } = DateTimeOffset.MinValue;
	public DateTimeOffset Close { get; set; } = DateTimeOffset.MaxValue;

	public static Window From(DateTimeOffset open, DateTimeOffset close)
	{
		return new Window { Open = open, Close = close };
	}

	public static Window From(DateTimeOffset open, long durationSeconds)
	{
		return new Window { Open = open, Close = open.AddSeconds(durationSeconds) };
	}

	public (long OpenSeconds, long CloseSeconds) RelativeTo(DateTimeOffset tZero)
	{
		return (Math.Max((long)(Open - tZero).TotalSeconds, 0), (long)(Close - tZero).TotalSeconds);
	}
}
