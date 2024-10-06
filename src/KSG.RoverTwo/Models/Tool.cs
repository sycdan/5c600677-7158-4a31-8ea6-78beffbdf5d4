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
	/// The default number of time units consumed when using this tool.
	/// </summary>
	public double DefaultWorkTime { get; set; } = 1;

	/// <summary>
	/// The default chance this tool will be used to complete a task.
	/// </summary>
	public double DefaultCompletionChance { get; set; } = 1;

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
