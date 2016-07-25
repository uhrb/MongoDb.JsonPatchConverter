using System;

namespace MongoDb.JsonPatchConverter.Tests.TestClasses
{
    public class UserEntity
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public double Rating { get; set; }
        public int TotalPets { get; set; }
        public Dog[] Dogs { get; set; }
    }
}
