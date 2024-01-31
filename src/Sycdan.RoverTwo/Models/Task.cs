namespace Sycdan.RoverTwo.Models;

public class Task
{
	public required string ToolId { get; set; }
	public required string Name { get; set; }
	public decimal Value { get; set; }
	public bool Optional { get; set; } = false;
}
