using System.IO;
using System.Security.Cryptography;

namespace Fable2Launcher;

public sealed record GameCompatibility(string Label, string Message, bool Supported, string? Profile = null);

public static class GameCompatibilityInspector
{
    private const string GotyHash = "88c4ef2e18e65409444d1b068eff921d1f7e180a5ae64edc64ba6b0872372662";
    private const string GermanHash = "cec9238ef5d7b391345a8897ef00a4673ae4f106f89385c81723ba5f9d0807b5";
    private const string GermanGotyHash = "3f36e7870a06e04b3702760e93c61b1c7fded321b94021da6bfa120b424e6eb4";

    public static GameCompatibility Inspect(string directory)
    {
        string xex = Path.Combine(directory, "default.xex");
        if (!File.Exists(xex))
            return new("Content missing", "default.xex was not found in the selected game folder.", false);

        string hash;
        using (FileStream input = File.OpenRead(xex))
            hash = Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant();

        bool hasGotyMarkers = File.Exists(Path.Combine(directory, "data", "gold_version.txt")) &&
                              File.Exists(Path.Combine(directory, "data", "startup.vfsconfig"));
        bool hasGermanMarkers = File.Exists(Path.Combine(directory, "data", "tu1_data.bnk")) &&
                                Directory.Exists(Path.Combine(directory, "data", "language", "de-de"));

        bool hasGermanLocalization = File.Exists(Path.Combine(directory, "data", "language", "de-de", "text", "book.babel"));
        return Classify(hash, hasGotyMarkers, hasGermanMarkers, hasGermanLocalization);
    }

    // Pure classification also permits synthetic tests without redistributing game assets.
    public static GameCompatibility Classify(string hash, bool hasGotyMarkers,
        bool hasGermanMarkers, bool hasGermanLocalization)
    {
        if (hash == GotyHash && hasGermanMarkers)
            return new("Mixed files detected", "The supported GOTY XEX is paired with German retail/TU1 data. This combination is known to freeze during the intro.", false);
        if (hash == GotyHash && hasGotyMarkers)
            return new("GOTY USA/Europe detected", "Original GOTY USA/Europe executable and content detected.", true, "goty-us-eu");
        if (hash == GermanGotyHash && hasGotyMarkers && hasGermanLocalization && !hasGermanMarkers)
            return new("German GOTY detected", "Original German GOTY executable and localization detected.", true, "goty-german");
        if (hash == GotyHash || hash == GermanGotyHash)
            return new("Incomplete or mixed GOTY files", "Required GOTY content or German localization is missing, or retail/TU1 files are mixed in.", false);
        if (hash == GermanHash)
            return new("German build detected", "German retail/TU1 is catalogued but needs its own recompilation profile; this executable is not supported yet.", false);
        return new("Unknown XEX build", $"Unknown default.xex SHA-256: {hash}", false);
    }
}
