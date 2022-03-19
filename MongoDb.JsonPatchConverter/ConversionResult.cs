using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Driver;

namespace MongoDb.JsonPatchConverter
{
    /// <summary>
    /// This object contain various definitions for updating using MongDb Driver, as well as errors occured during the conversion process
    /// </summary>
    /// <typeparam name="TOut"></typeparam>
    public class ConversionResult<TOut>
    {
        public ConversionResult()
        {
            Updates = new List<UpdateDefinition<TOut>>();
            Filters = new List<FilterDefinition<TOut>>();
            Errors = new List<OperationError>();
        }

        /// <summary>
        /// A collection of update definitions
        /// </summary>
        public ICollection<UpdateDefinition<TOut>> Updates { get; }

        /// <summary>
        /// A collection of filter definitions
        /// </summary>
        public ICollection<FilterDefinition<TOut>> Filters { get; }

        /// <summary>
        /// A collection of error during the conversion process
        /// </summary>
        public ICollection<OperationError> Errors { get; }

        /// <summary>
        /// Determine whether the conversion process has any error
        /// </summary>
        public bool HasErrors => Errors.Any();

        /// <summary>
        /// Apply the update operations to a collection
        /// </summary>
        /// <param name="collection">Collection to apply the update</param>
        /// <param name="additionalCriteria">Additional filter to apply, eg: match id</param>
        /// <param name="additionalOperation">Additional update operation to apply, eg: update timestamp</param>
        /// <exception cref="InvalidOperationException">Trying to apply changes to a error conversion result</exception>
        /// <returns>The result of an update operation.</returns>
        public async Task<UpdateResult> Apply(
            IMongoCollection<TOut> collection, 
            FilterDefinition<TOut> additionalCriteria,
            UpdateDefinition<TOut> additionalOperation)
        {
            if (HasErrors)
                throw new InvalidOperationException("Cannot apply changes to an error conversion result!");

            var filters = new List<FilterDefinition<TOut>>(Filters) { additionalCriteria };
            var updates = new List<UpdateDefinition<TOut>>(Updates) { additionalOperation };

            var finalFilter = filters.AndAll();
            var finalUpdate = Builders<TOut>.Update.Combine(updates);

            return await collection.UpdateManyAsync(finalFilter, finalUpdate);
        }

        /// <summary>
        /// Apply the update operations to a collection
        /// </summary>
        /// <param name="collection">Collection to apply the update</param>
        /// <exception cref="InvalidOperationException">Trying to apply changes to a error conversion result</exception>
        /// <returns>The result of an update operation.</returns>
        public async Task<UpdateResult> Apply(IMongoCollection<TOut> collection)
        {
            if (HasErrors)
                throw new InvalidOperationException("Cannot apply changes to an error conversion result!");

            var finalFilter = Filters.AndAll();
            var finalUpdate = Builders<TOut>.Update.Combine(Updates);

            return await collection.UpdateManyAsync(finalFilter, finalUpdate);
        }
    }
}