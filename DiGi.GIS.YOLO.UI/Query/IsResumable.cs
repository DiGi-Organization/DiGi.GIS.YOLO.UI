using DiGi.GIS.YOLO.UI.Classes;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DiGi.GIS.YOLO.UI
{
    public static partial class Query
    {
        /// <summary>
        /// Checks whether a prediction chunk's marker records the work a run is about to do, so the run can step over the chunk instead of exporting and detecting it again.
        /// <para>It has to name the same buildings in the same order, the same weights by SHA-256 and the same confidence threshold. The weights are compared by hash because <c>model.pt</c> is replaced in place, so a marker left by an earlier detector would otherwise stand in for the current one (DiGi-Organization/DiGi.GIS.YOLO.UI#30).</para>
        /// </summary>
        /// <param name="yearBuiltPredictionChunk">The marker read from disk.</param>
        /// <param name="references">The buildings of the chunk as the run splits them.</param>
        /// <param name="modelSHA256">The SHA-256 of the weights the run detects with.</param>
        /// <param name="confidence">The confidence threshold the run detects with.</param>
        /// <returns><see langword="true"/> when the marker records exactly this chunk scored by these weights at this threshold; otherwise <see langword="false"/>.</returns>
        public static bool IsResumable(this YearBuiltPredictionChunk? yearBuiltPredictionChunk, IEnumerable<string>? references, string? modelSHA256, double confidence)
        {
            if (yearBuiltPredictionChunk is null || references is null || string.IsNullOrWhiteSpace(modelSHA256))
            {
                return false;
            }

            if (!string.Equals(yearBuiltPredictionChunk.ModelSHA256, modelSHA256, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (yearBuiltPredictionChunk.Confidence != confidence)
            {
                return false;
            }

            if (yearBuiltPredictionChunk.References is not List<string> references_Chunk || yearBuiltPredictionChunk.ScoredReferences is null)
            {
                return false;
            }

            return references_Chunk.SequenceEqual(references, StringComparer.Ordinal);
        }
    }
}
