using System;
using System.Linq;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.JsonPatch.Operations;
using MongoDb.JsonPatchConverter.Tests.TestClasses;
using Newtonsoft.Json.Linq;
using Xunit;

namespace MongoDb.JsonPatchConverter.Tests
{
    public class JsonConverter
    {

        [Fact]
        [Trait("Category", "General")]
        public void RegistersMapWithoutErrors()
        {
            Helper.GetConverter();
            Assert.True(true);
        }

        [Fact]
        [Trait("Category", "General")]
        public void ThrowsIfModelsNotIntersect()
        {
            var converter = Helper.GetConverter();
            Assert.Throws<InvalidOperationException>(()=>converter.Convert<Fruit, Dog>(new JsonPatchDocument<Dog>()));
        }

        [Fact]
        [Trait("Category", "General")]
        public void EmptyOperationsListReturnEmptyResult()
        {
            var converter = Helper.GetConverter();
            var result = converter.Convert<Cat, Dog>(new JsonPatchDocument<Dog>());
            Assert.False(result.HasErrors);
            Assert.False(result.Filters.Any());
            Assert.False(result.Errors.Any());
            Assert.False(result.Updates.Any());
        }

        [Theory]
        [Trait("Category", "General")]
        [InlineData("test")]
        [InlineData("move")]
        [InlineData("copy")]
        public void NotSupportedOperationReturnNotSupportedError(string op)
        {
            var converter = Helper.GetConverter();
            var doc = new JsonPatchDocument<Dog>();
            doc.Operations.Add(new Operation<Dog>
            {
                from = string.Empty,
                op = "test",
                path = "/Name"
            });
            var result = converter.Convert<Cat, Dog>(doc);
            Assert.True(result.HasErrors);
            Assert.True(result.Errors.Any(_=>_.OperationErrorType == OperationErrorType.NotSupported));
        }

        [Theory]
        [Trait("Category", "General")]
        [InlineData("asdasd")]
        [InlineData("/Doubled")]
        [InlineData("Detour/ASom")]
        [InlineData("/Name/1")]
        [InlineData("/Name]")]
        [InlineData("/Name{")]
        [InlineData("/Name}")]
        [InlineData("/Name$")]
        public void InvalidPathInOperationReturnsPathError(string path)
        {
            var converter = Helper.GetConverter();
            var doc = new JsonPatchDocument<Dog>();
            doc.Operations.Add(new Operation<Dog>
            {
                from = string.Empty,
                op = "add",
                path = path
            });
            var result = converter.Convert<Cat, Dog>(doc);
            Assert.True(result.HasErrors);
            Assert.True(result.Errors.Any(_ => _.OperationErrorType == OperationErrorType.PathNotValid));
        }

        [Theory]
        [Trait("Category", "Remove")]
        [InlineData("/Name", "{ \"Name\" : { \"$exists\" : true } }", "{ \"$unset\" : { \"Name\" : 1 } }")]
        [InlineData("/Dogs", "{ \"Dogs\" : { \"$exists\" : true } }", "{ \"$unset\" : { \"Dogs\" : 1 } }")]
        [InlineData("/Dogs/1/Name", "{ \"Dogs.1.Name\" : { \"$exists\" : true } }", "{ \"$unset\" : { \"Dogs.1.Name\" : 1 } }")]
        
        public void RemoveOperationReturnsFilterToExistAndUnsetOperation(string path, string filter, string update)
        {
            var converter = Helper.GetConverter();
            var doc = new JsonPatchDocument<UserEntity>();
            doc.Operations.Add(new Operation<UserEntity>
            {
                from = string.Empty,
                op = "remove",
                path = path
            });
            var result = converter.Convert<UserEntity, UserEntity>(doc);
            Assert.False(result.HasErrors);
            Assert.True(result.Filters.Count(_=> Helper.RenderToString(_) == filter) == 1);
            Assert.True(result.Updates.Count(_=> Helper.RenderToString(_) == update) == 1);
        }

        [Theory]
        [Trait("Category", "Remove")]
        [InlineData("/Dogs/1")]
        [InlineData("/Dogs/1/Legs/1")]
        // Remove array element by index is not supported https://jira.mongodb.org/browse/SERVER-1014
        public void RemoveByIndexReturnsNotSupportedError(string path)
        {
            var converter = Helper.GetConverter();
            var doc = new JsonPatchDocument<UserEntity>();
            doc.Operations.Add(new Operation<UserEntity>
            {
                from = string.Empty,
                op = "remove",
                path = path
            });
            var result = converter.Convert<UserEntity, UserEntity>(doc);
            Assert.True(result.HasErrors);
            Assert.False(result.Updates.Any());
            Assert.False(result.Filters.Any());
            Assert.True(result.Errors.Count(_ => _.OperationErrorType == OperationErrorType.NotSupported) == 1);
        }

