using MongoDB.Driver;
using SearchLego.DataSeeder.Common;
using System;

namespace SearchLego.DataSeeder.MongoDB
{
    public abstract class MongoFactory<T> where T : IAggregate
    {
        protected IMongoCollection<T> Collection;

        protected MongoFactory(IMongoDatabase database, string collectionName)
        {
            Collection = database.GetCollection<T>(collectionName);
        }

        protected MongoFactory()
        {
        }

        protected virtual void AddInternal(T item)
        {
            // set initial version
            item.Version = 1;
            item.Id = item.Id == null ? Guid.NewGuid().ToString() : item.Id;
            try
            {
                Collection.InsertOne(item);
            }
            catch (MongoWriteException e)
            {
                if (IsDuplicateKeyException(e))
                {
                    // upsert failed due to version conflict
                    // throw new DuplicateKeyDataAccessException(item, e);
                }
                //  throw new DataAccessException($"Could not add document of type {typeof(T)} with ID {item.Id}", e);
            }
        }

        protected virtual void RemoveInternal(T item)
        {
            Collection.DeleteOne(x => x.Id == item.Id);
        }

        protected static bool IsDuplicateKeyException(MongoWriteException e)
        {
            return e.WriteError.Category == ServerErrorCategory.DuplicateKey && e.WriteError.Code == 11000;
        }
    }
}
