// -----------------------------------------------------------------------------------------
// <copyright file="Program.cs" company="Microsoft">
//    Copyright Microsoft Corporation
// 
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//      http://www.apache.org/licenses/LICENSE-2.0
// 
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.
// </copyright>
// -----------------------------------------------------------------------------------------

using System.Text;

namespace Microsoft.Azure.Batch.Samples.ImgProcSample
{
    using System;

    /// <summary>
    /// The ImgProc.exe main program.
    /// To run the full sample first edit ImgProc.exe.config to contain your Batch and Storage account information.  
    /// Then run the following commands:
    /// ImgProc.exe --Pool --Create
    /// ImgProg.exe
    /// </summary>
    public class Program
    {
        public static void Main(string[] args)
        {
            if (args.Length > 0 && (args[0] == "-help" || args[0] == "/?"))
            {
                Console.WriteLine(BuildHelpString());
                return;
            }

            try
            {
                //We share the same EXE for both the client program and the task
                //Decide which one to start based on the command line parameters
                if (args != null && args.Length > 0 && args[0] == "--Pool")
                {
                    MainProgram.MainProgramPool(args);
                }
                else if (args != null && args.Length > 0 && args[0] == "--Task")
                {
                    ImgProcTask.TaskMain(args);
                }
                else
                {
                    MainProgram.SubmitTasks(args);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception thrown: " + e.ToString());
            }
        }

        /// <summary>
        /// Generates a simple help string.
        /// </summary>
        /// <returns>The help string.</returns>
        private static string BuildHelpString()
        {
            StringBuilder builder = new StringBuilder();

            builder.AppendLine("ImgProc.exe help:");
            builder.AppendLine("ImgProc.exe --Pool --Create                                     Creates the pool.");
            builder.AppendLine("ImgProc.exe --Pool --Delete                                     Deletes the pool.");
            builder.AppendLine("ImgProc.exe --Task <outputContainerSAS> <OutputFilenamePrefix>  Run the task with the specified output container and filename prefix.");
            builder.AppendLine("ImgProc.exe                                                     Create the work item, submit the tasks, and wait for the tasks to complete.");

            return builder.ToString();
        }
    }
}
