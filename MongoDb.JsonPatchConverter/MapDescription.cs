using System;
using System.Text.RegularExpressions;

namespace MongoDb.JsonPatchConverter
{
    public class MapDescription : IEquatable<MapDescription>
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