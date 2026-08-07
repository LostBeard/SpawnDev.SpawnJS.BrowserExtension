namespace SpawnDev.SpawnJS.BrowserExtension.Build.Tasks
{
    /// <summary>
    /// Non-publish (dev / F5 / dotnet build) hook. Extension assembly (manifest merge + zip) only makes
    /// sense against a complete published wwwroot, so that work lives entirely in <see cref="PostPublishTask"/>.
    /// This task is intentionally a no-op for now; it stays wired as a placeholder for future build-time needs.
    /// </summary>
    public class PostBuildTask : Microsoft.Build.Utilities.Task
    {
        /// <summary>Launch a debugger at task start (opt-in diagnostic).</summary>
        public bool DebugSpawnDevBrowserExtensionBuildTasks { get; set; }

        /// <summary>If true, messages are upgraded to warnings so they surface in normal build output.</summary>
        public bool Verbose { get; set; }

        public override bool Execute()
        {
            if (DebugSpawnDevBrowserExtensionBuildTasks)
            {
                System.Diagnostics.Debugger.Launch();
            }
            return true;
        }
    }
}
