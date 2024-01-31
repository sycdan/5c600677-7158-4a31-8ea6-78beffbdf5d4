# Rover Two

This is a vehicle route optimization proof-of-concept, featuring time windows and (a custom implementation of) capacity.

It is essentially the [Travelling Salesman Problem](https://developers.google.com/optimization/routing/tsp) with many salespeople -- somewhat like a multiplayer game of Snake!

Travel times are calculated as the crow files -- this may be changed to use real drive time in future.

Costs are a accrued by way of workers performing their tasks; costs can be specific based on a combination of worker and job.

If no explicit cost factors are supplied, only the travel time between nodes will be considered.

## Tech

This project leverages [Google OR-Tools](https://developers.google.com/optimization/).

## Problem

A problem consists of multiple jobs over the course of a given day, and one or more workers, each equipped with one or more tools (stored in their vehicle).

Workers use tools at jobs to complete tasks; jobs have time windows in which the worker must begin work, and may require different tools to complete.

The aim is bifold: find a permutation of workers & tools that can complete a minimum percentage of the available work (at least all required tasks), and then minimize the cost to do so.

`Problem` has the following attributes, mostly loaded from a user input by way of a flat file or a request:

- `Value Unit`: optional string
  - Default: "Value"
  - e.g. revenue (used in this document)
- `T Zero`: timestamp
  - Default: now
  - The reference point for all other times in the problem
- `Workers`: list of `Worker` objects
  - Must contain at least 1 item
- `Jobs`: list of `Job` objects
  - Each has a list list of `Task` objects
  - Must contain at least 1 item
- `Tools`: list of `Tool` objects
  - Must contain at least 1 item

## Window

This class is used to define a time box in which an evert must occur.

`Window` has the following attributes:

- `Open`: timestamp
- `Close`: timestamp

## Nodes

Generic locations within the problem, which can be visited by workers.

`Node` has the following attributes:

- `Id`: required string
  - Must be unique among all nodes
- `Name`: optional string
  - Defaults to `Id` if omitted
- `Location`: required

Jobs and hubs are different types of nodes.

### Location

`Location` has the following properties:

- `X`: decimal
  - .e.g latitude
- `Y`: decimal
  - e.g. longitude

### Jobs

A `Job` is a subclass of `Node` and has the following additional attributes:

- `ArrivalWindow`: `Window`
  - The time box within which the worker must arrive on-site
- `Tasks`: list of `Task` instances
  - Informs the type and quantity of tools a worker must bring to complete the job for maximum revenue
  - Includes both optional and required work

#### Tasks

Units of work to be completed by a worker at a `Job` in order to generate revenue.

`Task` has the following attributes:

- `Tool Id`
  - Must be among the worker's `Capabilities` for them to be able to complete the work
- `Name`: required string
- `Value`: decimal
  - Contributes to the worker's total revenue at the end of the problem, assuming they complete the task
- `Optional`: boolean
  - Defaults to `false`
  - If a task is not optional, it must be finished before the worker can move on

### Hubs

A `Hub` is a subclass of `Node`, with no additional attributes, used for organization.

Each worker starts their day at one of these.

## Workers

Workers begin their day at their hub, travel to jobs throughout the day, then return to their hub.

`Worker` has the following attributes:

- `Id`: required string
  - Must be unique among all workers
- `Name`: optional string
  - Will default to `Id` if omitted
- `Hub Id`: required
  - Refers to a `Node`
- `Capabilities`: list of `Capability`

When a worker transits to a job, they spend travel time to get there.

When a worker transits from a job, they spend time using their tools to complete tasks and generate revenue before they leave.

A worker cannot be assigned to a job for which they lack the required tools.

### Capabilities

These combine two concepts:

- the quantities of tools the worker has at the start of their route, dictating the number of jobs they can complete
- the worker's individual proficiency with that type of tool, dictating how long it takes them to use it

A `Capability` has the following attributes:

- `Tool Id`
- `Quantity`: integer
  - Must be > 1
  - Determines the number of jobs the `Worker` can complete using this type of tool
- `Delay Factor`: decimal
  - Default: 1
  - Must be >= 0
  - Multiplied by the `Delay` when this tool type is used by the `Worker`
    - a value of of 0 makes the tool use instant
    - a value of of >1 makes the tool take longer to use than normal

## Tools

Tools are assigned per worker, carried to jobs in their vehicle, and consumed when used (each is single-use).

A tool has the following attributes:

- `Id`: required string
  - Must be unique among all tools
  - Represents the type of tool, e.g. "shovel" or "sword"
- `Name`: optional string
  - Default: `Id`
- `Delay`: optional integer
  - Default: 1
  - Must be >= 1
  - The base time spent when a worker uses this tool

## Distance

Simple geometry is used to calculate distances between nodes on our sample Cartesian plane.

## Costs

We use a different cost evaluator per vehicle. By default, costs is equal to time spent, but other factors can be added dynamically, with custom weights.

Costs are defined by a worker & job pair.

## Outcome

After a `Problem` has been solved, the output will contain:

- Each worker's ETA at each `Node` they visited on their route
- Total cost of all routes
- Cost per worker

## Simulation

After solving a problem, it must be rendered to the user in the form of a tick-by-tick sequence of events (frames), showing how each worker performs their duties.

Ideally this would include a simple 2D ASCII grid showing the location of each renderable entity at every tick.

### Ticks

A tick is an arbitrary measure of time. In the context of a `Problem` it will be one second, but this may become configurable.
