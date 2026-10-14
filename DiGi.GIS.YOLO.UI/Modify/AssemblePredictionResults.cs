using System;
using System.Collections.Generic;
using System.IO;

namespace DiGi.GIS.YOLO.UI
{
    public static partial class Modify
    {
        /// <summary>
        /// Joins the detector results of a county's prediction chunks into the county's results file, in the order given.
        /// <para>The results format has no header and one line per image or detection, and every image of a building is in one chunk, so the joined file says exactly what one pass over all the images would have said. The file is rewritten as a whole every time, through a temporary file moved over it, so a repeated or resumed run never doubles a chunk the way an append would (DiGi-Organization/DiGi.GIS.YOLO.UI#30).</para>
        /// <para>A chunk results file that does not exist is skipped: the detector writes none for a chunk with no imagery.</para>
        /// </summary>
        /// <param name="paths">The chunk results files, in chunk order.</param>
        /// <param name="path">The county results file to write.</param>
        /// <returns><see langword="true"/> when the county results file was written; otherwise <see langword="false"/>.</returns>
        public static bool AssemblePredictionResults(IEnumerable<string>? paths, string? path)
        {
            if (paths is null || string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            string path_Temp = path + ".tmp";

            try
            {
                using (FileStream fileStream_Output = new(path_Temp, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    foreach (string path_Chunk in paths)
                    {
                        if (string.IsNullOrWhiteSpace(path_Chunk) || !File.Exists(path_Chunk))
                        {
                            continue;
                        }

                        using FileStream fileStream_Input = new(path_Chunk, FileMode.Open, FileAccess.Read, FileShare.Read);
                        if (fileStream_Input.Length == 0)
                        {
                            continue;
                        }

                        fileStream_Input.CopyTo(fileStream_Output);

                        // A chunk file whose last line has no line break would run into the next chunk's first line.
                        fileStream_Input.Seek(-1, SeekOrigin.End);
                        if (fileStream_Input.ReadByte() != '\n')
                        {
                            fileStream_Output.WriteByte((byte)'\n');
                        }
                    }
                }

                File.Move(path_Temp, path, true);

                return true;
            }
            catch (Exception exception)
            {
                Serilog.Modify.Log(exception, "Failed to assemble the prediction results '{Path}'", path);

                try
                {
                    if (File.Exists(path_Temp))
                    {
                        File.Delete(path_Temp);
                    }
                }
                catch (IOException)
                {
                    // The temporary file is beside the target and is overwritten by the next attempt.
                }
            }

            return false;
        }
    }
}
