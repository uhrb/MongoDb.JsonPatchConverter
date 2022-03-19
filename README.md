![nuget](https://img.shields.io/nuget/v/MongoDb.JsonPatchConverter)

# MongoDb.JsonPatchConverter
MongoDb.JsonPatchConverter is a simple library to convert from JsonPatchDocument&lt;T&gt; to FilterDefiniton&lt;T&gt; and UpdateDefinition&lt;T&gt; 

# Installation 
*Change `<version>` to your version of choice*

Package Manager Console: 
```
Install-Package MongoDb.JsonPatchConverter -Version <version>
```

.NET CLI:
```
dotnet add package MongoDb.JsonPatchConverter --version <version>
```

# Usage
### First, create an instance of `MongoDb.JsonPatchConverter.JsonPatchConverter`
```cs
using MongoDb.JsonPatchConverter;

...

var converter = new JsonPatchConverter();
```
Or, if you prefer the ASP.NET Core way:

```cs
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<JsonPatchConverter>();

...
```

### Using the converter in a controller:
```cs
[ApiController]
public class SampleController : ControllerBase
{
  private readonly JsonPatchConverter _converter;
  private readonly IMongoCollection<Sample> _samples;
  
  public SampleController(JsonPatchConverter converter, IMongoCollection<Sample> samples) 
  {
    _converter = converter;
    _smaples = samples;
  }

  [HttpPatch]
  public async Task<IActionResult> Modify([FromBody] JsonPatchDocument<Sample> descriptor)
  {
      await _converter.Convert(descriptor).Apply(_samples);
      return NoContent();
  }
}
```

If you are using different model for `JsonPatchDocument` and your underlying database, such as Data Transfer Objects (DTOs), you can specify additional type parameters in `Apply<TOut, TModel>(...)`
```cs
...

[HttpPatch]
public async Task<IActionResult> Modify([FromBody] JsonPatchDocument<SampleDTO> descriptor)
{
    await _converter.Convert<Sample, SampleDTO>(descriptor).Apply(_samples);
    return NoContent();
}
```

### Advance usages
The above example will convert a `JsonPatchDocument<Sample>` and apply the changes to the `_sample` collection. However, if you need to define additional filter and operation, you can use another overload of `Apply(...)`, the example below will include a id match, userid match and update the timestamp upon updating the document:
```cs
... 

[HttpPatch]
public async Task<IActionResult> Modify([FromRoute] string id, [FromQuery] string userId, [FromBody] JsonPatchDocument<Sample> descriptor)
{
    var filter = Builders<Sample>.Filter.Eq(x => x.Id, id) & Builders<Sample>.Filter.Eq(x => x.UserId, userId);
    var update = Builders<Sample>.Update.Set(x => x.Timestamp, DateTime.UtcNow);
    await _converter.Convert(descriptor).Apply(_samples, filter, update);
    return NoContent();
}
```

If you want to get the definitions, it is located in `Filters` and `Updates` properties of `ConversionResult<TOut>`
```cs
...

var result = _converter.Convert(descriptor);
var filterDef = result.Filters;
var updateDef = result.Updates;
// Do something with these definitions

...
```

# Configuration
### Cammel case field serializer

You can define serialize and deserialize configuration in `JsonPatchConverter` constructor: 
```cs
var serializeConfig = new JsonSerializerSettings 
{ 
    ContractResolver = new CamelCasePropertyNamesContractResolver() 
};

var converter = new JsonPatchConverter(new MapRegistry(), serializeConfig, x => {});
```

# License
This project is licensed under the [MIT License](https://opensource.org/licenses/MIT). Please refer to the terms in the `LICENSE` file included in the repository.
