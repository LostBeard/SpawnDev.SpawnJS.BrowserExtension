
using System.Diagnostics.CodeAnalysis;
using SpawnDev.SpawnJS;

namespace SpawnDev.SpawnJS.BrowserExtension
{
    public class StorageChange<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T> : SpawnJSObject
    {
        public StorageChange(SpawnJSObjectReference _ref) : base(_ref) { }
        // The class's T. These used to declare their own <T>, which shadowed it (CS0693): a StorageChange<Settings>
        // offered no typed read, and every caller had to restate the type as OldValue<Settings>().
        public T OldValue() => JSRef!.Get<T>("oldValue");
        public T NewValue() => JSRef!.Get<T>("newValue");
    }
    public class StorageChanges : SpawnJSObject
    {
        public StorageChanges(SpawnJSObjectReference _ref) : base(_ref) { }
        public List<string> Keys => JS.Call<SpawnJSObjectReference, List<string>>("Object.keys", JSRef!);
        public StorageChange<T> Get<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(string key) => JSRef!.Get<StorageChange<T>>(key);
    }
}
