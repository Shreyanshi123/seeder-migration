using System.Collections.Generic;

namespace SearchLego.DataSeeder.Common
{
    public interface IHierarchyRepository<TBaseType> : IRepository<TBaseType>
    {
        TDestinationType GetById<TDestinationType>(string id) where TDestinationType : TBaseType, IAggregate;

        IList<TDestinationType> GetByIds<TDestinationType>(string[] ids) where TDestinationType : TBaseType, IAggregate;
    }
}
