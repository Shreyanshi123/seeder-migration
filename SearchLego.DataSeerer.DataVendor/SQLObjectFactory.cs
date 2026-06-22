using SearchLego.DataSeeder.Host;
using SearchLego.DataSeeder.SQL;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace SearchLego.DataSeerer.DataVendor
{
   public class SQLObjectFactory : IDataSourceFactory
    {
        public string GetData(string Query, string DefaultValue)
        {
            throw new NotImplementedException();
        }

        public DataSet GetDataSet(string Query)
        {
            throw new NotImplementedException();
        }

        public override IDataSourceFactory GetDBObject(string ConnectionString)
        {
            return new SQLDataRepository(ConnectionString);
        }
    }
}
