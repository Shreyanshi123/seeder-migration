using Newtonsoft.Json;
using SearchLego.DataSeeder.Common;
using SearchLego.DataSeeder.Host;
using System;
using System.Data;

namespace SearchLego.DataSeeder.PostgressSQL
{
    public class PostgressDataRepository : IDataSourceFactory
    {
        private readonly string _connectionString;

        public PostgressDataRepository(string connectionString)
        {
            _connectionString = connectionString;
        }
        public string GetData(string Query, string DefaultValue)
        {
            Query = Query.IndexOf("(") > 0 ? Query + ")" : Query;  // this is for temp solution 
            using var _postgressHelper = new PostgressHelper(_connectionString);
            DataSet objDs = _postgressHelper.ExecDataSet(Query);
            if (objDs != null && objDs.Tables.Count > 0 && objDs.Tables[0].Rows.Count > 0)
                return JsonConvert.SerializeObject(CommonUtility.ConvertDataTableFieldType(objDs.Tables[0], DefaultValue));
            return string.Empty;
        }

        public DataSet GetDataSet(string Query)
        {
            throw new NotImplementedException();
        }
        public string GetData(string query, string defaultValue, string connectionString)
        {
            query = query.IndexOf("(") > 0 ? query + ")" : query;  // this is for temp solution 
            using var _postgressHelper = new PostgressHelper(connectionString);
            DataSet objDs = _postgressHelper.ExecDataSet(query);
            if (objDs != null && objDs.Tables.Count > 0 && objDs.Tables[0].Rows.Count > 0)
                return JsonConvert.SerializeObject(CommonUtility.ConvertDataTableFieldType(objDs.Tables[0], defaultValue));
            return string.Empty;
        }
    }
}
