## Article Projects

These projects contain the sample code backing various articles in [Batch documentation](http://azure.microsoft.com/documentation/services/batch/).

### [DotNetTutorial](./DotNetTutorial)
The DotNetTutorial sample project backs the code snippets found in [Get started with the Azure Batch library for .NET ](http://azure.microsoft.com/documentation/articles/batch-dotnet-get-started/). This solution includes two projects - *DotNetTutorial* and *TaskApplication* - that together demonstrate a common Batch application workflow. While the code in the solution does not demonstrate every feature of the Batch service, it is intended to act as a primer for basic Batch concepts and features such as pools, nodes, jobs, and tasks, as well as demonstrate interaction between Batch and the Azure Storage service.

### [EfficientListQueries](./EfficientListQueries)
This console application project backs the code snippets found in [Efficient Batch list queries](http://azure.microsoft.com/documentation/articles/batch-efficient-list-queries/), and demonstrates the value of limiting the type (and therefore the amount) of information returned by the Batch service when querying it for task, job, or other information. This example restricts the properties returned for each of a large number of tasks, showing how query performance is increased by printing elapsed time information for different queries.

### [JobPrepRelease](./JobPrepRelease)
The JobPrepRelease sample project backs the code snippets found in [Run job preparation and completion tasks on Azure Batch compute nodes](http://azure.microsoft.com/documentation/articles/batch-job-prep-release/). The application demonstrates the creation of a CloudJob configured with job preparation and release tasks, then prints information to the console detailing the execution of these and the other CloudTasks.

### [ParallelTasks](./ParallelTasks)
The ParallelTasks sample project backs the code snippets found in [Maximize Azure Batch compute resource usage with concurrent node tasks](http://azure.microsoft.com/documentation/articles/batch-parallel-node-tasks/). The application demonstrates the creation of a Batch pool whose compute nodes are configured for executing multiple concurrent tasks, and prints node and task information to the console during execution to show how tasks are distributed among compute nodes and node cores.
