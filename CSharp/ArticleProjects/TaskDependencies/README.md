## TaskDependencies

This C# console application demonstrates the use of task dependencies in Azure Batch. With task dependencies, you can configure scenarios such as the following:

* *taskB* depends on *taskA* (*taskB* will not begin execution until *taskA* has completed)
* *taskC* depends on both *taskA* and *taskB*
* *taskD* depends on a range of tasks, such as tasks *1* through *10*, before it executes
