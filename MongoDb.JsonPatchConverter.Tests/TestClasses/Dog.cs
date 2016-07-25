namespace MongoDb.JsonPatchConverter.Tests.TestClasses
{
    public class Dog
    {
        public string Name { get; set; }
        public int Age { get; set; }
        public string FavoriteFood { get; set; }
        public Leg[] Legs { get; set; }
    }
}