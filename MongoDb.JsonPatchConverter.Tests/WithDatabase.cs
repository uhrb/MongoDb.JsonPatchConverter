using System;
using System.Linq;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.JsonPatch.Operations;
using MongoDb.JsonPatchConverter.Tests.TestClasses;
using MongoDB.Bson.Serialization;
using Newtonsoft.Json.Linq;
using Xunit;
using MongoDB.Driver;

namespace MongoDb.JsonPatchConverter.Tests
{
    public class WithDatabase
    {
        [Theory]
        [Trait("Category", "Database")]
        [InlineData("/Dogs", "[ {\"Name\":\"Sparky\" } ]", true)]
        public void ApplyReplaceArray(string path, object value, bool isArray)
        {
            var converter = Helper.GetConverter();
            var doc = new JsonPatchDocument<UserEntity>();
            BsonClassMap.RegisterClassMap<UserEntity>();
            doc.Operations.Add(new Operation<UserEntity>
            {
                from = string.Empty,
                op = "replace",
                path = path,
                value = isArray ? JArray.Parse((string)value) : value
            });
            var result = converter.Convert<UserEntity, UserEntity>(doc);
            using (var coll = Helper.GetCollection<UserEntity>())
            {
                var filtered = Builders<UserEntity>.Filter.And(result.Filters);
                var updates = Builders<UserEntity>.Update.Combine(result.Updates);
                coll.Collection.InsertOne(new UserEntity { Id = Guid.NewGuid(), Dogs = new []
                {
                    new Dog { Name = "Don"}
                }});
                var updateResult = coll.Collection.UpdateOne(filtered, updates);
                Assert.True(updateResult.ModifiedCount > 0);
                var entities = coll.Collection.Find("{}").ToList();
                Assert.True(entities.Any());
                Assert.NotNull(entities[0].Dogs);
                Assert.True(entities[0].Dogs.Any());
                Assert.True(entities[0].Dogs[0].Name != "Don");
            }
        }
    }
}