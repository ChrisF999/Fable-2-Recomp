using System.IO;
using System.Security.Cryptography;
using Fable2Launcher.Constants;

namespace Fable2Launcher;

public sealed record GameCompatibility(string Label, string Message, bool Supported, string? Profile = null);

public static class GameCompatibilityInspector
{
    public static GameCompatibility Inspect(string directory)
    {
        string xex = Path.Combine(directory, "default.xex");
        if (!File.Exists(xex))
            return new("Content missing", "default.xex was not found in the selected game folder.", false);
        using FileStream input = File.OpenRead(xex);
        string hash = Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant();
        return Classify(hash,
            path => File.Exists(Path.Combine(directory, path)),
            path => Directory.Exists(Path.Combine(directory, path)));
    }

    // Pure, catalogue-driven classification; tests supply marker predicates.
    // Adding a validated locale does not require a new hash/locale if-branch.
    public static GameCompatibility Classify(string hash, Func<string, bool> hasFile,
        Func<string, bool> hasDirectory, IReadOnlyList<GameVersion>? versions = null)
    {
        foreach (GameVersion version in versions ?? GameVersions.All)
        {
            if (!string.Equals(hash, version.Hash, StringComparison.OrdinalIgnoreCase)) continue;
            if (!version.Compatible)
                return new(version.Name + " detected", version.Reason, false);
            if (version.CodeGroup != GameVersions.CodeGroup)
                return new(version.Name + " detected",
                    "This executable's guest-code group is not compiled into this build.", false);
            bool hasRejectionRule = version.RejectFiles.Length + version.RejectDirectories.Length > 0;
            if (hasRejectionRule && version.RejectFiles.All(hasFile) &&
                version.RejectDirectories.All(hasDirectory))
                return new("Mixed files detected",
                    "This XEX is paired with incompatible retail/TU1 content. Use a complete matching dump.", false);
            if (!version.RequiredFiles.All(hasFile))
                return new("Incomplete " + version.Name + " files",
                    "Required content or localization is missing. Select a complete matching dump.", false);
            return new(version.Name + " detected", version.Reason, true, version.Id);
        }
        return new("Unknown XEX build", $"Unknown default.xex SHA-256: {hash}", false);
    }
}
