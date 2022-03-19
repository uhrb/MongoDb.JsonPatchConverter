using System;
using System.Collections.Generic;

namespace MongoDb.JsonPatchConverter
{
    /// <summary>
    /// Contains information about types to aid with converting
    /// </summary>
    public interface IMapRegistry
    {
        /// <summary>
        /// Scan a type for mapping information
        /// </summary>
        /// <param name="type">the type to scan</param>
        void MapType(Type type);

        /// <summary>
        /// Get mappings of a type
        /// </summary>
        /// <param name="t">type to get mapping information</param>
        /// <returns>A <see cref="IEnumerable{MapDescription}"/> that describes this type</returns>
        IEnumerable<MapDescription> GetMap(Type t);
    }
}