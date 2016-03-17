## TaskDependencies

This C# console application demonstrates the use of task dependencies in Azure Batch. With task dependencies, you can configure scenarios such as the following:

* *taskB* depends on *taskA* (*taskB* will not begin execution until *taskA* has completed)
* *taskC* depends on both *taskA* and *taskB*
* *taskD* depends on a range of tasks, such as tasks *1* through *10*, before it executes

### Requirements

* Task dependencies require a job with [CloudJob][net_cloudjob].[UsesTaskDependencies][net_cloudjob_usestdp] set to `true` (the default is `false`). You **must** set this property value to `true` to use task dependencies.

   ```
   CloudJob myJob = batchClient.JobOperations.CreateJob(
       "MyJob",
       new PoolInformation { PoolId = "MyPool" });

   myJob.UsesTaskDependencies = true;
   ```

* When using **task ranges** for your dependencies, your task IDs must be string reprepresentations of integer values.

   ```
   List<CloudTask> tasks = new List<CloudTask>
   {
       new CloudTask("1", "cmd.exe /c MyTaskExecutable.exe -process data1")
       new CloudTask("2", "cmd.exe /c MyTaskExecutable.exe -process data2")
   };
   ```


[net_cloudjob]: https://msdn.microsoft.com/library/azure/microsoft.azure.batch.cloudjob.aspx
[net_cloudjob_usestdp]: https://msdn.microsoft.com/library/azure/microsoft.azure.batch.cloudjob.usestaskdependencies.aspx