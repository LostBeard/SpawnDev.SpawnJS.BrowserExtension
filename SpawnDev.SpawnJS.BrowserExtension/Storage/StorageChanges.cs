
using SpawnDev.SpawnJS;

namespace SpawnDev.SpawnJS.BrowserExtension
{
    public class StorageChange<T> : SpawnJSObject
    {
        public StorageChange(SpawnJSObjectReference _ref) : base(_ref) { }
        public T OldValue<T>() => JSRef!.Get<T>("oldValue");
        public T NewValue<T>() => JSRef!.Get<T>("newValue");
    }
    public class StorageChanges : SpawnJSObject
    {
        public StorageChanges(SpawnJSObjectReference _ref) : base(_ref) { }
        public List<string> Keys => JS.Call<SpawnJSObjectReference, List<string>>("Object.keys", JSRef!);
        public StorageChange<T> Get<T>(string key) => JSRef!.Get<StorageChange<T>>(key);
    }
}
