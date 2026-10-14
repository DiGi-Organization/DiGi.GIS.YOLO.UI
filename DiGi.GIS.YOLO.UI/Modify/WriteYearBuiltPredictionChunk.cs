using DiGi.GIS.YOLO.UI.Classes;
using System;
using System.IO;

namespace DiGi.GIS.YOLO.UI
{
    public static partial class Modify
    {
        /// <summary>
        /// Writes the marker of a prediction chunk.
        /// <para>Written to a temporary file beside the target and moved over it, so a run stopped mid-write leaves either the previous marker or none - never a truncated one that reads as a finished chunk.</para>
        /// </summary>
        /// <param name="yearBuiltPredictionChunk">The marker to write.</param>
        /// <param name="path">The path of the marker.</param>
        /// <returns><see langword="true"/> when the marker was written; otherwise <see langword="false"/>.</returns>
        public static bool WriteYearBuiltPredictionChunk(this YearBuiltPredictionChunk? yearBuiltPredictionChunk, string? path)
        {
            if (yearBuiltPredictionChunk is null || string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            string? json = Core.Convert.ToSystem_String(yearBuiltPredictionChunk);
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            string path_Temp = path + ".tmp";

            try
            {
                string? directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(path_Temp, json);
                File.Move(path_Temp, path, true);

                return true;
            }
            catch (Exception exception)
            {
                Serilog.Modify.Log(exception, "Failed to write the prediction chunk marker '{Path}'", path);
            }

            return false;
        }
    }
}
