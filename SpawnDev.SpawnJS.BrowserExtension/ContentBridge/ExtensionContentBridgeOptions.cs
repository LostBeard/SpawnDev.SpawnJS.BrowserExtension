using SpawnDev.SpawnJS.JSObjects;
using System.Text.Json.Serialization;

namespace SpawnDev.SpawnJS.BrowserExtension
{
    public class ExtensionContentBridgeOptions
    {
        public string InstanceId { get; set; }
        public string RemoteInstanceId { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? Serve { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public EventTarget? EventElement { get; set; }
        public ExtensionContentBridgeOptions(string instanceId, string remoteInstanceId, EventTarget? eventElement = null)
        {
            InstanceId = instanceId;
            RemoteInstanceId = remoteInstanceId;
            EventElement = eventElement;
        }
    }
}
