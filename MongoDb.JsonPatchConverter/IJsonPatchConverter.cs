using Microsoft.AspNetCore.JsonPatch;

namespace MongoDb.JsonPatchConverter
{
    public interface IJsonPatchConverter
    {
        ConversionResult<TOut> Convert<TOut, TModel>(JsonPatchDocument<TModel> document) where TModel : class;
    }
}