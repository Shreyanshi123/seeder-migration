using System.Data;

namespace SearchLego.DataSeeder.Host
{
    public interface IDataSourceFactory
    {
        string GetData(string Query, string DefaultValue);
        DataSet GetDataSet(string Query);
        string GetData(string query, string defaultValue,string connectionString);
    }

}
