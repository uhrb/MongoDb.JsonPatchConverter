using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.JsonPatch.Operations;
using MongoDB.Driver;
using MongoDB.Bson.Serialization;
using Newtonsoft.Json;

namespace MongoDb.JsonPatchConverter
{
    /// <summary>
    /// Convert <see cref="JsonPatchDocument"/> to MongoDB update query
    /// </summary>
    public class JsonPatchConverter : IJsonPatchConverter
    {
        private const string OperationNotSupportedFormat = "Operation '{0}' is not supported.";
        private const string NotSupportedByMongo =
            "Remove array element by index is not supported https://jira.mongodb.org/browse/SERVER-1014";
        private const string ConversionErrorFormat = "Cannot convert value for property at {0}";
        private const string NoTypeMapFormat = "Type {0} has no registered mappings";
        private static readonly char[] BadSymbols = { '$', '{', '}', '[', ']' };
        private const string PathNotFoundFormat = "Operation '{0}' points to path '{1}' , which is not found on models.";
        private const string FiledMismatchOnTypes = "Model {0} does not have any field, which exist in {1}.";
        private const string OperationUpdateDefinitionTypeName = "MongoDB.Driver.OperatorUpdateDefinition`2";

        private static readonly Type OperationUpdateDefinitionType;
        private static readonly Type GenericStringFieldDefinition;

        static JsonPatchConverter()
        {
            OperationUpdateDefinitionType = typeof(UpdateDefinition<>).Assembly.GetType(OperationUpdateDefinitionTypeName, true);
            GenericStringFieldDefinition = typeof(StringFieldDefinition<int, int>).GetGenericTypeDefinition();
        }

        public IMapRegistry MapRegistry { get; }
        public JsonSerializerSettings SerializerSettings { get; } = new JsonSerializerSettings();
        public Action<BsonDeserializationContext.Builder> DeserializationConfig { get; } = x => { };

        /// <summary>
        /// Instanciate <see cref="JsonPatchConverter"/> with custom serializer and deserializer settings
        /// </summary>
        public JsonPatchConverter(
            IMapRegistry mapRegistry, 
            JsonSerializerSettings serializerSettings,
            Action<BsonDeserializationContext.Builder> deserializationConfig)
        {
            MapRegistry = mapRegistry;
            SerializerSettings = serializerSettings;
            DeserializationConfig = deserializationConfig;
        }

        /// <summary>
        /// Instanciate <see cref="JsonPatchConverter"/>
        /// </summary>
        /// <param name="mapRegistry"></param>
        public JsonPatchConverter(IMapRegistry mapRegistry)
        {
            MapRegistry = mapRegistry;
        }

        /// <summary>
        /// Instanciate <see cref="JsonPatchConverter"/> with default <see cref="MongoDb.JsonPatchConverter.MapRegistry"/>
        /// </summary>
        public JsonPatchConverter() : this(new MapRegistry()) { }


        /// <summary>
        /// Convert <see cref="JsonPatchDocument{TModel}"/> to MongoDb update query
        /// </summary>
        /// <typeparam name="TModel"> the json patch model </typeparam>
        /// <param name="document"> the json patch document </param>
        /// <returns> A object contains various definitions to update using MongoDb Driver </returns>
        /// <exception cref="InvalidOperationException">  </exception>
        public ConversionResult<TModel> Convert<TModel>(JsonPatchDocument<TModel> document) where TModel : class
            => Convert<TModel, TModel>(document);

        /// <summary>
        /// Convert <see cref="JsonPatchDocument{TModel}"/> to MongoDb update query, with different model for input and output
        /// </summary>
        /// <typeparam name="TOut"> the output model, usually the one used in your database </typeparam>
        /// <typeparam name="TModel"> the json patch model, eg: a DTO or a projection </typeparam>
        /// <param name="document"> the json patch document </param>
        /// <returns> A object contains various definitions to update using MongoDb Driver </returns>
        /// <exception cref="InvalidOperationException">  </exception>
        public ConversionResult<TOut> Convert<TOut, TModel>(JsonPatchDocument<TModel> document) where TModel : class
        {
            var modelMaps = MapsOrThrow(typeof(TModel));
            var outMaps = MapsOrThrow(typeof(TOut));
            var interSection = outMaps.Intersect(modelMaps).ToArray();
            if (interSection.Length == 0)
            {
                throw new InvalidOperationException(string.Format(FiledMismatchOnTypes, typeof(TModel), typeof(TOut)));
            }
            var result = new ConversionResult<TOut>();
            foreach (var op in document.Operations)
            {
                var matched = interSection.FirstOrDefault(_ => _.Regex.IsMatch(op.path));
                if (ContainsBadCharacters(op.path) || matched == null)
                {
                    result.Errors.Add(new OperationError(string.Format(PathNotFoundFormat,op.op,op.path), OperationErrorType.PathNotValid, op));
                    continue;
                }
                var path = op.path.Substring(1).Replace('/', '.');
                switch (op.OperationType)
                {
                    case OperationType.Add:
                        HandleAdd(op, matched, path, result, SerializerSettings, DeserializationConfig);
                        break;
                    case OperationType.Copy:
                    case OperationType.Move:
                    case OperationType.Test:
                        result.Errors.Add(new OperationError(string.Format(OperationNotSupportedFormat, op.op), OperationErrorType.NotSupported, op));
                        break;
                    case OperationType.Remove:
                        HandleRemove(op, matched, path, result);
                        break;
                    case OperationType.Replace:
                        HandleReplace(op, matched, path, result, SerializerSettings, DeserializationConfig);
                        break;

                }
            }

            return result;
        }