        [Theory]
        [Trait("Category", "Replace")]
        [InlineData("/Name", null,"{ \"Name\" : { \"$exists\" : true } }", "{ \"$set\" : { \"Name\" : null } }", false)]
        [InlineData("/Rating", 1, "{ \"Rating\" : { \"$exists\" : true } }", "{ \"$set\" : { \"Rating\" : 1.0 } }", false)]
        [InlineData("/Dogs", "[ {\"Name\":\"Sparky\" } ]", "{ \"Dogs\" : { \"$exists\" : true } }", "{ \"$set\" : { \"Dogs\" : [{ \"Name\" : \"Sparky\", \"Age\" : 0, \"FavoriteFood\" : null, \"Legs\" : null }] } }", true)]
        [InlineData("/Dogs/1/Name", "Sparky" ,"{ \"Dogs.1.Name\" : { \"$exists\" : true } }", "{ \"$set\" : { \"Dogs.1.Name\" : \"Sparky\" } }", false)]
        [InlineData("/Dogs/2/FavoriteFood", "Meat","{ \"Dogs.2.FavoriteFood\" : { \"$exists\" : true } }", "{ \"$set\" : { \"Dogs.2.FavoriteFood\" : \"Meat\" } }", false)]
        public void ReplaceOperationReturnsFilterToExistAndSetOperation(string path,object value, string filter, string update, bool isArray)
        {
            var converter = Helper.GetConverter();
            var doc = new JsonPatchDocument<UserEntity>();
            doc.Operations.Add(new Operation<UserEntity>
            {
                from = string.Empty,
                op = "replace",
                path = path,
                value = isArray ? JArray.Parse((string)value) : value
            });
            var result = converter.Convert<UserEntity, UserEntity>(doc);
            Assert.False(result.HasErrors);
            Assert.True(result.Filters.Count(_ => Helper.RenderToString(_) == filter) == 1);
            Assert.True(result.Updates.Count(_ => Helper.RenderToString(_) == update) == 1);
        }

        
        [Theory]
        [Trait("Category", "Replace")]
        [InlineData("/Rating", "newRating")]
        [InlineData("/Dogs", "new dogs")]
        public void ReplaceOperationReturnsErrorsWhenTypeMissmatch(string path, object value)
        {
            var converter = Helper.GetConverter();
            var doc = new JsonPatchDocument<UserEntity>();
            doc.Operations.Add(new Operation<UserEntity>
            {
                from = string.Empty,
                op = "replace",
                path = path,
                value = value
            });
            var result = converter.Convert<UserEntity, UserEntity>(doc);
            Assert.True(result.HasErrors);
            Assert.True(result.Errors.Count(_=>_.OperationErrorType == OperationErrorType.TypeError) == 1);
            Assert.False(result.Filters.Any());
            Assert.False(result.Updates.Any());
        }


        [Theory]
        [Trait("Category", "Add")]
        [InlineData("/Name", "value")]
        [InlineData("/Rating", 10)]
        public void AddForRootPathReturnsNoFilter(string path, object value)
        {
            var converter = Helper.GetConverter();
            var doc = new JsonPatchDocument<UserEntity>();
            doc.Operations.Add(new Operation<UserEntity>
            {
                from = string.Empty,
                op = "add",
                path = path,
                value = value
            });
            var result = converter.Convert<UserEntity, UserEntity>(doc);
            Assert.False(result.HasErrors);
            Assert.False(result.Filters.Any());
            Assert.True(result.Updates.Any());
        }

        [Theory]
        [Trait("Category", "Add")]
        [InlineData("/asas", "value")]
        [InlineData("/aaaaaa/asdasdasd", 10)]
        public void AddOnNotExistancePathOnModelReturnsError(string path, object value)
        {
            var converter = Helper.GetConverter();
            var doc = new JsonPatchDocument<UserEntity>();
            doc.Operations.Add(new Operation<UserEntity>
            {
                from = string.Empty,
                op = "add",
                path = path,
                value = value
            });
            var result = converter.Convert<UserEntity, UserEntity>(doc);
            Assert.True(result.HasErrors);
            Assert.True(result.Errors.Count(_=>_.OperationErrorType == OperationErrorType.PathNotValid) == 1);
            Assert.False(result.Filters.Any());
            Assert.False(result.Updates.Any());
        }


        [Theory]
        [Trait("Category", "Add")]
        [InlineData("/Dogs/1/Name", "value", "{ \"Dogs.1\" : { \"$exists\" : true } }", "{ \"$set\" : { \"Dogs.1.Name\" : \"value\" } }")]
        [InlineData("/Dogs/1/Legs/1/IsOk", true, "{ \"Dogs.1.Legs.1\" : { \"$exists\" : true } }", "{ \"$set\" : { \"Dogs.1.Legs.1.IsOk\" : true } }")]
        public void AddOnArrayElementReturnsArrayShouldExists(string path, object value, string filter, string update)
        {
            var converter = Helper.GetConverter();
            var doc = new JsonPatchDocument<UserEntity>();
            doc.Operations.Add(new Operation<UserEntity>
            {
                from = string.Empty,
                op = "add",
                path = path,
                value = value
            });
            var result = converter.Convert<UserEntity, UserEntity>(doc);
            Assert.False(result.HasErrors);
            Assert.True(result.Filters.Count(_=> Helper.RenderToString(_) == filter) == 1);
            Assert.True(result.Updates.Count(_ => Helper.RenderToString(_) == update) == 1);
        }
    }
}
