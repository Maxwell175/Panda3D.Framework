using Xunit;

// Panda's task manager, event queue, clock, and audio globals are process-wide. Keep the audio tests
// serial with the rest of the suite's native-loop tests.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
