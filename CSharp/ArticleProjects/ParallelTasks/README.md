### ParallelTasks

This console application sample project backs the code snippets found in [Maximize Azure Batch compute resource usage with concurrent node tasks](http://azure.microsoft.com/documentation/articles/batch-parallel-node-tasks/). The application demonstrates the creation of a Batch pool whose compute nodes are configured for executing multiple concurrent tasks, submit tasks with variable slots, and prints node/job/task information to the console during execution to show how tasks are distributed among compute nodes and node cores.

1. Open the project in **Visual Studio 2017**.
2. Add your Batch and Storage **account credentials** to **accountsettings.json** in the Microsoft.Azure.Batch.Samples.Common project.
3. **Build** and then **run** the solution. Restore any NuGet packages if prompted.
