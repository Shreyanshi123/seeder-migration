using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;
using Nest;
using SearchLego.DataSeeder.Common;
using SearchLego.DataSeeder.Entities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;

namespace SearchLego.DataSeeder.FileConvert
{
    public class ExtractTextFromFile : IExtractTextFromFile
    {
        public List<DocumentPage> GetTextFromPdf(string filePath)
        {
            List<DocumentPage> pageWiseContents = new List<DocumentPage>();
            if (File.Exists(filePath))
            {
                lock (this)
                {
                    using (PdfReader reader = new PdfReader(filePath))
                    {
                        for (int i = 1; i <= reader.NumberOfPages; i++)
                        {
                            ITextExtractionStrategy strategy = new SimpleTextExtractionStrategy();
                            string currentText = PdfTextExtractor.GetTextFromPage(reader, i, strategy);
                            currentText = Encoding.UTF8.GetString(ASCIIEncoding.Convert(Encoding.Default, Encoding.UTF8, Encoding.Default.GetBytes(currentText)));
                            pageWiseContents.Add(new DocumentPage() { PageNumber = i, Text = CommonUtility.CleanData(currentText) });
                        }
                        reader.Close();

                    }
                }

            }
            return pageWiseContents;

        }

        public List<DocumentPage> GetTextFromPdfByAspose(string filePath)
        {
            List<DocumentPage> pageWiseContents = new List<DocumentPage>();
            if (File.Exists(filePath))
            {
                StringBuilder _sBuilder = new StringBuilder();

                lock (this)
                {
                    using (Aspose.Pdf.Document pdfDocument = new Aspose.Pdf.Document(filePath))
                    {

                        foreach (var pdfPage in pdfDocument.Pages)
                        {

                            // Aspose.Pdf.Text.TextAbsorber textAbsorber = new Aspose.Pdf.Text.TextAbsorber();
                            Aspose.Pdf.Text.TextFragmentAbsorber textFragmentAbsorber = new Aspose.Pdf.Text.TextFragmentAbsorber();
                            pdfDocument.Pages[pdfPage.Number].Accept(textFragmentAbsorber);

                            Aspose.Pdf.Text.TextFragmentCollection textFragmentCollection = textFragmentAbsorber.TextFragments;
                            foreach (Aspose.Pdf.Text.TextFragment textFragment in textFragmentCollection)
                            {
                                _sBuilder.Append(textFragment.Text).Append(' ');
                            }
                            
                            string currentText = _sBuilder.ToString();//textAbsorber.Text;
                            currentText = Encoding.UTF8.GetString(ASCIIEncoding.Convert(Encoding.Default, Encoding.UTF8, Encoding.Default.GetBytes(currentText)));
                            pageWiseContents.Add(new DocumentPage() { PageNumber = pdfPage.Number, Text = CommonUtility.CleanData(currentText) });
                            _sBuilder.Clear();
                        }

                    }
                }

            }
            return pageWiseContents;

        }
        public List<DocumentPage> GetTextFromExcel(string filePath)
        {
            List<DocumentPage> pageWiseContents = new List<DocumentPage>();
            using (SpreadsheetDocument doc = SpreadsheetDocument.Open(filePath, false))
            {
                WorkbookPart workbookPart = doc.WorkbookPart;
                int count = workbookPart.WorksheetParts.Count();
                foreach (WorksheetPart worksheetPart in workbookPart.WorksheetParts)
                {
                    StringBuilder sb = new StringBuilder();
                    Worksheet worksheet = worksheetPart.Worksheet;
                    IEnumerable<Row> rows = worksheet.GetFirstChild<SheetData>().Descendants<Row>();
                    foreach (Row row in rows)
                        foreach (Cell cell in row.Descendants<Cell>())
                            if (cell.CellValue != null)
                                sb.Append(GetCellValue(doc, cell) + " ");
                    pageWiseContents.Add(new DocumentPage() { PageNumber = count, Text = HtmlEncode(sb.ToString()) });
                    count--;
                }
            }
            return pageWiseContents;
        }
        private string GetCellValue(DocumentFormat.OpenXml.Packaging.SpreadsheetDocument doc, Cell cell)
        {
            string value = cell.CellValue.InnerText;
            if (cell.DataType != null && cell.DataType.Value == CellValues.SharedString)
            {
                return doc.WorkbookPart.SharedStringTablePart.SharedStringTable.ChildElements.GetItem(int.Parse(value)).InnerText;
            }
            return value;
        }
        private string HtmlEncode(string Text)
        {
            if (!string.IsNullOrEmpty(Text))
            {
                StringWriter encodedResult = new StringWriter();
                HttpUtility.HtmlEncode(Text, encodedResult);
                return Convert.ToString(encodedResult);
            }
            return Text;
        }

        //public static List<PageWiseContent> GetTextFromWord(string filePath)
        //{
        //    List<PageWiseContent> pageWiseContents = new List<PageWiseContent>();
        //    StringBuilder text = new StringBuilder();
        //    if (File.Exists(filePath))
        //    {
        //        using (PdfReader reader = new PdfReader(filePath))
        //        {
        //            for (int i = 1; i <= reader.NumberOfPages; i++)
        //                pageWiseContents.Add(new PageWiseContent() { PageNumber = i, Text = PdfTextExtractor.GetTextFromPage(reader, i) });
        //            reader.Close();
        //        }
        //    }
        //    return pageWiseContents;

        //}
    }
}
