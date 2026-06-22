using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace SearchLego.DataSeeder.Common
{
    public interface IRepository<T> : IReadOnlyRepository<T>
    {
        void Add(T item);

        void Update(T item);
        void Update(T item, bool IsSystemUpdate);

        void Remove(string id);
        
        long Count(Expression<Func<T, bool>> filter = null);
    }

    public interface IReadOnlyRepository<T>
    {
        IList<T> Get(int pageNumber = 0, int pageSize = 0);

        T GetById(string id);

        T GetByFieldValue(string field, object value);

        IList<T> GetByIds(string[] ids);

        IList<T> GetCustomerProcessedData();

        void UpdateNerEntityRuler(T item);
        Task<IList<T>> GetAllAsyn();

    }
}
