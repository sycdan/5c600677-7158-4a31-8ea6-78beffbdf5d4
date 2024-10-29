# RoverTwo

_Saving time for **you**._

## What does it do?

`RoverTwo` is an opinionated route optimizer. It attempts to find the lowest-cost route for X `Workers` to visit Y `Jobs`, while making some simple assumptions:

- Each `Worker`...
  - starts at a `Hub` and will return to a `Hub` at the end of their route
    - the starting and ending `Hub` need not be the same
  - carries a specific set of `Tools` as they travel between `Jobs`
  - has `Capabilities` describing how efficient they are at using a given `Tool`
  - is only allowed to visit a job if they are capable with all required `Tools`
- Each `Job`...
  - must be visited by a `Worker` within a specific `ArrivalWindow`
  - may be optional, in which case it can be skipped if no worker has time for it
  - comprises one or more `Tasks`
- Each `Task`...
  - requires a specific `Tool`, without which it cannot be completed
  - has one or more reward `Metrics`, which result in a reduced route cost when earned
- Each `Tool`...
  - takes a certain amount of time to use, which can be altered by a `Capability`
  - has an average rate at which it is used to complete a `Task` (100% unless otherwise specified)
- Each `Metric` has a `Weight`, which determines how relatively important it is to the overall route cost

## What does it output?

A list of `Visit` objects, each having:

- `PlaceId`
- `WorkerId`
- `ArrivalTime`
- `DepartureTime`
- `EarnedRewards`
- `CompletedTasks`

A list of `SkippedJobs`.

## Notes

All `Id` values must be unique within the scope of the problem being solved. `UUID` is recommended.

## Strategies

### Black/white-listing

If you want to only allow certain workers to visit certain jobs, you can create a "visit" tool for each place, and give some workers a capability with that tool, then add a task to the job that requires the job's visit tool.

If you want to make certain places visitable by only one worker, you can add a "vehicle" tool (and a matching capability) for each worker, then add a job task that requires that tool.

### Break times

To implement worker break times, you can add a "break tool" to each worker, and a "job" with a task that requires it. If you do not specify a location for the job, it will have 0 distance to all nodes, thus can be reached easily from anywhere when it fits best into the solution. The job's arrival window can be larger than the amount of work time required to use the tool, for added flexibility.

## Running

There is a sample json input file in the `docs` directory.

```bash
dotnet run --project src/KSG.RoverTwo/KSG.RoverTwo.csproj "$(cat docs/vikings.json)" --pretty
```

## TODO

- experiment with coefficients to decrease distance between best and worst route costs (reduce idle vehicles)
