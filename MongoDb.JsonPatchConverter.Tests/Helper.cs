using System;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace MongoDb.JsonPatchConverter.Tests
{
    public static class Helper
    {
        public static string RenderToString<T>(FilterDefinition<T> filter)
        {
            var serializerRegistry = BsonSerializer.SerializerRegistry;
            var documentSerializer = serializerRegistry.GetSerializer<T>();
            // ReSharper disable once SpecifyACultureInStringConversionExplicitly
            var result = filter.Render(documentSerializer, serializerRegistry).ToString();
            return result;
        }

        public static string RenderToString<T>(UpdateDefinition<T> filter)
        {
            var serializerRegistry = BsonSerializer.SerializerRegistry;
            var documentSerializer = serializerRegistry.GetSerializer<T>();
            // ReSharper disable once SpecifyACultureInStringConversionExplicitly
            var result = filter.Render(documentSerializer, serializerRegistry).ToString();
            return result;
        }


        public static OneTimeCollection<T> GetCollection<T>()
        {
            return new OneTimeCollection<T>();
        }

        public class OneTimeCollection<T> : IDisposable
        {
            private readonly IMongoDatabase _db;
            private readonly string _collName;
            private readonly string _dbName;

            public OneTimeCollection()
            {
                var client = new MongoClient("mongodb://localhost:27017");
                _dbName = "TEST_DB" + Guid.NewGuid().ToString("N");
                _db = client.GetDatabase(_dbName);
                _collName = "COL" + Guid.NewGuid().ToString("N");
            }

            public IMongoCollection<T> Collection => _db.GetCollection<T>(_collName);
            public void Dispose()
            {
                var client = new MongoClient("mongodb://localhost:27017");
                client.DropDatabase(_dbName);
            }
        }
    }
}