using KSG.RoverTwo.Interfaces;

namespace KSG.RoverTwo.Models;

public class Tool : IAmUnique
{
	public string Id { get; init; } = Guid.NewGuid().ToString();

	/// <summary>
	/// Optional display name.
	/// </summary>
	public string? Name { get; set; }

	/// <summary>
	/// The base number of time units consumed when using this tool.
	/// </summary>
	public required double Delay { get; set; }

	/// <summary>
	/// The rate at which this tool is typically used to complete tasks.
	/// </summary>
	public double CompletionRate { get; init; } = 1.0;

	public override string ToString()
	{
		if (string.IsNullOrWhiteSpace(Name))
		{
			return $"{nameof(Tool)}:{Id}";
		}
		return Name;
	}

	public override int GetHashCode()
	{
		return Id.GetHashCode();
	}

	public override bool Equals(object? obj)
	{
		if (obj is Tool other)
		{
			return Id.Equals(other.Id);
		}
		return false;
	}
}
