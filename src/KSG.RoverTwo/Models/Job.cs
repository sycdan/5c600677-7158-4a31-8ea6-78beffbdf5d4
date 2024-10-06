namespace KSG.RoverTwo.Models;

public class Job : Place
{
	public bool Optional { get; set; } = false;
	public required List<Task> Tasks { get; init; }
	public Window ArrivalWindow { get; set; } = new();
}
