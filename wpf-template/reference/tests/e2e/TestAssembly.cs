[assembly: Xunit.v3.Parallelization(
    Mode = Xunit.Sdk.ParallelMode.Collections,
    MaxThreads = 2,
    Algorithm = Xunit.Sdk.ParallelAlgorithm.Conservative)]
