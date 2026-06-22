using Aspose.Words;
using System.IO;

namespace SearchLego.DataSeeder.FileConvert
{
    public class ApsoseWordLicense : IApsoseWordLicense
    {
        public ApsoseWordLicense(string LicPath)
        {
            if (File.Exists(LicPath))
            {
                License license = new License();
                license.SetLicense(LicPath);
            }
        }

    }
    public interface IApsoseWordLicense
    {

    }
}
