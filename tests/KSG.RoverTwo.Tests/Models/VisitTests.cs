using KSG.RoverTwo.Models;
using Build = KSG.RoverTwo.Tests.Helpers.Builder;

namespace KSG.RoverTwo.Tests.Models;

public class VisitTests : TestBase
{
	[Theory]
	[InlineData("0001-01-01T00:00:00-00:00", 18000, "0001-01-01T00:00:00-05:00")]
	[InlineData("1997-08-29T02:14:00-05:00", 1, "1997-08-29T02:14:01-05:00")]
	public void ArrivalTime_PlusWorkSeconds_EqualsDepartureTime(
		string arrivalTimeString,
		long workSeconds,
		string departureTimeString
	)
	{
		var job = Build.Job();
		var worker = Build.Worker();
		var visit = new Visit
		{
			Worker = worker,
			Place = job,
			ArrivalTime = DateTimeOffset.Parse(arrivalTimeString),
		};

		visit.WorkSeconds += workSeconds;

		Assert.Equal(DateTimeOffset.Parse(departureTimeString), visit.DepartureTime);
	}
}
