## Article Projects

These projects contain the sample code backing various articles in [Batch documentation](http://azure.microsoft.com/documentation/services/batch/).

### [ApplicationInsights](./ApplicationInsights)
This article shows how to add and configure the Application Insights library into your solution and instrument your application code. Futhermore it provides examples on how to monitor your application via the Azure portal and build custom dashboards.

### [EfficientListQueries](./EfficientListQueries)
This console application project backs the code snippets found in [Efficient Batch list queries](http://azure.microsoft.com/documentation/articles/batch-efficient-list-queries/), and demonstrates the value of limiting the type (and therefore the amount) of information returned by the Batch service when querying it for task, job, or other information. This example restricts the properties returned for each of a large number of tasks, showing how query performance is increased by printing elapsed time information for different queries.

### [JobPrepRelease](./JobPrepRelease)
The JobPrepRelease sample project backs the code snippets found in [Run job preparation and completion tasks on Azure Batch compute nodes](http://azure.microsoft.com/documentation/articles/batch-job-prep-release/). The application demonstrates the creation of a CloudJob configured with job preparation and release tasks, then prints information to the console detailing the execution of these and the other CloudTasks.

### [MultiInstanceTasks](./MultiInstanceTasks)
The MultiInstanceTasks sample project backs the code snippets found in [Use multi-instance tasks to run Message Passing Interface (MPI) applications in Azure Batch](http://azure.microsoft.com/documentation/articles/batch-mpi/). It demonstrates how to use a multi-instance task to run an MS-MPI application on Batch compute nodes.

### [ParallelTasks](./ParallelTasks)
The ParallelTasks sample project backs the code snippets found in [Maximize Azure Batch compute resource usage with concurrent node tasks](http://azure.microsoft.com/documentation/articles/batch-parallel-node-tasks/). The application demonstrates the creation of a Batch pool whose compute nodes are configured for executing multiple concurrent tasks, and prints node and task information to the console during execution to show how tasks are distributed among compute nodes and node cores.

### [PersistOutputs](./PersistOutputs)

This PersistOutputs sample project backs the code snippets found in [Persist Azure Batch job and task output](http://azure.microsoft.com/documentation/articles/batch-task-output/). This Visual Studio 2017 solution demonstrates how to use the [Azure Batch File Conventions](https://www.nuget.org/packages/Microsoft.Azure.Batch.Conventions.Files/) library to persist task output to durable storage.

### [TaskDependencies](./TaskDependencies)
The TaskDependencies sample project demonstrates the use of the task dependency feature of Azure Batch. With task dependencies, you can create tasks that depend on the completion of other tasks before they are executed.
