using Aspose.Slides;
using System.IO;

namespace SearchLego.DataSeeder.FileConvert
{
    public class ApsoseSlideLicense : IApsoseSlideLicense
    {
        public ApsoseSlideLicense(string LicPath)
        {
            if (File.Exists(LicPath))
            {
                License license = new License();
                license.SetLicense(LicPath);
            }
        }
    }
    public interface IApsoseSlideLicense
    {

    }
}
