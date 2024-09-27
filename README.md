# RoverTwo

_Saving time for **you**._

## What does it do?

`RoverTwo` is an opinionated route optimizer. It attempts to find the lowest-cost route for X `Workers` to visit Y `Jobs`, while making some simple assumptions:

- Each `Worker`...
  - starts at a `Hub` and will return to a `Hub` at the end of their route
    - the starting and ending `Hub` need not be the same
  - carries a specific set of `Tools` as they travel between `Jobs`
  - has `Capabilities` describing how efficient they are at using a given `Tool`
  - may be explicitly denied eligibility to visit any `Job`
- Each `Job`...
  - must be visited exactly once by one `Worker` within a specific `ArrivalWindow`
  - comprises one or more `Tasks`
- Each `Task`...
  - requires a specific `Tool`, without which it cannot be performed
  - has a `Reward`, which results in a reduced cost if it is completed
- Each `Tool`...
  - takes a minimum amount of time to use, which can be altered by a `Capability`
  - has an average rate at which it is used to complete a `Task` (100% unless otherwise specified)
  - with a 100% completion rate must be represented by a `Capability` in order for that `Worker` to be eligible to visit a `Job` where that `Tool` is required for a `Task`
- Any combination of `Time`, `Distance` & `Value` can be factored into the route cost
  - An arbitrary amount of custom cost `Factors` can also be considered
- Each `Factor` has a `Weight`, which determines how relatively important it is to the overall route cost

## What does it output?

A list of `Visit` objects, each having:

- `PlaceId`
- `WorkerId`
- `ArrivalTime`
- `DepartureTime`
- `Rewards`

## Notes

All `Id` values should be unique within the scope of the problem being solved, and composed of lowercase alphanumerics and dashes.

## TODO

- more tests
  - helper method that creates a basic test which can be mutated
- everything is an entity with an id (ideally guid)
  - use dictionaries by default, with some interface that suggests the id comes from the key
- finalize result output (hash input to name file?)
  - return worktime in results
  - total travel time
  - total distance
  - total reward
  - total work time
  - total cost?
  - cost per visit/worker?
  - R/R ratio?
  - completion %
  - per-visit itemized task completion
- worker break times
- per-worker reward factor
- per-task completion rate
- validate custom risks/rewards on workers
- allow null-place (applies to all) custom metrics
- validate custom metrics all > 0
- allow a place to be marked as "must visit, by any vehicle" vs only specific vehicles
- place sequences (one worker must visit all with no deviation)
- traveltime -> duration where possible
- guarantee that a place must be visited, b y any worker
- experiment with coefficients to decrease distance between best and worst route costs (reduce idle vehicles)

