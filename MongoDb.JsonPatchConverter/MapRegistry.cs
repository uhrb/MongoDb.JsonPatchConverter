using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace MongoDb.JsonPatchConverter
{
    public class MapRegistry : IMapRegistry
    {
        private readonly ConcurrentDictionary<Type, MapDescription[]> _dictionary;
        private const string StringMappingNotAllowed = "String mapping is not allowed";

        public MapRegistry()
        {
            _dictionary = new ConcurrentDictionary<Type, MapDescription[]>();
        }

        public void MapType<T>() where T : class => MapType(typeof(T));

        public void MapType(Type type)
        {
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

        public IEnumerable<MapDescription> GetMap<T>() => GetMap(typeof(T));

        public IEnumerable<MapDescription> GetMap(Type t)
        {
            if (!_dictionary.TryGetValue(t, out MapDescription[] map))
            {
                MapType(t);
                if (!_dictionary.TryGetValue(t, out map))
                {
                    yield break;
                }
            }

            foreach (var mapDescription in map)
            {
                yield return new MapDescription(mapDescription.Regex, mapDescription.IsIndexer, mapDescription.Type);
            }
        }

        private static IEnumerable<MapDescription> CreateTypeMappings(string previousRoot, bool isIndexer, string name, Type t, string[] arraySegments)
        {
            var root = string.IsNullOrEmpty(name) ? previousRoot : $"{previousRoot}/{name}";
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
    }
}