namespace Sycdan.RoverTwo.Models;

public class Capability
{
	public required string ToolId { get; set; }
	public int Quantity { get; set; }
	public decimal DelayFactor { get; set; } = 1;
}
