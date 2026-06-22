using SearchLego.DataSeeder.Common;

namespace SearchLego.DataSeeder.Host
{
    public abstract class IDataObjectFactory
    {
        public abstract IDataSourceFactory GetDBObject(ApplicationConfig configDetail);
    }
}
