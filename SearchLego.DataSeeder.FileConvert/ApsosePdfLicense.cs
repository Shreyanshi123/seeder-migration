using Aspose.Pdf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SearchLego.DataSeeder.FileConvert
{
    public class ApsosePdfLicense
    {
        public ApsosePdfLicense(string LicPath)
        {
            if (File.Exists(LicPath))
            {
                License license = new License();
                license.SetLicense(LicPath);
            }
        }
    }
}
