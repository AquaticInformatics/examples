using System.IO;
using SondeFileSynchronizer.Config;
using SondeFileSynchronizer.FileManagement;

namespace SondeFileSynchronizer.Transform
{
    public class SondeFileConverter
    {
        private readonly Context _context;
        private readonly SondeFileManager _fileMan;

        public SondeFileConverter(Context context)
        {
            _context = context;
            _fileMan = new SondeFileManager(_context.Setting);
        }

        public FileInfo ToSamplesObservationFile(FileInfo sondeFileInfo)
        {
            return new FileInfo(_fileMan.GetConvertedSamplesFilePath(sondeFileInfo));
        }
    }
}
