//Copyright (c) Microsoft Corporation

namespace Microsoft.Azure.Batch.Samples.TopNWordsSample
{
    using System.Threading.Tasks;

    public class Program
    {
        public static void Main(string[] args)
        {
            //We share the same EXE for both the main program and the task
            //Decide which one to start based on the command line parameters
            if (args != null && args.Length > 0 && args[0] == "--Task")
            {
                TopNWordsTask.TaskMain(args);
            }
            else
            {
                Task.Run(() => Job.JobMain(args)).GetAwaiter().GetResult();
            }
        }
    }
}