        private static void HandleReplace<TOut>(
            Operation op,
            MapDescription map,
            string path,
            ConversionResult<TOut> conversion,
            JsonSerializerSettings serializerSettings,
            Action<BsonDeserializationContext.Builder> deserializationConfig)
        {
            var val = op.value;
            try
            {
                val = ConvertType(map, val, serializerSettings, deserializationConfig);
            }
            catch
            {
                conversion.Errors.Add(new OperationError(string.Format(ConversionErrorFormat,path), OperationErrorType.TypeError, op));
                return;
            }
            var filter = Builders<TOut>.Filter.Exists(new StringFieldDefinition<TOut>(path));
            var update = ConstructTypedSet<TOut>(path, map, val);
            conversion.Filters.Add(filter);
            conversion.Updates.Add(update);
        }

        private static void HandleRemove<TOut>(
            Operation op,
            MapDescription map,
            string path,
            ConversionResult<TOut> conversion)
        {
            if (map.IsIndexer)
            {
                conversion.Errors.Add(new OperationError(NotSupportedByMongo, OperationErrorType.NotSupported, op));
                return;
            }
            var filter  = Builders<TOut>.Filter.Exists(new StringFieldDefinition<TOut>(path));
            conversion.Filters.Add(filter);
            conversion.Updates.Add(Builders<TOut>.Update.Unset(path));
        }

        private static void HandleAdd<TOut>(
            Operation operation, 
            MapDescription map, 
            string path,
            ConversionResult<TOut> conversion,
            JsonSerializerSettings serializerSettings,
            Action<BsonDeserializationContext.Builder> deserializationConfig)
        {
            var lastDot = path.LastIndexOf(".", StringComparison.InvariantCulture);
            if (lastDot >0)
            {
                // target location may not exists, however, only last property may not exists, but all previous should
                var existance = path.Substring(0, lastDot);
                conversion.Filters.Add(Builders<TOut>.Filter.Exists(existance));
            }

            object val;
            try
            {
                val = ConvertType(map, operation.value, serializerSettings, deserializationConfig);
            }
            catch
            {
                conversion.Errors.Add(new OperationError(string.Format(ConversionErrorFormat,path), OperationErrorType.TypeError, operation));
                return;
            }
            var update = ConstructTypedSet<TOut>(path, map, val);
            conversion.Updates.Add(update);
        }

        private IEnumerable<MapDescription> MapsOrThrow(Type t)
        {
            var maps = MapRegistry.GetMap(t).ToArray();

            if (maps.Length == 0)
            {
                throw new InvalidOperationException(string.Format(NoTypeMapFormat, t));
            }
            return maps;
        }

        private static object ConvertType(
            MapDescription map, 
            object value, 
            JsonSerializerSettings serializerSettings,
            Action<BsonDeserializationContext.Builder> deserializationConfig)
        {
            // Actually, i wanted to implement something cool, but after i saw 
            // https://github.com/aspnet/JsonPatch/blob/98e2d5d4c729770e5e8e146602ab2b6c5bdc439a/src/Microsoft.AspNetCore.JsonPatch/Adapters/ObjectAdapter.cs#L1012
            // i decided, that everything is ok :)
            var serialized = JsonConvert.SerializeObject(value, serializerSettings);
            return BsonSerializer.Deserialize(serialized, map.Type, deserializationConfig);
        }

        private static bool ContainsBadCharacters(string value)
        {
            return value.IndexOfAny(BadSymbols) >= 0;
        }

      
        private static UpdateDefinition<TOut> ConstructTypedSet<TOut>(string path, MapDescription map, object value)
        {
            // TODO add caching
            var genericUpdate = OperationUpdateDefinitionType.MakeGenericType(typeof(TOut), map.Type);
            var genericField = GenericStringFieldDefinition.MakeGenericType(typeof(TOut), map.Type);
            var fieldDefinition = Activator.CreateInstance(genericField, path, null);
            //public OperatorUpdateDefinition(string operatorName, FieldDefinition<TDocument, TField> field, TField value)
            var updateDefinition = Activator.CreateInstance(genericUpdate, "$set", fieldDefinition, value);

            return (UpdateDefinition<TOut>)updateDefinition;
        }
    }
}