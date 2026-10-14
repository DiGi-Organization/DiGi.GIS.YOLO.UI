namespace DiGi.GIS.YOLO.UI.Constants
{
    /// <summary>
    /// Provides constant directory names used within the GIS YOLO UI.
    /// </summary>
    public static class DirectoryName
    {
        /// <summary>
        /// Gets the name of the folder a county's exported orthophoto prediction images are written to.
        /// </summary>
        public const string PredictionImages = "images";

        /// <summary>
        /// Gets the name of the folder under a county's scratch folder that holds the chunks of its prediction imagery.
        /// <para>Each chunk is a folder of its own, named by <see cref="FileName.PredictionChunkFormat"/>, beside its marker and its detector results, so no single folder ever holds the imagery of a whole county (DiGi-Organization/DiGi.GIS.YOLO.UI#30).</para>
        /// </summary>
        public const string PredictionChunks = "chunks";

        /// <summary>
        /// Gets the name of the folder inside a training run that ultralytics writes the checkpoints it can resume from into.
        /// <para>Holds <c>last.pt</c>, the checkpoint a resume continues, and the per-epoch checkpoints a run with <c>save_period</c> leaves behind.</para>
        /// </summary>
        public const string Weights = "weights";
    }
}
