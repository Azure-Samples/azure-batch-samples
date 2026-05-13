## TaskDependencies

This C# console application demonstrates the use of task dependencies in Azure Batch. With task dependencies, you can configure scenarios such as the following:

* *taskB* depends on *taskA* (*taskB* will not begin execution until *taskA* has completed)
* *taskC* depends on both *taskA* and *taskB*
* *taskD* depends on a range of tasks, such as tasks *1* through *10*, before it executes

### Requirements

* Task dependencies require a job with [BatchJobCreateOptions][net_jobcreateoptions].`UsesTaskDependencies` set to `true` (the default is `false`). You **must** set this property value to `true` to use task dependencies.

   ```csharp
   var jobOptions = new BatchJobCreateOptions(
       "MyJob",
       new BatchPoolInfo { PoolId = "MyPool" })
   {
       UsesTaskDependencies = true,
   };
   await batchClient.CreateJobAsync(jobOptions);
   ```

* When using **task ranges** for your dependencies, your task IDs must be string representations of integer values.

   ```csharp
   var tasks = new List<BatchTaskCreateOptions>
   {
       new BatchTaskCreateOptions("1", "cmd.exe /c MyTaskExecutable.exe -process data1"),
       new BatchTaskCreateOptions("2", "cmd.exe /c MyTaskExecutable.exe -process data2"),
   };
   ```


[net_jobcreateoptions]: https://learn.microsoft.com/dotnet/api/azure.compute.batch.batchjobcreateoptions
