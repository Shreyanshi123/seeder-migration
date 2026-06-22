using Aspose.Slides;
using System;
using System.Diagnostics;

namespace SearchLego.DataSeeder.FileConvert
{
    public class ConvertFileToPDF : IConvertFileToPDF
    {
        public bool ConvertToPDF(string arguments, string libreOfficePath)
        {
            bool processStatus = true;
            lock (this)
            {
                Process Process;
                Process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        WindowStyle = ProcessWindowStyle.Hidden,
                        FileName = libreOfficePath,
                        Arguments = arguments
                    }
                };
                try
                {
                    using (Process)
                    {
                        Process.Start();
                        Process.WaitForExit();
                    }
                }
                catch (Exception e)
                {
                    processStatus = false;
                }
            }


            return processStatus;

        }

        public bool ConvertToPDFAspose(string sourcePath, string destPath, string sourceType)
        {
            try
            {
                if (sourceType.ToLower() == "pptx" || sourceType.ToLower() == "ppt")
                    AsposePptToPdf(sourcePath, destPath);
                else if (sourceType.ToLower() == "doc" || sourceType.ToLower() == "docx" || sourceType.ToLower() == "txt")
                {

                    Aspose.Words.Document doc = new Aspose.Words.Document(sourcePath);
                    doc.Save(destPath);
                    doc = null;
                }
                return true;
            }
            catch (Exception ex)
            {
                throw ex;
            }

        }
        private void AsposePptToPdf(string sourcePath, string destPath)
        {

            using (Presentation presentation = new Presentation(sourcePath))
            {
                //PdfOptions pdfOptions = new PdfOptions();
                //// Set Jpeg quality
                //pdfOptions.JpegQuality = 10;
                //// Set behavior for metafiles
                //pdfOptions.SaveMetafilesAsPng = true;
                //// Set text compression level
                //pdfOptions.TextCompression = PdfTextCompression.Flate;
                //// Define the PDF standard
                //pdfOptions.Compliance = PdfCompliance.Pdf15;
                // Save the presentation as PDF
                presentation.Save(destPath, Aspose.Slides.Export.SaveFormat.Pdf);
            }


        }
    }
}
