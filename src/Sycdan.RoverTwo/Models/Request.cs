namespace Sycdan.RoverTwo.Models;

public class Request
{
	public string ValueUnit { get; set; } = "Value";
	public DateTimeOffset TZero { get; set; } = DateTimeOffset.UtcNow;
	public required List<Worker> Workers { get; set; }
	public required List<Job> Jobs { get; set; }
	public required List<Hub> Hubs { get; set; }
	public required List<Tool> Tools { get; set; }
}
