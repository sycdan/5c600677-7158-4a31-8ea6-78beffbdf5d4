namespace Sycdan.RoverTwo.Models;

public class Window
{
	public DateTimeOffset Open { get; set; } = DateTimeOffset.MinValue;
	public DateTimeOffset Close { get; set; } = DateTimeOffset.MinValue;
}
