using MongoDB.Bson;
using SearchLego.DataSeeder.Common;
using System.Collections.Generic;

namespace SearchLego.DataSeeder.MongoDB.Host
{
    public interface IConfigUpdateRepsitory : IRepository<BsonDocument>
    {
        IList<BsonDocument> GetAll();
    }
}
