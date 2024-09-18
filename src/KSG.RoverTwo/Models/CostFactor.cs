using System.Text;
using System.Text.Json.Serialization;
using KSG.RoverTwo.Enums;
using MathNet.Numerics.LinearAlgebra;

namespace KSG.RoverTwo.Models;

public class CostFactor()
{
	/// <summary>
	/// Which metric is being factored into the cost.
	/// </summary>
	public required Metric Metric { get; init; }

	private string? _id;
	public string Id
	{
		get => _id ?? Metric.ToString().ToLower();
		init => _id = value.ToLower();
	}

	/// <summary>
	/// Actual values are arbitrary; what matters is each factor's weight in relation to its peers.
	/// We will normalize this so all weights sum to 100%.
	/// </summary>
	public double Weight { get; init; } = 1;
	public double? NormalizedWeight { get; private set; }

	/// <summary>
	/// Will reduce cost if true.
	/// </summary>
	public bool IsBenefit { get; init; } = false;

	public override string ToString()
	{
		var sb = new StringBuilder();
		sb.Append(Metric.ToString());
		if (!Id.Equals(Metric.ToString(), StringComparison.InvariantCultureIgnoreCase))
		{
			sb.Append($" <{Id}>");
		}
		sb.Append(" @ ");
		if (NormalizedWeight.HasValue)
		{
			sb.Append(Math.Round(NormalizedWeight.Value * 100, 2));
			sb.Append("%");
		}
		else
		{
			sb.Append(Weight);
		}
		return sb.ToString();
	}
}
