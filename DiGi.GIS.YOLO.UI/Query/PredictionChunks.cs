using System;
using System.Collections.Generic;
using System.Linq;

namespace DiGi.GIS.YOLO.UI
{
    public static partial class Query
    {
        /// <summary>
        /// Splits the buildings of a county into the chunks a prediction run exports and detects one at a time.
        /// <para>The references are de-duplicated, ordered ordinally and cut into consecutive runs of at most <paramref name="chunkSize"/>, so the same buildings always make the same chunks - which is what lets a resumed run recognise a chunk it finished before (DiGi-Organization/DiGi.GIS.YOLO.UI#30).</para>
        /// </summary>
        /// <param name="references">The building references. Null and blank entries are skipped.</param>
        /// <param name="chunkSize">The largest number of buildings in one chunk.</param>
        /// <returns>The chunks in order; empty when there are no references or <paramref name="chunkSize"/> is below one.</returns>
        public static List<List<string>> PredictionChunks(IEnumerable<string?>? references, int chunkSize)
        {
            List<List<string>> result = [];

            if (references is null || chunkSize < 1)
            {
                return result;
            }

            HashSet<string> references_Unique = new(StringComparer.Ordinal);
            foreach (string? reference in references)
            {
                if (!string.IsNullOrWhiteSpace(reference))
                {
                    references_Unique.Add(reference);
                }
            }

            List<string> references_Sorted = [.. references_Unique.OrderBy(x => x, StringComparer.Ordinal)];

            for (int i = 0; i < references_Sorted.Count; i += chunkSize)
            {
                result.Add(references_Sorted.GetRange(i, Math.Min(chunkSize, references_Sorted.Count - i)));
            }

            return result;
        }
    }
}
