using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SearchLego.DataSeeder.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SearchLego.DataSeeder.FileConvert
{
    public class ExcelBasedTagging
    {
        private readonly ILogger<dynamic> _logger;
        public ExcelBasedTagging(ILogger<dynamic> logger)
        {
            _logger = logger;
        }
        public DataTable GetTgasFromExcelFile(string filePath, Stream stream)
        {
            DataTable table = new DataTable();
            try
            {
                if (!string.IsNullOrEmpty(filePath) && stream != null)
                {
                    using (SpreadsheetDocument spreadSheetDocument = SpreadsheetDocument.Open(stream, false))
                    {

                        //setting up our workbook using OpenXml
                        WorkbookPart workbookPart = spreadSheetDocument.WorkbookPart!;
                        IEnumerable<Sheet> sheets = workbookPart.Workbook.GetFirstChild<Sheets>()!.Elements<Sheet>();
                        string workbookSheetId = sheets.First().Id!.Value!;
                        WorksheetPart worksheetPart = (WorksheetPart)workbookPart.GetPartById(workbookSheetId!);
                        Worksheet workSheet = worksheetPart.Worksheet;
                        SheetData sheetData = workSheet.GetFirstChild<SheetData>()!;
                        IEnumerable<Row> rows = sheetData.Descendants<Row>();

                        if (rows.Count() > 0) //check for null excel file
                        {

                            //for creating column headers in our datatable
                            foreach (Cell cell in rows.ElementAt(0))
                            {
                                table.Columns.Add(GetCellValue(workbookPart, cell));
                            }


                            //this will also include the header row which will later be romoved
                            foreach (Row row in rows)
                            {
                                DataRow dataRow = table.NewRow();
                                for (int i = 0; i < row.Descendants<Cell>().Count(); i++)
                                {
                                    Cell cell = row.Descendants<Cell>().ElementAt(i);
                                    int actualCellIndex = CellReferenceToIndex(cell);
                                    dataRow[actualCellIndex] = GetCellValue(workbookPart, cell);
                                }
                                table.Rows.Add(dataRow);
                            }
                        }
                        else
                        {
                            table = null;
                        }

                    }
                    table.Rows.RemoveAt(0);
                }

            }
            catch (Exception ex)
            {
                _logger.LogError($"invalid operation - File_Path or File_Name  : {filePath}  Excel based tagging exception, {ex.Message}, {ex.StackTrace}");
            }
            return table;
        }

        /// <summary>
        /// this function is use for get tag MetaData
        /// </summary>
        /// <param name="filePath">relative file path to find the file name into file</param>
        /// <param name="excelTagFilePath">excel sheet file path/name for tagging</param>
        /// <param name="stream">Excel file read from cloud and return stream for extract the data</param>
        /// <returns></returns>
        public object GetTagMetaData(string filePath, string excelTagFilePath, Stream stream)
        {
            //string excelFilePath = @"C:\PdfDoc\DocumentTag\DocumentTagging.xlsx";
            var objectData = ReadDynamicTagColumns(filePath, excelTagFilePath, stream);
            return objectData;
        }
        private Dictionary<string, object> ReadDynamicTagColumns(string filePathName, string excelTagFilePath, Stream stream)
        {
            var table = GetTgasFromExcelFile(excelTagFilePath, stream);
            if (table != null)
            {
                var tableData = table.Rows.OfType<DataRow>()
                                .Select(row => table.Columns.OfType<DataColumn>()
                                    .ToDictionary(col => col.ColumnName, c => row[c]));

                foreach (var rowData in tableData)
                {
                    var obj = new Dictionary<string, object>();

                    if (rowData.TryGetValue("File_Path", out var value) && rowData.TryGetValue("File_Name", out var fileName))
                    {
                        string rowDatafilePathName = value!.ToString();

                        if (validateFile(filePathName, rowDatafilePathName))
                        {
                            var filterData = rowData.Where(rd => !rd.Key.Contains("File_Path") && !rd.Key.Contains("File_Name"))
                                                    .Select(k => new { k.Key, k.Value }).ToList();

                            foreach (var f in filterData)
                            {
                                var splitedValue = f.Value.ToString().Split(",");
                                if (ExcelUtility.CheckDate(f.Value!.ToString()) || ExcelUtility.CheckNumber(f.Value!.ToString()))
                                {
                                    obj[f.Key] = new
                                    {
                                        key = f.Value,
                                        val = f.Value
                                    };
                                }
                                else
                                {
                                    obj[f.Key] = splitedValue;
                                }
                            }
                            return obj;
                        }
                        else
                        {
                            _logger.LogInformation($"# File_Path or File_Name :{filePathName} is not matched with excel File_Path row data: {rowDatafilePathName}.");
                        }
                    }
                    else
                    {
                        _logger.LogError($"# File_Path or File_Name column is not available or valid in excel sheet template.");
                        //obj["ErrorMessage"] = "File_Path column is not availabel or valid in excel sheet template.";
                    }

                }
            }
            return null;
        }

        private bool validateFile(string filePathName, string rowDatafilePathName)
        {
            bool isValidFile = false;
            if (!string.IsNullOrEmpty(rowDatafilePathName) && !string.IsNullOrEmpty(filePathName))
            {

                if (rowDatafilePathName.ToLower().Trim().Equals(filePathName.ToLower().Trim()))
                {
                    isValidFile = true;
                }
                else if (Path.GetFileName(rowDatafilePathName).ToLower().Trim().Equals(Path.GetFileName(filePathName).ToLower().Trim()))
                    isValidFile = true;
            }

            return isValidFile;
        }


        private string GetCellValue(WorkbookPart workbookPart, Cell cell)
        {
            SharedStringTablePart sharedStringTablePart = workbookPart.GetPartsOfType<SharedStringTablePart>().First();
            SharedStringTable sharedStringTable = sharedStringTablePart.SharedStringTable;

            if (cell.CellValue == null)
                return String.Empty;

            string value = cell.CellValue.InnerXml;
            if ((cell.DataType != null) && (cell.DataType == CellValues.SharedString) && (cell.CellValue != null))
            {
                return sharedStringTable.ChildElements[int.Parse(value)].InnerText;
            }
            else
            {
                if (cell.StyleIndex != null)
                {
                    var cellFormat = workbookPart.WorkbookStylesPart.Stylesheet.CellFormats.ChildElements[int.Parse(cell.StyleIndex.InnerText)] as CellFormat;
                    if (cellFormat != null)
                    {
                        var dateFormat = ExcelUtility.GetDateTimeFormat(cellFormat.NumberFormatId);
                        if (!string.IsNullOrEmpty(dateFormat))
                            return DateTime.FromOADate(double.Parse(value)).ToString("dd/MM/yyyy");
                    }
                }
                return value;
            }
        }
        private int CellReferenceToIndex(Cell cell)
        {
            int index = 0;
            string reference = cell.CellReference!.ToString()!.ToUpper();
            foreach (char ch in reference)
            {
                if (Char.IsLetter(ch))
                {
                    int value = (int)ch - 64; //in excel indexing starts from 1 rather than 0, hence direct difference
                    index = (index == 0) ? value : ((index) * 26) + value;
                }
                else
                {
                    return index - 1;
                }
            }
            return index - 1;
        }
    }
}
