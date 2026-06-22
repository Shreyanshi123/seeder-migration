using System;
using MongoDB.Driver;
using System.Linq;
using MongoDB.Bson;
using Newtonsoft.Json;
using System.Text.RegularExpressions;

namespace SearchLego.DataSeeder.MongoDB
{
    public sealed class MongoHelper : IDisposable
    {
        // Internal members
        private string _connString = null;
        private MongoClient _conn = null;
        private bool _disposed = false;

        /// <summary>
        /// Sets or returns the connection string use by all instances of this class.
        /// </summary>

        /// <summary>
        /// Returns the current SqlTransaction object or null if no transaction
        /// is in effect.
        /// </summary>


        /// <summary>
        /// Constructure using connection string override
        /// </summary>
        /// <param name="connString">Connection string for this instance</param>
        public MongoHelper(string connString)
        {
            _connString = connString;
            Connect();
        }

        // Creates a SqlConnection using the current connection string
        private void Connect()
        {
            _conn = new MongoClient(_connString);
        }


        #region Exec Members


        /// <summary>
        /// Executes a query and returns the results as a DataSet
        /// </summary>
        /// <param name="qry">Query text</param>
        /// <param name="args">Any number of parameter name/value pairs and/or SQLParameter arguments</param>
        /// <returns>Results as a DataSet</returns>
        public string ExecDataSet(string qry)
        {
          

            var dbName = MongoUrl.Create(_connString).DatabaseName;

            var pattern = @"('[^'\\]*(?:\\.[^'\\]*)*'|<=|>=|!=|=|>|<|\)|\(|\s+)";
            var tokens = Regex.Split(qry.Replace(',', ' '), pattern).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x=>x.Replace('\'', ' ').Trim()).ToList();
            if (tokens.Count != 3)
            {
                return "";
            }

            //string query = qry.Substring(qry.IndexOf('\'') + 1);
            var collectionName = tokens[0];
            string lastUpdated4 = tokens[1];
            var clientId = tokens[2];
            var db = _conn.GetDatabase(dbName);
            var mongodata = new MongoData(db);
            DateTime lastUpdated;
            var isValidDateTime = DateTime.TryParse(lastUpdated4, out lastUpdated);
            var result = mongodata.FetchData(lastUpdated, clientId, collectionName);
            return result;
        }


        #endregion

        #region Transaction Members

        /// <summary>
        /// Begins a transaction
        /// </summary>
        /// <returns>The new SqlTransaction object</returns>
        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                // Need to dispose managed resources if being called manually
                if (disposing)
                {
                    if (_conn != null)
                    {
                        //Rollback();
                        //_conn.
                        //_conn.Dispose();
                        _conn = null;
                    }
                }
                _disposed = true;
            }
        }

        #endregion
    }
}