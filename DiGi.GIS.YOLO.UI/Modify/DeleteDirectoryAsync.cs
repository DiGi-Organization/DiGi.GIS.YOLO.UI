using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DiGi.GIS.YOLO.UI
{
    public static partial class Modify
    {
        /// <summary>
        /// Deletes a directory and everything in it, trying again after a short back-off when a file in it is held open.
        /// <para>A recursive delete is not atomic: it removes what it can and throws on the first file another process holds, which leaves a folder that looks neither finished nor untouched. A run's own results file being read by a monitoring tool at the moment the run cleans up is enough (county 55417, DiGi-Organization/DiGi.GIS.YOLO.UI#30), and such a hold is usually brief, so the delete is retried. When it still fails, what is left is logged - how many files and which - rather than only the exception.</para>
        /// </summary>
        /// <param name="directory">The directory to delete. A directory that does not exist counts as deleted.</param>
        /// <param name="attempts">How many times the delete is tried. Below one, it is tried once.</param>
        /// <param name="delay">The wait before the second attempt, doubled before each one after it. Null waits 250 milliseconds.</param>
        /// <returns>A task returning <see langword="true"/> when the directory no longer exists; otherwise <see langword="false"/>.</returns>
        public static async Task<bool> DeleteDirectoryAsync(string? directory, int attempts = 3, TimeSpan? delay = null)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                return false;
            }

            int attempts_Temp = attempts < 1 ? 1 : attempts;
            TimeSpan delay_Temp = delay ?? TimeSpan.FromMilliseconds(250);

            Exception? exception_Last = null;
            for (int i = 0; i < attempts_Temp; i++)
            {
                if (i != 0)
                {
                    await Task.Delay(delay_Temp);
                    delay_Temp += delay_Temp;
                }

                try
                {
                    if (Directory.Exists(directory))
                    {
                        Directory.Delete(directory, true);
                    }

                    return true;
                }
                catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
                {
                    exception_Last = exception;
                }
            }

            List<string> paths_Remaining = [];
            try
            {
                paths_Remaining = [.. Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)];
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                // What is left is reported only as far as it can be listed.
            }

            Serilog.Modify.Log(exception_Last, "The directory {Directory} could not be removed after {Attempts} attempt(s) - {RemainingCount} file(s) remain, among them {RemainingPaths}", directory, attempts_Temp, paths_Remaining.Count, string.Join(", ", paths_Remaining.Take(5)));

            return false;
        }
    }
}
