using Newtonsoft.Json;
using SearchLego.DataSeeder.Common;
using SearchLego.DataSeeder.Host;
using System;
using System.Data;

namespace SearchLego.DataSeeder.SQL
{
    public class SQLDataRepository : IDataSourceFactory
    {
        private readonly string _connectionString;
        public SQLDataRepository(string connectionString)
        {
            _connectionString = connectionString;
        }
        public string GetData(string Query, string DefaultValue)
        {
            try
            {
                using var _sqlHelper = new SQLHelper(_connectionString);
                DataSet objDs = _sqlHelper.ExecDataSet(Query, null);
                if (objDs != null && objDs.Tables.Count > 0 && objDs.Tables[0].Rows.Count > 0)
                    return JsonConvert.SerializeObject(CommonUtility.ConvertDataTableFieldType(objDs.Tables[0], DefaultValue));
                return string.Empty;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
        public DataSet GetDataSet(string Query)
        {
            try
            {
                using var _sqlHelper = new SQLHelper(_connectionString);
                DataSet objDs = _sqlHelper.ExecDataSet(Query, null);
                return objDs;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public string GetData(string query, string defaultValue, string connectionString)
        {
            try
            {
                using var _sqlHelper = new SQLHelper(connectionString);
                DataSet objDs = _sqlHelper.ExecDataSet(query, null);
                if (objDs != null && objDs.Tables.Count > 0 && objDs.Tables[0].Rows.Count > 0)
                    return JsonConvert.SerializeObject(CommonUtility.ConvertDataTableFieldType(objDs.Tables[0], defaultValue));
                return string.Empty;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

    }
}
