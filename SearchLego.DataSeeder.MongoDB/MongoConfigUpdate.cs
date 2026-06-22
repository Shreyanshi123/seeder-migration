//using MongoDB.Driver.Builders;
using MongoDB.Bson;
using MongoDB.Driver;
using SearchLego.DataSeeder.Common;
using SearchLego.DataSeeder.Host;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace SearchLego.DataSeeder.MongoDB
{
    public class MongoConfigUpdate : IRepository<BsonDocument>, IMongoConfigFactory
    {
        protected IMongoCollection<BsonDocument> Collection;
        public MongoConfigUpdate(IMongoDatabase database, string collectionName)
        {
            Collection = database.GetCollection<BsonDocument>(collectionName);
        }

        public void Add(BsonDocument item)
        {
            Collection.InsertOne(item);
        }
        public IList<BsonDocument> GetAll()
        {
            return Find().ToList();
        }

        public long Count(Expression<Func<BsonDocument, bool>> filter = null)
        {
            throw new NotImplementedException();
        }

        public IList<BsonDocument> Get(int pageNumber = 0, int pageSize = 0)
        {
            throw new NotImplementedException();
        }
        public BsonDocument GetById(string id)
        {
            var query = Builders<BsonDocument>.Filter.Eq("_id", id);

            return Collection.Find(query).FirstOrDefault();
        }

        public BsonDocument GetByFieldValue(string field, object value)
        {
            var query = Builders<BsonDocument>.Filter.Eq(field, value);
            return Collection.Find(query).FirstOrDefault();
        }

        public IList<BsonDocument> GetByIds(string[] ids)
        {
            var query = Builders<BsonDocument>.Filter.In("_id", ids);
            return Collection.Find(query).ToList();
        }

        public void Remove(string id)
        {
            var query = Builders<BsonDocument>.Filter.Eq("_id", id);
            Collection.DeleteOne(query);
        }
        public IFindFluent<BsonDocument, BsonDocument> Find()
        {
            FilterDefinition<BsonDocument> filter = FilterDefinition<BsonDocument>.Empty;

            if (filter == null)
                return Collection.Find(filter);
            return Collection.Find(filter);
        }
        public void Update(BsonDocument item)
        {
            var builder = Builders<BsonDocument>.Filter;
            var filter = builder.Eq("_id", item.GetElement("_id").Value.ToString());
            var result = Collection.ReplaceOne(filter, item, new UpdateOptions() { IsUpsert = true });
        }

        public void Update(BsonDocument item, bool IsSystemUpdate)
        {
            throw new NotImplementedException();
        }

        public IList<BsonDocument> GetCustomerProcessedData()
        {
            var query = Builders<BsonDocument>.Filter.Eq("isCustomNerProcessed", false);

            return Collection.Find(query).ToList();
        }

        public void UpdateNerEntityRuler(BsonDocument item)
        {
            var builder = Builders<BsonDocument>.Filter;
            var filter = builder.Eq("_id", ObjectId.Parse(item.GetElement("_id").Value.ToString()));
            var result = Collection.ReplaceOne(filter, item);
        }
        public async Task<IList<BsonDocument>> GetAllAsyn()
        {
            return await Collection.AsQueryable().ToListAsync();
        }
    }
}
