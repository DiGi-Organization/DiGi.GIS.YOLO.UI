using System;
using System.IO;
using System.Linq;

namespace DiGi.GIS.YOLO.UI
{
    public static partial class Query
    {
        /// <summary>
        /// Reads the marker of a prediction chunk.
        /// <para>A marker that is missing, empty or does not read is answered with null, which a run treats as a chunk it has not finished: the cost of a damaged marker is the chunk being exported and detected again, never a chunk being stepped over.</para>
        /// </summary>
        /// <param name="path">The path of the marker.</param>
        /// <returns>The marker, or null when there is none to read.</returns>
        public static Classes.YearBuiltPredictionChunk? YearBuiltPredictionChunk(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return null;
            }

            try
            {
                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return null;
                }

                return Core.Convert.ToDiGi<Classes.YearBuiltPredictionChunk>(json)?.FirstOrDefault();
            }
            catch (Exception exception)
            {
                Serilog.Modify.Log(exception, "Failed to read the prediction chunk marker '{Path}'", path);
            }

            return null;
        }
    }
}
