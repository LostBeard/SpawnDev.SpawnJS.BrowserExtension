using System.Text.Json.Serialization;

namespace SpawnDev.SpawnJS.BrowserExtension
{
    /// <summary>
    /// The type of frame.<br/>
    /// https://developer.chrome.com/docs/extensions/reference/api/extensionTypes#type-FrameType
    /// </summary>
    public enum FrameType
    {
        /// <summary>
        /// OutermostFrame
        /// </summary>
        [JsonPropertyName("outermost_frame")]
        OutermostFrame,
        /// <summary>
        /// FencedFrame
        /// </summary>
        [JsonPropertyName("fenced_frame")]
        FencedFrame,
        /// <summary>
        /// SubFrame
        /// </summary>
        [JsonPropertyName("sub_frame")]
        SubFrame,
    }
}
