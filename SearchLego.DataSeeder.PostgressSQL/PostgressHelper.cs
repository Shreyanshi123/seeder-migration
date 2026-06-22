using Npgsql;
using System;
using System.Data;

namespace SearchLego.DataSeeder.PostgressSQL
{

    public sealed class PostgressHelper : IDisposable
    {
        // Internal members
        private string _connString = null;
        private NpgsqlConnection _conn = null;
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
        public PostgressHelper(string connString)
        {
            _connString = connString;
            Connect();
        }

        // Creates a SqlConnection using the current connection string
        private void Connect()
        {
            _conn = new NpgsqlConnection(_connString);
            _conn.Open();
        }


        #region Exec Members


        /// <summary>
        /// Executes a query and returns the results as a DataSet
        /// </summary>
        /// <param name="qry">Query text</param>
        /// <param name="args">Any number of parameter name/value pairs and/or SQLParameter arguments</param>
        /// <returns>Results as a DataSet</returns>
        public DataSet ExecDataSet(string qry)
        {
            using var cmd = new NpgsqlDataAdapter(qry, _connString);
            DataSet dbset = new DataSet();
            cmd.Fill(dbset);
            return dbset;
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
                        _conn.Close();
                        _conn.Dispose();
                        _conn = null;
                    }
                }
                _disposed = true;
            }
        }

        #endregion
    }

}
