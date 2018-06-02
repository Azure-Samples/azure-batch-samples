## PersistOutputs

This console application sample project backs the code snippets found in [Persist Azure Batch job and task output](http://azure.microsoft.com/documentation/articles/batch-task-output/). This Visual Studio 2017 solution demonstrates how to use the [Azure Batch File Conventions](https://www.nuget.org/packages/Microsoft.Azure.Batch.Conventions.Files/) library to persist task output to durable storage.

To run the sample, follow these steps:

1. Open the project in **Visual Studio 2017**.
2. Add your Batch and Storage **account credentials** to **accountsettings.json** in the Microsoft.Azure.Batch.Samples.Common project.
3. **Build** (but do not run) the solution. Restore any NuGet packages if prompted.
4. If running in "File Conventions" mode, use the Azure portal to upload an [application package](http://azure.microsoft.com/documentation/articles/batch-application-packages/) for **PersistOutputsTask**. Include the `PersistOutputsTask.exe` and its dependent assemblies in the .zip package, set the application ID to "PersistOutputsTask", and the application package version to "1.0".
5. **Start** (run) the **PersistOutputs** project.