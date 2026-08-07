using Microsoft.JSInterop;
using SpawnDev.SpawnJS.JSObjects;

namespace SpawnDev.SpawnJS.BrowserExtension.Services
{
    /// <summary>
    /// Event handle for custom event dispatched when the page location change has been detected.
    /// </summary>
    class LocationChangeEvent : Event
    {
        /// <summary>
        /// Deserialization constructor
        /// </summary>
        /// <param name="_ref"></param>
        public LocationChangeEvent(SpawnJSObjectReference _ref) : base(_ref) { }
        /// <summary>
        /// The new location
        /// </summary>
        public string Detail => JSRef.Get<string>("detail");
    }
}
