## MultiInstanceTasks

This C# console application project backs the code snippets found in [Use
multi-instance tasks to run Message Passing Interface (MPI) applications in
Azure Batch][batch_mpi]. It demonstrates how to use a multi-instance task to
run an MS-MPI application on Batch compute nodes.

To successfully run this sample, you must first create an
[application package][batch_app_pkg] containing [MSMpiSetup.exe][msmpi_msdn]
(installed on a pool's compute nodes with a start task) and an
[MS-MPI][msmpi_howto] program for the multi-instance task to execute. For the
latter, we provide the [MPIHelloWorld](./MPIHelloWorld) sample project for you
to compile and use as your MS-MPI program.

For full instructions on running the sample, see the
[Code sample][batch_mpi_sample] section of the Batch MPI article.

[batch_app_pkg]: https://azure.microsoft.com/documentation/articles/batch-application-packages/
[batch_mpi]: https://azure.microsoft.com/documentation/articles/batch-mpi/
[batch_mpi_sample]: https://azure.microsoft.com/documentation/articles/batch-mpi/#code-sample
[msmpi_howto]: http://blogs.technet.com/b/windowshpc/archive/2015/02/02/how-to-compile-and-run-a-simple-ms-mpi-program.aspx
[msmpi_msdn]: https://msdn.microsoft.com/library/bb524831.aspx
