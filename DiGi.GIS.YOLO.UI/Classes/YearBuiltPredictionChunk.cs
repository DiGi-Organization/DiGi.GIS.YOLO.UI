using DiGi.Core.Classes;
using DiGi.GIS.YOLO.UI.Interfaces;
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace DiGi.GIS.YOLO.UI.Classes
{
    /// <summary>
    /// The marker of one prediction chunk the detector has scored: which buildings the chunk holds, which of them it was handed imagery for, and which detector scored them.
    /// <para>Written beside the chunk's results once the detector has finished with it, and only then, so a marker on disk means a complete chunk. A resumed run steps over a chunk whose marker names the same buildings, the same weights and the same confidence threshold, and reads the scored buildings back from it once the chunk's imagery has been removed (DiGi-Organization/DiGi.GIS.YOLO.UI#30).</para>
    /// </summary>
    public class YearBuiltPredictionChunk : SerializableObject, IGISYOLOUISerializableObject
    {
        [JsonInclude, JsonPropertyName(nameof(Completed))]
        private readonly DateTimeOffset completed;

        [JsonInclude, JsonPropertyName(nameof(Confidence))]
        private readonly double confidence;

        [JsonInclude, JsonPropertyName(nameof(ImageCount))]
        private readonly long imageCount;

        [JsonInclude, JsonPropertyName(nameof(Index))]
        private readonly int index;

        [JsonInclude, JsonPropertyName(nameof(ModelSHA256))]
        private readonly string? modelSHA256;

        [JsonInclude, JsonPropertyName(nameof(References))]
        private readonly List<string>? references;

        [JsonInclude, JsonPropertyName(nameof(ScoredReferences))]
        private readonly List<string>? scoredReferences;

        /// <summary>
        /// Initializes a new instance of the <see cref="YearBuiltPredictionChunk"/> class.
        /// </summary>
        /// <param name="index">The zero-based index of the chunk within its county.</param>
        /// <param name="references">The buildings the chunk holds, in ordinal order.</param>
        /// <param name="scoredReferences">The buildings the detector was handed imagery for - the chunk's buildings that have at least one image.</param>
        /// <param name="imageCount">The number of images the detector was handed.</param>
        /// <param name="modelSHA256">The SHA-256 of the weights that scored the chunk.</param>
        /// <param name="confidence">The confidence threshold the detector reported with.</param>
        /// <param name="completed">When the detector finished with the chunk.</param>
        public YearBuiltPredictionChunk(int index, IEnumerable<string>? references, IEnumerable<string>? scoredReferences, long imageCount, string? modelSHA256, double confidence, DateTimeOffset completed)
        {
            this.index = index;
            this.references = references is null ? null : [.. references];
            this.scoredReferences = scoredReferences is null ? null : [.. scoredReferences];
            this.imageCount = imageCount;
            this.modelSHA256 = modelSHA256;
            this.confidence = confidence;
            this.completed = completed;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="YearBuiltPredictionChunk"/> class by copying an existing one.
        /// </summary>
        /// <param name="yearBuiltPredictionChunk">The <see cref="YearBuiltPredictionChunk"/> to copy from.</param>
        public YearBuiltPredictionChunk(YearBuiltPredictionChunk? yearBuiltPredictionChunk)
            : base(yearBuiltPredictionChunk)
        {
            if (yearBuiltPredictionChunk is not null)
            {
                completed = yearBuiltPredictionChunk.completed;
                confidence = yearBuiltPredictionChunk.confidence;
                imageCount = yearBuiltPredictionChunk.imageCount;
                index = yearBuiltPredictionChunk.index;
                modelSHA256 = yearBuiltPredictionChunk.modelSHA256;
                references = yearBuiltPredictionChunk.references is null ? null : new List<string>(yearBuiltPredictionChunk.references);
                scoredReferences = yearBuiltPredictionChunk.scoredReferences is null ? null : new List<string>(yearBuiltPredictionChunk.scoredReferences);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="YearBuiltPredictionChunk"/> class from a <see cref="JsonObject"/>.
        /// </summary>
        /// <param name="jsonObject">The <see cref="JsonObject"/> containing the serialized data.</param>
        public YearBuiltPredictionChunk(JsonObject? jsonObject)
            : base(jsonObject)
        {
        }

        /// <summary>
        /// Gets when the detector finished with the chunk.
        /// </summary>
        [JsonIgnore]
        public DateTimeOffset Completed
        {
            get
            {
                return completed;
            }
        }

        /// <summary>
        /// Gets the confidence threshold the detector reported with. A lower threshold reports more detections, so a chunk scored at another threshold is scored again.
        /// </summary>
        [JsonIgnore]
        public double Confidence
        {
            get
            {
                return confidence;
            }
        }

        /// <summary>
        /// Gets the number of images the detector was handed.
        /// </summary>
        [JsonIgnore]
        public long ImageCount
        {
            get
            {
                return imageCount;
            }
        }

        /// <summary>
        /// Gets the zero-based index of the chunk within its county.
        /// </summary>
        [JsonIgnore]
        public int Index
        {
            get
            {
                return index;
            }
        }

        /// <summary>
        /// Gets the SHA-256 of the weights that scored the chunk.
        /// <para><c>model.pt</c> is replaced in place when a new detector ships, so the file name says nothing; a chunk scored by other weights is scored again.</para>
        /// </summary>
        [JsonIgnore]
        public string? ModelSHA256
        {
            get
            {
                return modelSHA256;
            }
        }

        /// <summary>
        /// Gets the buildings the chunk holds, in ordinal order - every building of the chunk, imagery or not.
        /// </summary>
        [JsonIgnore]
        public List<string>? References
        {
            get
            {
                return references is null ? null : [.. references];
            }
        }

        /// <summary>
        /// Gets the buildings the detector was handed imagery for, fired on or not.
        /// <para>The detection write replaces the detection columns of exactly these buildings (DiGi-Organization/DiGi.GIS.YOLO.UI#21). The chunk's image folder is the other record of them, and it is removed once this marker is written.</para>
        /// </summary>
        [JsonIgnore]
        public List<string>? ScoredReferences
        {
            get
            {
                return scoredReferences is null ? null : [.. scoredReferences];
            }
        }
    }
}
