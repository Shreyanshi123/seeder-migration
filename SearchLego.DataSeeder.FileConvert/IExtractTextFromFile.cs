using SearchLego.DataSeeder.Entities;
using System.Collections.Generic;

namespace SearchLego.DataSeeder.FileConvert
{
    public interface IExtractTextFromFile
    {
        List<DocumentPage> GetTextFromPdf(string filePath);
        List<DocumentPage> GetTextFromExcel(string filePath);
        List<DocumentPage> GetTextFromPdfByAspose(string filePath);
    }
}
