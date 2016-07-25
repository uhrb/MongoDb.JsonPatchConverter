using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.JsonPatch.Operations;
using MongoDB.Driver;
using MongoDB.Bson.Serialization;
using Newtonsoft.Json;

namespace MongoDb.JsonPatchConverter
{
    public class JsonPatchConverter
    {
        private const string OperationNotSupportedFormat = "Operation '{0}' is not supported.";
        private const string NotSupportedByMongo =
            "Remove array element by index is not supported https://jira.mongodb.org/browse/SERVER-1014";
        private const string ConversionErrorFormat = "Cannot convert value for property at {0}";
        private const string NoTypeMapFormat = "Type {0} has no registered mappings";
        private static readonly char[] BadSymbols = {'$', '{', '}', '[', ']'};
        private const string PathNotFoundFormat = "Operation '{0}' points to path '{1}' , which is not found on models.";
        private const string FiledMismatchOnTypes = "Model {0} does not have any field, which exist in {1}.";
        private const string OperationUpdateDefinitionTypeName = "MongoDB.Driver.OperatorUpdateDefinition`2";
        private const string StringMappingNotAllowed = "String mapping is not allowed";

        private static readonly Type OperationUpdateDefinitionType;
        private static readonly Type GenericStringFieldDefinition;
        private readonly ConcurrentDictionary<Type, MapDescription[]> _dictionary;

        static JsonPatchConverter()
        {
            OperationUpdateDefinitionType = typeof(UpdateDefinition<>).Assembly.GetType(OperationUpdateDefinitionTypeName, true);
            GenericStringFieldDefinition = typeof(StringFieldDefinition<int, int>).GetGenericTypeDefinition();
        }
        public JsonPatchConverter()
        {
            _dictionary = new ConcurrentDictionary<Type, MapDescription[]>();
        }

        public void MapType<T>() where T : class
        {
            var type = typeof(T);
            if (type == typeof(string))
            {
                throw new InvalidOperationException(StringMappingNotAllowed);
            }
            var valueFactory =
                new Func<Type, MapDescription[]>(
                    a => a.GetProperties()
                        .SelectMany(_ => CreateTypeMappings(null, false, _.Name, _.PropertyType, new string[] { }))
                        .ToArray());
            _dictionary.AddOrUpdate(type, valueFactory, (a, b) => b);
        }

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
                        HandleAdd(op, matched, path, result);
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
                        HandleReplace(op, matched, path, result);
                        break;

                }
            }

            return result;
        }

        private static void HandleReplace<TOut>(
            Operation op,
            MapDescription map,
            string path,
            ConversionResult<TOut> conversion)
        {
            var val = op.value;
            try
            {
                val = ConvertType(map, val);
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
            ConversionResult<TOut> conversion)
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
                val = ConvertType(map, operation.value);
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
            MapDescription[] modelMaps;
            if (false == _dictionary.TryGetValue(t, out modelMaps))
            {
                throw new InvalidOperationException(string.Format(NoTypeMapFormat, t));
            }
            return modelMaps;
        }

        private static object ConvertType(MapDescription map, object value)
        {
            // Actually, i wanted to implement something cool, but after i saw 
            // https://github.com/aspnet/JsonPatch/blob/98e2d5d4c729770e5e8e146602ab2b6c5bdc439a/src/Microsoft.AspNetCore.JsonPatch/Adapters/ObjectAdapter.cs#L1012
            // i decided, that everything is ok :)
            var serialized = JsonConvert.SerializeObject(value);
            return BsonSerializer.Deserialize(serialized, map.Type);
        }

        private static bool ContainsBadCharacters(string value)
        {
            return value.IndexOfAny(BadSymbols) >= 0;
        }

        private static IEnumerable<MapDescription> CreateTypeMappings(string previosRoot, bool isIndexer, string name, Type t, string[] arraySegments)
        {
            var root = string.IsNullOrEmpty(name) ? previosRoot : $"{previosRoot}/{name}";
            var lst = new List<MapDescription>();
            if (false == string.IsNullOrEmpty(root))
            {
                lst.Add(new MapDescription(new Regex($"^{root}$", RegexOptions.Compiled | RegexOptions.CultureInvariant), isIndexer, t));
            }
            if (t.IsValueType)
            {
                return lst;
            }
            if (t == typeof(string))
            {
                return lst;
            }
            if (t.IsArray)
            {
                var arrayRoot = root + "/[0-9]+";
                var elementType = t.GetElementType();
                var newSegments = new string[arraySegments.Length + 1];
                arraySegments.CopyTo(newSegments, 0);
                newSegments[newSegments.Length - 1] = root;
                lst.AddRange(CreateTypeMappings(arrayRoot, true, string.Empty, elementType, arraySegments));
            }
            else
            {
                var props = t.GetProperties();
                var mapped = props.SelectMany(_ => CreateTypeMappings(root, false, _.Name, _.PropertyType, arraySegments));
                lst.AddRange(mapped);
            }

            return lst;
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

        private class MapDescription : IEquatable<MapDescription>
        {
            public MapDescription(Regex regex, bool isIndexer, Type type)
            {
                Regex = regex;
                IsIndexer = isIndexer;
                Type = type;
            }
            public Regex Regex { get; }

            public bool IsIndexer { get; }
            public Type Type { get; }

            public bool Equals(MapDescription other)
            {
                return Regex.ToString() == other.Regex.ToString()
                       && IsIndexer == other.IsIndexer
                       && Type == other.Type;
            }

            public override int GetHashCode()
            {
                return Regex.ToString().GetHashCode() ^ IsIndexer.GetHashCode() ^ Type.FullName.GetHashCode();
            }
        }
    }
}