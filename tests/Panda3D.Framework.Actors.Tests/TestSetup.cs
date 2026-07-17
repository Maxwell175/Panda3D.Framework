using Xunit;

// Actor tests load models, bind animations, render offscreen, and touch Panda's global task/clock
// state. Keep them serial with the rest of the native-loop tests.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
