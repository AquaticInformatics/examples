using System;
using System.IO;

namespace SondeFileSynchronizer.FileManagement
{
    public class FileInfoSet
    {
        public FileInfoSet(FileInfo originalFileInfo)
        {
            OriginalSondeFile = originalFileInfo ?? throw new ArgumentNullException(nameof(originalFileInfo));
        }

        public FileInfo OriginalSondeFile { get; set; }
        public FileInfo ProcessingSondeFile { get; set; }
        public FileInfo ConvertedSamplesFile { get; set; }
    }
}
