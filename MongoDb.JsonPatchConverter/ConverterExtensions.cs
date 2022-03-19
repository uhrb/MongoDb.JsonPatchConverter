using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MongoDb.JsonPatchConverter
{
    public static class ConverterExtensions
    {
        /// <summary>
        /// Scan a type for mapping information
        /// </summary>
        /// <typeparam name="T">the type to scan</typeparam>
        /// <param name="registry"></param>
        public static void MapType<T>(this IMapRegistry registry) => registry.MapType(typeof(T));

        /// <summary>
        /// Get mappings of a type
        /// </summary>
        /// <typeparam name="T">type to get mapping information</typeparam>
        /// <param name="registry"></param>
        /// <returns>A <see cref="IEnumerable{MapDescription}"/> that describes this type</returns>
        public static IEnumerable<MapDescription> GetMap<T>(this IMapRegistry registry) => registry.GetMap(typeof(T));

        /// <summary>
        /// Combine all filters with and operation
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="filters"></param>
        /// <returns>A new <see cref="FilterDefinition{TDocument}"/> that combines all specified filters</returns>
        public static FilterDefinition<T> AndAll<T>(this IEnumerable<FilterDefinition<T>> filters)
        {
            if (filters.Any())
                return filters.Aggregate((acc, val) => acc & val);
            else
                return Builders<T>.Filter.Empty;
        }

        /// <summary>
        /// Combine all filters with or operation
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="filters"></param>
        /// <returns>A new <see cref="FilterDefinition{TDocument}"/> that combines all specified filters</returns>
        public static FilterDefinition<T> OrAll<T>(this IEnumerable<FilterDefinition<T>> filters)
        {
            if (filters.Any())
                return filters.Aggregate((acc, val) => acc | val);
            else
                return Builders<T>.Filter.Empty;
        }


        /// <summary>
        /// Apply the update operations to a collection
        /// </summary>
        /// <typeparam name="T">Type model of the collection</typeparam>
        /// <param name="col"></param>
        /// <param name="conversion">The conversion result</param>
        /// <param name="additionalCriteria">Additional filter to apply, eg: match id</param>
        /// <param name="additionalOperation">Additional update operation to apply, eg: update timestamp</param>
        /// <exception cref="InvalidOperationException">Trying to apply changes to a error conversion result</exception>
        /// <returns>The result of an update operation.</returns>
        public static async Task<UpdateResult> UpdateManyAsync<T>(
           this IMongoCollection<T> col,
           ConversionResult<T> conversion,
           FilterDefinition<T> additionalCriteria,
           UpdateDefinition<T> additionalOperation)
           => await conversion.Apply(col, additionalCriteria, additionalOperation);

        /// <summary>
        /// Apply the update operations to a collection
        /// </summary>
        /// <typeparam name="T">Type model of the collection</typeparam>
        /// <param name="col"></param>
        /// <param name="conversion">The conversion result</param>
        /// <exception cref="InvalidOperationException">Trying to apply changes to a error conversion result</exception>
        /// <returns>The result of an update operation.</returns>
        public static async Task<UpdateResult> UpdateManyAsync<T>(this IMongoCollection<T> col, ConversionResult<T> conversion)
            => await conversion.Apply(col);
    }
}
