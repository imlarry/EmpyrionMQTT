using System;
using System.Linq;
using System.Diagnostics;

namespace ESB.TopicHandlers
{
    public class MemoryDumper
    {
        public string Dump(string processName)
        {
            // Get the process by name
            Process process = Process.GetProcessesByName(processName).FirstOrDefault();
            if (process == null)
            {
                Console.WriteLine("Could not find a process with the name: " + processName);
                return null;
            }

            // Get the process ID
            int processId = process.Id;

            // Define the dump file name
            string dumpFileName = $"{processName}_{DateTime.Now:yyyyMMdd_HHmmss}.dmp";

            // Use ProcDump to create a memory dump
            Process.Start("C:\\Users\\imlar\\OneDrive\\Desktop\\Procdump\\procdump64", $"-ma -accepteula {processId} {dumpFileName}");

            // Return the dump file name
            return dumpFileName;
        }
    }
}
