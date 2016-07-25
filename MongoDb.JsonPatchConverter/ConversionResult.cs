using System.Collections.Generic;
using System.Linq;
using MongoDB.Driver;

namespace MongoDb.JsonPatchConverter
{
    public class ConversionResult<TOut>
    {
        public ConversionResult()
        {
            Updates = new List<UpdateDefinition<TOut>>();
            Filters = new List<FilterDefinition<TOut>>();
            Errors = new List<OperationError>();
        }

        public ICollection<UpdateDefinition<TOut>> Updates { get; }
        public ICollection<FilterDefinition<TOut>> Filters { get; }
        public ICollection<OperationError> Errors { get; }
        public bool HasErrors => Errors.Any();
    }
}