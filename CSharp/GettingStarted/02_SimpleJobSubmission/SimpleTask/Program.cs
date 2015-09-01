namespace Microsoft.Azure.Batch.Samples.SimpleJobSubmission
{
    using System;
    using System.IO;

    // Simple command line program which prints the names of all the files in the current directory
    public class Program
    {
        public static void Main(string[] args)
        {
            foreach (string file in Directory.EnumerateFiles("."))
            {
                Console.WriteLine("Found a file: {0}", file);
            }
        }
    }
}
