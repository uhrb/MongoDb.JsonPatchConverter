using Microsoft.AspNetCore.JsonPatch;

namespace MongoDb.JsonPatchConverter
{
    public interface IJsonPatchConverter
    {
        void MapType<T>() where T : class;
        ConversionResult<TOut> Convert<TOut, TModel>(JsonPatchDocument<TModel> document) where TModel : class;
    }
}