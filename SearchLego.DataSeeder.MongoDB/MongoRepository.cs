using MongoDB.Bson;
using MongoDB.Driver;
using SearchLego.DataSeeder.Common;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace SearchLego.DataSeeder.MongoDB
{
    public abstract class MongoRepository<T> : MongoFactory<T> where T : IAggregate
    {
        protected MongoRepository(IMongoDatabase database, string collectionName)
            : base(database, collectionName)
        {
        }

        protected MongoRepository()
        {
        }
        

       
        public long Count(Expression<Func<T, bool>> filter = null)
        {
            return Find(filter).Count();
        }

        public virtual void Add(T item)
        {
            AddInternal(item);
        }

        public virtual void Update(T item)
        {
            // increment new version, and save existing version
            var currentVersion = item.Version++;

            ///TO DO: refactor

            var filter = Builders<T>.Filter.And(
                Builders<T>.Filter.Eq(x => x.Id, item.Id),
                Builders<T>.Filter.Eq(x => x.Version, currentVersion));
            try
            {
                var result = Collection.ReplaceOne(filter, item, new UpdateOptions { IsUpsert = true });
                if (result.ModifiedCount != 1)
                {
                    // the document was not modified due to version mismatch
                    // throw new ConcurrentUpdateDataAccessException(item, currentVersion);
                }
            }
            catch (MongoWriteException e)
            {
                if (IsDuplicateKeyException(e))
                {
                    // upsert failed due to version conflict
                    // throw new ConcurrentUpdateDataAccessException(item, currentVersion);
                }
                // throw new DataAccessException($"Could not upsert document of type {typeof(T)} with ID {item.Id}", e);
            }
        }


        public virtual void Update(T item, bool isSystemUpdate)
        {
            // increment new version, and save existing version
            var currentVersion = item.Version++;

            var filter = Builders<T>.Filter.And(
                Builders<T>.Filter.Eq(x => x.Id, item.Id),
                Builders<T>.Filter.Eq(x => x.Version, currentVersion));

            try
            {
                var result = Collection.ReplaceOne(filter, item, new UpdateOptions { IsUpsert = true });
                if (result.ModifiedCount != 1)
                {
                    // the document was not modified due to version mismatch
                }
            }
            catch (MongoWriteException e)
            {
                if (IsDuplicateKeyException(e))
                {
                    // upsert failed due to version conflict
                }
                // throw new DataAccessException($"Could not upsert document of type {typeof(T)} with ID {item.Id}", e);
            }
        }


        public virtual void Remove(string id)
        {
            Collection.DeleteOne(x => x.Id == id);
        }

        protected IFindFluent<T, T> Find(Expression<Func<T, bool>> filter = null)
        {
            if (filter == null)
                return Collection.Find(Builders<T>.Filter.Empty);
            return Collection.Find(filter);
        }

        public IList<T> Get(int pageNumber = 0, int pageSize = 0)
        {
            var find = Find(null);
            if (pageSize == 0 && pageSize == 0)
                return find.ToList();

            return find.Skip(pageSize * (pageNumber - 1)).Limit(pageSize).ToList();
        }

        public virtual T GetById(string id)
        {
            return Collection.Find(x => x.Id == id).SingleOrDefault();
        }

        public IList<T> GetByIds(string[] ids)
        {
            return Collection.Find(Builders<T>.Filter.In(f => f.Id, ids)).ToList();
        }
        

    }
}