using SearchLego.DataSeeder.Host;
using System;
using System.Data;

namespace SearchLego.DataSeeder.MongoDB
{
    public class MongoDataRepository : IDataSourceFactory
    {
        private readonly string _connectionString;

        public MongoDataRepository(string connectionString)
        {
            _connectionString = connectionString;
        }
        public string GetData(string Query, string DefaultValue)
        {
            //Query = Query.IndexOf("(") > 0 ? Query + ")" : Query;  // this is for temp solution 
            using var m_ = new MongoHelper(_connectionString);
            string json = m_.ExecDataSet(Query);
            return json;
        }

        public DataSet GetDataSet(string Query)
        {
            throw new NotImplementedException();
        }
        public string GetData(string query, string defaultValue,string connectionString)
        {
            connectionString = !string.IsNullOrEmpty(connectionString) ? connectionString : _connectionString;
            using var m_ = new MongoHelper(connectionString);
            string json = m_.ExecDataSet(query);
            return json;
        }
    }
}





