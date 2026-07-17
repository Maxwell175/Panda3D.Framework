using Xunit;

// The engine globals (ClockObject, AsyncTaskManager, EventQueue) are process-wide singletons, and
// the smoke test drives the one global task manager. Run tests serially so they don't collide.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
