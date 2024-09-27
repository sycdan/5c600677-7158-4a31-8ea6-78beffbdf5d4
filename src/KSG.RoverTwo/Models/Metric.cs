using System.Text;
using KSG.RoverTwo.Enums;
using KSG.RoverTwo.Interfaces;

namespace KSG.RoverTwo.Models;

public class Metric : IAmUnique
{
	public string Id { get; init; } = Guid.NewGuid().ToString();

	/// <summary>
	/// Is this one of the built-in metrics, or a custom metric.
	/// </summary>
	public required MetricType Type { get; init; }

	/// <summary>
	/// Should the optimizer aim to minimize or maximize this metric?
	/// </summary>
	public required MetricMode Mode { get; init; }

	/// <summary>
	/// Actual values are arbitrary; what matters is each factor's weight in relation to its peers.
	/// This will be normalized internally so all weights sum to 100%.
	/// </summary>
	public double Weight { get; init; } = 1;

	public override string ToString()
	{
		var sb = new StringBuilder();
		sb.Append(Mode.ToString());
		sb.Append(' ');
		if (MetricType.Custom.Equals(Type))
		{
			sb.Append(Id);
		}
		else
		{
			sb.Append(Type.ToString());
		}
		sb.Append(" * ");
		sb.Append(Weight);
		return sb.ToString();
	}

	public override int GetHashCode()
	{
		return HashCode.Combine(Id, Type, Mode, Weight);
	}

	public override bool Equals(object? obj)
	{
		if (obj is Metric other)
		{
			return Id.Equals(other.Id)
				&& Type.Equals(other.Type)
				&& Mode.Equals(other.Mode)
				&& Weight.Equals(other.Weight);
		}
		return false;
	}
}
