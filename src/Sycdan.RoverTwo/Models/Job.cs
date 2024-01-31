namespace Sycdan.RoverTwo.Models;

public class Job : Place
{
	public required Window ArrivalWindow { get; set; }
	public required List<Task> Tasks { get; set; }
}
