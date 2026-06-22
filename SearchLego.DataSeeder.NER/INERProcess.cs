namespace SearchLego.DataSeeder.NER
{
    public interface INERProcess
    {
        string NERDataProcess(object data, string baseUrl, string url);
    }
}