using DiGi.Core.Classes;
using DiGi.GIS.YOLO.UI.Interfaces;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace DiGi.GIS.YOLO.UI.Classes
{
    /// <summary>
    /// One area kept out of a YOLO training dataset: an axis-aligned bounding box in EPSG:2180, named by <see cref="YOLOTrainingDatasetOptions.ExclusionBoundingBoxes"/> and grown by <see cref="YOLOTrainingDatasetOptions.ExclusionBuffer"/> before every intersection test.
    /// <para>The coordinates are stored as given; the intersection test reads the area between them, so a corner pair supplied in any order still names the same box.</para>
    /// </summary>
    public class YOLOExclusionBoundingBox : SerializableObject, IGISYOLOUISerializableObject
    {
        [JsonInclude, JsonPropertyName(nameof(MaxX))]
        private readonly double maxX;

        [JsonInclude, JsonPropertyName(nameof(MaxY))]
        private readonly double maxY;

        [JsonInclude, JsonPropertyName(nameof(MinX))]
        private readonly double minX;

        [JsonInclude, JsonPropertyName(nameof(MinY))]
        private readonly double minY;

        /// <summary>
        /// Initializes a new instance of the <see cref="YOLOExclusionBoundingBox"/> class from its corner coordinates in EPSG:2180.
        /// </summary>
        /// <param name="minX">The minimum easting.</param>
        /// <param name="minY">The minimum northing.</param>
        /// <param name="maxX">The maximum easting.</param>
        /// <param name="maxY">The maximum northing.</param>
        public YOLOExclusionBoundingBox(double minX, double minY, double maxX, double maxY)
        {
            this.minX = minX;
            this.minY = minY;
            this.maxX = maxX;
            this.maxY = maxY;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="YOLOExclusionBoundingBox"/> class by copying an existing one.
        /// </summary>
        /// <param name="yOLOExclusionBoundingBox">The <see cref="YOLOExclusionBoundingBox"/> to copy from.</param>
        public YOLOExclusionBoundingBox(YOLOExclusionBoundingBox? yOLOExclusionBoundingBox)
            : base(yOLOExclusionBoundingBox)
        {
            if (yOLOExclusionBoundingBox is not null)
            {
                maxX = yOLOExclusionBoundingBox.maxX;
                maxY = yOLOExclusionBoundingBox.maxY;
                minX = yOLOExclusionBoundingBox.minX;
                minY = yOLOExclusionBoundingBox.minY;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="YOLOExclusionBoundingBox"/> class from a <see cref="JsonObject"/>.
        /// </summary>
        /// <param name="jsonObject">The <see cref="JsonObject"/> containing the serialized data.</param>
        public YOLOExclusionBoundingBox(JsonObject? jsonObject)
            : base(jsonObject)
        {
        }

        /// <summary>
        /// Gets the maximum easting.
        /// </summary>
        [JsonIgnore]
        public double MaxX => maxX;

        /// <summary>
        /// Gets the maximum northing.
        /// </summary>
        [JsonIgnore]
        public double MaxY => maxY;

        /// <summary>
        /// Gets the minimum easting.
        /// </summary>
        [JsonIgnore]
        public double MinX => minX;

        /// <summary>
        /// Gets the minimum northing.
        /// </summary>
        [JsonIgnore]
        public double MinY => minY;
    }
}
