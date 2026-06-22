using SearchLego.DataSeeder.Entities;

namespace SearchLego.DataSeeder.Elastic
{
    public interface IModelIngestService
    {
        ModelConfig GenerateModelFile(object data, string baseApi, string url);
    }
}
