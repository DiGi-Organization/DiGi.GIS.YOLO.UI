using DiGi.Geometry.Planar.Classes;
using DiGi.GIS.YOLO.UI.Classes;
using System;
using System.Collections.Generic;

namespace DiGi.GIS.YOLO.UI
{
    public static partial class Query
    {
        /// <summary>
        /// Tests a footprint envelope against the exclusion bounding boxes of a YOLO training dataset: the envelope grown by the exclusion buffer on every side is excluded when it touches or crosses any box - an edge that only touches counts as inside, so a crop never slips past a boundary it lands exactly on.
        /// <para>An empty or null box list excludes nothing, a box whose minimum and maximum were given the wrong way round is read as the area between them, and an envelope without coordinates excludes nothing. A negative buffer is read as 0 - the boxes exactly as drawn.</para>
        /// </summary>
        /// <param name="boundingBox2D">The footprint envelope to test, or null when no footprint could be read.</param>
        /// <param name="yOLOExclusionBoundingBoxes">The exclusion bounding boxes, in EPSG:2180.</param>
        /// <param name="exclusionBuffer">The distance, in metres, the envelope is grown by on every side before the test.</param>
        /// <returns>True when the grown envelope touches or crosses any exclusion bounding box; false when nothing excludes it.</returns>
        public static bool IsExcluded(this BoundingBox2D? boundingBox2D, IEnumerable<YOLOExclusionBoundingBox?>? yOLOExclusionBoundingBoxes, double exclusionBuffer)
        {
            if (boundingBox2D is null || yOLOExclusionBoundingBoxes is null)
            {
                return false;
            }

            double buffer = Math.Max(exclusionBuffer, 0);

            Point2D point2D_Min = boundingBox2D.Min;
            Point2D point2D_Max = boundingBox2D.Max;

            double minX = point2D_Min.X - buffer;
            double minY = point2D_Min.Y - buffer;
            double maxX = point2D_Max.X + buffer;
            double maxY = point2D_Max.Y + buffer;

            foreach (YOLOExclusionBoundingBox? yOLOExclusionBoundingBox in yOLOExclusionBoundingBoxes)
            {
                if (yOLOExclusionBoundingBox is null)
                {
                    continue;
                }

                double exclusionMinX = Math.Min(yOLOExclusionBoundingBox.MinX, yOLOExclusionBoundingBox.MaxX);
                double exclusionMaxX = Math.Max(yOLOExclusionBoundingBox.MinX, yOLOExclusionBoundingBox.MaxX);
                double exclusionMinY = Math.Min(yOLOExclusionBoundingBox.MinY, yOLOExclusionBoundingBox.MaxY);
                double exclusionMaxY = Math.Max(yOLOExclusionBoundingBox.MinY, yOLOExclusionBoundingBox.MaxY);

                if (minX <= exclusionMaxX && maxX >= exclusionMinX && minY <= exclusionMaxY && maxY >= exclusionMinY)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
