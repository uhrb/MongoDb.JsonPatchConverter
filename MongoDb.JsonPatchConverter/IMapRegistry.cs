using System;
using System.Collections.Generic;

namespace MongoDb.JsonPatchConverter
{
    public interface IMapRegistry
    {
        void MapType<T>() where T : class;
        IEnumerable<MapDescription> GetMap(Type t);
    }
}