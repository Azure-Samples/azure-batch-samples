## Article Projects

These projects contain the sample code backing various articles in [Batch documentation](http://azure.microsoft.com/documentation/services/batch/).

### [EfficientListQueries](./EfficientListQueries)
This console application project backs the code snippets found in [Efficient Batch list queries](http://azure.microsoft.com/documentation/articles/batch-efficient-list-queries/), and demonstrates the value of limiting the type (and therefore the amount) of information returned by the Batch service when querying it for task, job, or other information. This example restricts the properties returned for each of a large number of tasks, showing how query performance is increased by printing elapsed time information for different queries.

### [JobPrepRelease](./JobPrepRelease)
The JobPrepRelease sample project backs the code snippets found in [Perform job preparation and completion maintenance in Azure Batch](http://azure.microsoft.com/documentation/articles/batch-job-prep-release/). The application demonstrates the creation of a CloudJob configured with job preparation and release tasks, then prints information to the console detailing the execution of these and the other CloudTasks.

### [ParallelTasks](./ParallelTasks)
The ParallelTasks sample project backs the code snippets found in [Maximize Azure Batch compute resource usage with concurrent node tasks](http://azure.microsoft.com/documentation/articles/batch-parallel-node-tasks/). The application demonstrates the creation of a Batch pool whose compute nodes are configured for executing multiple concurrent tasks, and prints node and task information to the console during execution to show how tasks are distributed among compute nodes and node cores.
