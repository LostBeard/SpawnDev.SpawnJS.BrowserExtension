using Microsoft.Build.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SpawnDev.SpawnJS.BrowserExtension.Build.Tasks
{
    public static class JsonObjectExtensions
    {
        public static List<string> GetPropertyNames(this JsonObject _this) => _this.Select(o => o.Key).ToList();
        /// <summary>
        /// Shallow top-level merge: keys from <paramref name="newerProperties"/> override the base;
        /// base key order is preserved and new keys are appended.
        /// </summary>
        public static JsonObject Merge(this JsonObject _this, JsonObject newerProperties)
        {
            var ret = new JsonObject();
            var origKeys = _this.GetPropertyNames();
            var newerKeys = newerProperties.GetPropertyNames();
            var allKeys = origKeys.Union(newerKeys).ToList();
            foreach (var key in allKeys)
            {
                if (newerKeys.Contains(key))
                {
                    ret.Add(key, newerProperties[key].DeepClone());
                }
                else
                {
                    ret.Add(key, _this[key].DeepClone());
                }
            }
            return ret;
        }
    }

    /// <summary>
    /// Publish-time browser-extension assembler. For each platform it:
    ///   1. copies the published wwwroot into &lt;platform&gt;/app/,
    ///   2. merges manifest.&lt;platform&gt;.json onto the base manifest.json and writes the result to
    ///      &lt;platform&gt;/manifest.json (the ONLY file at the extension root),
    ///   3. zips &lt;platform&gt;/ into &lt;platform&gt;.zip.
    /// Platforms are the manifest.&lt;platform&gt;.json partials found in wwwroot; with none, a single
    /// generic "browser" output is produced from the base manifest alone.
    /// </summary>
    public class PostPublishTask : Microsoft.Build.Utilities.Task
    {
        /// <summary>Publish output directory (PublishDir). The published app lives under OutputPath/wwwroot.</summary>
        [Required]
        public string OutputPath { get; set; }

        /// <summary>True only on publish; the task no-ops otherwise.</summary>
        [Required]
        public bool PublishMode { get; set; }

        /// <summary>
        /// Optional explicit ';'-separated platform list. Empty => derive from manifest.&lt;platform&gt;.json
        /// partials, falling back to a single "browser" output. "false"/"0" disables the extension build.
        /// </summary>
        public string ExtensionPlatforms { get; set; }

        /// <summary>Create a &lt;platform&gt;.zip for each assembled platform folder.</summary>
        public bool Zip { get; set; } = true;

        /// <summary>Launch a debugger at task start (opt-in diagnostic).</summary>
        public bool DebugSpawnDevBrowserExtensionBuildTasks { get; set; }

        /// <summary>If true, messages are upgraded to warnings so they surface in normal build output.</summary>
        public bool Verbose { get; set; }

        string OutputWwwroot { get; set; }

        public override bool Execute()
        {
            LogMessage("**********************************  PostPublishTask.Execute  **********************************");
            if (DebugSpawnDevBrowserExtensionBuildTasks)
            {
                System.Diagnostics.Debugger.Launch();
            }
            var platformsSetting = (ExtensionPlatforms ?? "").Trim();
            if (platformsSetting == "0" || platformsSetting.Equals("false", StringComparison.OrdinalIgnoreCase))
            {
                LogMessage("Extension build disabled (SpawnDevBrowserExtensionPlatforms=false).");
                return true;
            }
            if (!PublishMode)
            {
                return true;
            }
            OutputWwwroot = Path.GetFullPath(Path.Combine(OutputPath, "wwwroot"));
            if (!Directory.Exists(OutputWwwroot))
            {
                LogWarning($"Published wwwroot not found at {OutputWwwroot}; skipping extension build.");
                return true;
            }
            var baseManifestPath = Path.Combine(OutputWwwroot, "manifest.json");
            if (!File.Exists(baseManifestPath))
            {
                LogMessage("No wwwroot/manifest.json; project is not configured as a browser extension. Skipping.");
                return true;
            }
            // Platform names come from manifest.<platform>.json partials (base manifest.json yields "" and is skipped).
            var partialFiles = Directory.GetFiles(OutputWwwroot, "manifest.*.json");
            var manifestPlatforms = partialFiles
                .Select(o => string.Join(".", Path.GetFileNameWithoutExtension(o).Split('.').Skip(1)))
                .Where(o => !string.IsNullOrEmpty(o))
                .ToArray();
            // Resolve: explicit list > derived partials > single generic "browser".
            string[] extensionPlatforms;
            if (!string.IsNullOrEmpty(platformsSetting))
                extensionPlatforms = platformsSetting.Split(';').Select(o => o.Trim()).Where(o => o.Length > 0).ToArray();
            else if (manifestPlatforms.Length > 0)
                extensionPlatforms = manifestPlatforms;
            else
                extensionPlatforms = new[] { "browser" };

            LogMessage($"Platforms: {string.Join(", ", extensionPlatforms)}");
            foreach (var extensionPlatform in extensionPlatforms)
            {
                PublishPlatform(extensionPlatform);
            }
            LogMessage("Publish platforms complete");
            return true;
        }

        void LogMessage(string msg)
        {
            if (Verbose) LogWarning($"VERBOSE: {msg}");
            else Log?.LogMessage($"BrowserExtension: {msg}");
        }
        void LogWarning(string msg)
        {
            Log?.LogWarning($"BrowserExtension: {msg}");
        }

        static JsonSerializerOptions DefaultJsonSerializerOptions { get; } = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        void PublishPlatform(string platform)
        {
            var publishWwwrootPath = OutputWwwroot;
            var platformOutputPath = Path.GetFullPath(Path.Combine(OutputPath, platform));
            var platformAppOutputPath = Path.Combine(platformOutputPath, "app");
            // Fresh output so re-publish is idempotent (CopyDirectory does not overwrite).
            if (Directory.Exists(platformOutputPath)) Directory.Delete(platformOutputPath, true);
            // App payload (contents of published wwwroot) lives under app/.
            CopyDirectory(publishWwwrootPath, platformAppOutputPath);
            // Base (shared) manifest.
            var platformAppSharedManifestPath = Path.Combine(platformAppOutputPath, "manifest.json");
            var manifestJson = File.ReadAllText(platformAppSharedManifestPath, Encoding.UTF8);
            // Merge the platform-specific partial if present.
            var platformAppManifestPaths = Directory.GetFiles(platformAppOutputPath, "manifest.*.json").ToList();
            var platformAppManifestPath = platformAppManifestPaths.FirstOrDefault(o => Path.GetFileName(o).Equals($"manifest.{platform}.json", StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(platformAppManifestPath) && File.Exists(platformAppManifestPath))
            {
                var manifestPlatformStr = File.ReadAllText(platformAppManifestPath);
                var manifestCommon = JsonSerializer.Deserialize<JsonNode>(manifestJson, DefaultJsonSerializerOptions).AsObject();
                var manifestPlatform = JsonSerializer.Deserialize<JsonNode>(manifestPlatformStr, DefaultJsonSerializerOptions).AsObject();
                var manifestFinal = manifestCommon.Merge(manifestPlatform);
                manifestJson = manifestFinal.ToJsonString(DefaultJsonSerializerOptions);
                LogMessage($"[{platform}] merged manifest.{platform}.json onto base manifest.json");
            }
            else
            {
                LogMessage($"[{platform}] no platform manifest partial; using base manifest.json");
            }
            // Strip every manifest*.json from app/ - only the merged manifest belongs at the extension root.
            platformAppManifestPaths.ForEach(o => File.Delete(o));
            if (File.Exists(platformAppSharedManifestPath)) File.Delete(platformAppSharedManifestPath);
            // Write merged manifest at the extension root (UTF-8, no BOM).
            var platformOutputManifestPath = Path.Combine(platformOutputPath, "manifest.json");
            File.WriteAllText(platformOutputManifestPath, manifestJson, new UTF8Encoding(false));
            LogMessage($"[{platform}] wrote {platformOutputManifestPath}");
            // Zip the assembled extension (forward-slash entries - see ZipDirectory).
            if (Zip)
            {
                var zipPath = platformOutputPath + ".zip";
                ZipDirectory(platformOutputPath, zipPath);
                LogMessage($"[{platform}] zipped -> {Path.GetFileName(zipPath)}");
            }
        }

        /// <summary>
        /// Zips the CONTENTS of <paramref name="sourceDir"/> writing entry paths with FORWARD SLASHES.
        /// Built by hand with ZipArchive rather than ZipFile.CreateFromDirectory because, under the .NET
        /// Framework MSBuild that Visual Studio uses, CreateFromDirectory writes BACKSLASH separators -
        /// which Firefox rejects (every app/ subfolder file 404s: "Loading failed for the &lt;script&gt;").
        /// Forcing '/' here is host-independent (VS desktop MSBuild AND dotnet CLI).
        /// </summary>
        static void ZipDirectory(string sourceDir, string zipPath)
        {
            if (File.Exists(zipPath)) File.Delete(zipPath);
            var baseFull = Path.GetFullPath(sourceDir)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            using (var fs = new FileStream(zipPath, FileMode.CreateNew))
            using (var archive = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
                {
                    var full = Path.GetFullPath(file);
                    var rel = full.Substring(baseFull.Length).Replace('\\', '/');
                    var entry = archive.CreateEntry(rel, CompressionLevel.Optimal);
                    try { entry.LastWriteTime = new DateTimeOffset(File.GetLastWriteTime(file)); }
                    catch { /* out-of-range timestamps: leave default */ }
                    using (var es = entry.Open())
                    using (var ins = File.OpenRead(file))
                        ins.CopyTo(es);
                }
            }
        }

        static void CopyDirectory(string sourceDir, string destinationDir, bool recursive = true)
        {
            var dir = new DirectoryInfo(sourceDir);
            if (!dir.Exists) throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");
            DirectoryInfo[] dirs = dir.GetDirectories();
            Directory.CreateDirectory(destinationDir);
            foreach (FileInfo file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath);
            }
            if (recursive)
            {
                foreach (DirectoryInfo subDir in dirs)
                {
                    string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                    CopyDirectory(subDir.FullName, newDestinationDir);
                }
            }
        }
    }
}
