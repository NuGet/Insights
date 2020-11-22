namespace Knapcode.ExplorePackages.Logic.Worker
{
    public class StorageId
    {
        private readonly string _value;

        public StorageId(string sortable, string unique)
        {
            Sortable = sortable;
            Unique = unique;
            _value = sortable + "-" + unique;
        }

        public string Sortable { get; }
        public string Unique { get; }

        public override string ToString() => _value;
    }
}
