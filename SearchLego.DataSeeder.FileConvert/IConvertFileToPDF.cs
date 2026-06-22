namespace SearchLego.DataSeeder.FileConvert
{
    public interface IConvertFileToPDF
    {
        bool ConvertToPDF(string arguments, string libreOfficePath);

        bool ConvertToPDFAspose(string sourceFile, string destPath, string conversionType);
    }
}