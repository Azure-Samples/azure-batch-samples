using System;

namespace Microsoft.Azure.Batch.Samples.TextSearch
{
    /// <summary>
    /// This map-reduce style sample uses Azure Batch to perform parallel text processing on an
    /// input file by splitting it up into multiple sub-files and performing regular expression
    /// matching on each sub-file. The results are then rolled-up into a final report.
    /// 
    /// JobSubmitter - Uploads the files required for the text processing to Azure Storage and submits a 
    /// job to the Azure Batch service that utilizes the autopool functionality. Also provides a job manager task.  
    /// The job manager task will run on the autopool and drive the work done on the Batch Service.
    /// 
    /// The JobManager task - The job manager task submits mapper and reducer tasks and also monitors the 
    /// status of those tasks.
    /// 
    /// The mapper tasks - Each task processes a subsection of the original input file and writes the results to standard out.
    /// 
    /// The reducer task - Aggregates the output of the mapper tasks and writes it to standard out.
    /// 
    /// Note: Most arguments to this program are controlled via the .settings file/app.config.  Before running the sample
    /// there are fields in that file which must be populated.
    /// </summary>
    public class Program
    {
        //The same Exe is shared for the job submitter, job manager task, mapper task, reducer task
        //Decide which one to start based on the command line parameters.
        public static void Main(string[] args)
        {
            if (args != null && args.Length > 0)
            {
                if (args[0] == "/?" || args[0] == "-help")
                {
                    DisplayUsage();
                    return;
                }

                if (args[0] == "-JobManagerTask")
                {
                    TextSearchJobManagerTask jobManager = new TextSearchJobManagerTask();
                    jobManager.RunAsync().Wait();
                }
                else if (args[0] == "-MapperTask")
                {
                    if (args.Length != 2)
                    {
                        DisplayUsage();
                        throw new ArgumentException("Incorrect number of arguments");
                    }

                    string blobSas = args[1];

                    MapperTask mapperTask = new MapperTask(blobSas);
                    mapperTask.RunAsync().Wait();
                }
                else if (args[0] == "-ReducerTask")
                {
                    ReducerTask reducerTask = new ReducerTask();
                    reducerTask.RunAsync().Wait();
                }
                else if (args[0] == "-SubmitJob")
                {
                    JobSubmitter submitter = new JobSubmitter();
                    submitter.RunAsync().Wait();
                }
                else
                {
                    DisplayUsage();
                    throw new ArgumentException("Invalid option " + args[0]);
                }
            }
            else
            {
                DisplayUsage();
                return;
            }
        }

        /// <summary>
        /// Displays the usage of this executable.
        /// </summary>
        private static void DisplayUsage()
        {
            Console.WriteLine("{0} Usage:", Constants.TextSearchExe);
            Console.WriteLine("{0} -SubmitJob                   - Runs the job submitter", Constants.TextSearchExe);
            Console.WriteLine("{0} -JobManagerTask              - Runs the job manager, submits tasks and waits for their completion", Constants.TextSearchExe);
            Console.WriteLine("{0} -MapperTask <blob SAS>       - Runs the mapper task, which downloads a file and performs a search on it", Constants.TextSearchExe);
            Console.WriteLine("{0} -ReducerTask                 - Runs the reducer task which collects the output of all the mapper tasks and combines it", Constants.TextSearchExe);
        }
    }
}
