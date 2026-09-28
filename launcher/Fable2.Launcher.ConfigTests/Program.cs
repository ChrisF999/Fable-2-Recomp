using Fable2Launcher;
using System.Xml.Linq;

string testDirectory = Path.Combine(Path.GetTempPath(), "fable2-launcher-config-test-" + Guid.NewGuid());
Directory.CreateDirectory(testDirectory);

try
{
    string configPath = Path.Combine(testDirectory, "fable_2.toml");
    const string original = "# existing config\nresolution = \"720p\" # keep this note\ncustom_mod = true\n";
    File.WriteAllText(configPath, original);

    var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["resolution"] = "\"4k\"",
        ["window_width"] = "3840",
        ["resolution_scale"] = "3",
        ["vsync"] = "true"
    };
    string[] order = ["resolution", "window_width", "resolution_scale", "vsync"];

    LauncherConfigFile.WriteValues(configPath, settings, order);
    string updated = File.ReadAllText(configPath);
    Require(updated.Contains("resolution = \"4k\" # keep this note"), "managed value was not updated");
    Require(updated.Contains("custom_mod = true"), "unknown setting was not preserved");
    Require(updated.Contains("resolution_scale = 3"), "missing setting was not appended");
    Require(updated.Contains("window_width = 3840"), "output width was not appended");
    Require(File.ReadAllText(configPath + ".launcher-backup") == original, "backup differs from original");

    settings["resolution"] = "\"1080p\"";
    LauncherConfigFile.WriteValues(configPath, settings, order);
    Require(File.ReadAllText(configPath + ".launcher-backup") == original, "first backup was overwritten");

    Dictionary<string, string> roundTrip = LauncherConfigFile.ReadValues(configPath);
    Require(roundTrip["resolution"] == "\"1080p\"", "roundtrip resolution failed");
    Require(roundTrip["custom_mod"] == "true", "roundtrip unknown setting failed");
    Console.WriteLine("Launcher configuration roundtrip passed.");

    foreach (string aa in new[] { "none", "fxaa", "fxaa_extreme" })
    {
        var graphics = GraphicsSettings.Create("4k", "3", "5", aa, "borderless", true);
        Require(graphics["resolution"] == "\"\"", "guest resolution must remain native");
        Require(graphics["window_width"] == "3840" && graphics["window_height"] == "2160", "output dimensions failed");
        Require(graphics["resolution_scale"] == "3", "render scale failed");
        Require(graphics["anisotropic_override"] == "5", "16x filtering enum failed");
        Require(graphics["swap_post_effect"] == $"\"{aa}\"", "AA option failed");
        LauncherConfigFile.WriteValues(configPath, graphics, GraphicsSettings.ManagedKeys);
        Require(LauncherConfigFile.ReadValues(configPath)["swap_post_effect"] == $"\"{aa}\"", "AA roundtrip failed");
    }
    var windowed = GraphicsSettings.Create("720p", "1", "-1", "none", "windowed", false);
    Require(windowed["fullscreen"] == "false" && windowed["vsync"] == "false", "windowed/vsync failed");
    Require(GraphicsSettings.Create("1440p", "2", "4", "fxaa", "exclusive", true)["fullscreen_exclusive"] == "true", "exclusive fullscreen failed");
    RequireThrows(() => GraphicsSettings.Create("8k", "3", "5", "none", "windowed", true), "unsupported resolution accepted");
    RequireThrows(() => GraphicsSettings.Create("4k", "3", "5", "unknown", "windowed", true), "unsupported AA accepted");

    string tablePath = Path.Combine(testDirectory, "tables.toml");
    File.WriteAllText(tablePath, "custom = \"keep # literal\"\n[other]\nvsync = false\n");
    LauncherConfigFile.WriteValues(tablePath, windowed, GraphicsSettings.ManagedKeys);
    string tableContent = File.ReadAllText(tablePath);
    Require(tableContent.Contains("[other]\nvsync = false") || tableContent.Contains("[other]\r\nvsync = false"), "unrelated table was modified");
    Require(tableContent.Contains("custom = \"keep # literal\""), "unknown string was changed");
    Require(tableContent.IndexOf("window_width", StringComparison.Ordinal) < tableContent.IndexOf("[other]", StringComparison.Ordinal), "root key appended inside table");
    Require(LauncherConfigFile.ReadValues(tablePath)["vsync"] == "false", "root value not loaded");
    Console.WriteLine("Graphics options, anti-aliasing and table preservation passed.");

    string appXaml = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
        "..", "..", "..", "..", "Fable2.Launcher", "App.xaml"));
    XNamespace xamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    var styles = XDocument.Load(appXaml).Descendants(xamlNamespace + "Style").ToList();
    var textStyle = styles.Single(style => (string?)style.Attribute("TargetType") == "TextBlock");
    Require(!textStyle.Elements(xamlNamespace + "Setter").Any(setter => (string?)setter.Attribute("Property") == "Foreground"),
        "global TextBlock foreground overrides dropdown text inheritance");
    foreach (string control in new[] { "ComboBox", "ComboBoxItem" })
    {
        var style = styles.Single(style => (string?)style.Attribute("TargetType") == control);
        Require(style.Elements(xamlNamespace + "Setter").Any(setter => (string?)setter.Attribute("Property") == "Foreground" && (string?)setter.Attribute("Value") == "#FF172020"), "dropdown foreground regression");
        Require(style.Elements(xamlNamespace + "Setter").Any(setter => (string?)setter.Attribute("Property") == "Background" && (string?)setter.Attribute("Value") == "#FFF3EEE4"), "dropdown background regression");
    }
    Console.WriteLine("Dropdown text inheritance and contrast styles passed.");

    const string usHash = "88c4ef2e18e65409444d1b068eff921d1f7e180a5ae64edc64ba6b0872372662";
    const string deHash = "3f36e7870a06e04b3702760e93c61b1c7fded321b94021da6bfa120b424e6eb4";
    var us = GameCompatibilityInspector.Classify(usHash, true, false, false);
    var de = GameCompatibilityInspector.Classify(deHash, true, false, true);
    Require(us.Supported && us.Profile == "goty-us-eu", "USA/EU classification failed");
    Require(de.Supported && de.Profile == "goty-german", "German classification failed");
    Require(!GameCompatibilityInspector.Classify(usHash, true, true, true).Supported, "mixed US/TU1 accepted");
    Require(!GameCompatibilityInspector.Classify(deHash, true, false, false).Supported, "incomplete German accepted");
    Require(!GameCompatibilityInspector.Classify(deHash, false, false, true).Supported, "missing GOTY markers accepted");
    Require(!GameCompatibilityInspector.Classify(deHash, true, true, true).Supported, "mixed German/TU1 accepted");
    Require(!GameCompatibilityInspector.Classify("unknown", true, false, true).Supported, "unknown hash accepted");
    Require(!GameCompatibilityInspector.Classify("cec9238ef5d7b391345a8897ef00a4673ae4f106f89385c81723ba5f9d0807b5", false, true, true).Supported, "retail/TU1 accepted");
    File.WriteAllText(Path.Combine(testDirectory, "fable_2.exe"), "synthetic fixture, not an executable");
    GameLaunchPlanner.Create(testDirectory, testDirectory, us);
    RequireThrows(() => GameLaunchPlanner.Create(testDirectory, testDirectory, de), "legacy US executable accepted German");
    File.WriteAllText(Path.Combine(testDirectory, "fable2_build.json"), "{\"profiles\":[\"goty-german\"]}");
    RequireThrows(() => GameLaunchPlanner.Create(testDirectory, testDirectory, us), "German-only descriptor accepted USA/EU");
    GameLaunchPlanner.Create(testDirectory, testDirectory, de);
    File.WriteAllText(Path.Combine(testDirectory, "fable2_build.json"), "{\"profiles\":[\"goty-us-eu\",\"goty-german\"]}");
    var plan = GameLaunchPlanner.Create(testDirectory, testDirectory, de);
    Require(plan.GameRoot == Path.GetFullPath(testDirectory), "game root was not preserved");
    GameLaunchPlanner.Create(testDirectory, testDirectory, us);
    File.WriteAllText(Path.Combine(testDirectory, "fable2_build.json"), "{malformed");
    RequireThrows(() => GameLaunchPlanner.Create(testDirectory, testDirectory, de), "malformed descriptor accepted");
    Console.WriteLine("Profile recognition and dual-GOTY launch planning passed.");

    if (args.Length > 0)
    {
        GameCompatibility compatibility = GameCompatibilityInspector.Inspect(args[0]);
        Console.WriteLine($"Compatibility probe: {compatibility.Label} — {compatibility.Message}");
        if (args.Contains("--expect-german-goty", StringComparer.OrdinalIgnoreCase))
            Require(compatibility.Label == "German GOTY detected", "German GOTY profile was not recognized");
    }
}
finally
{
    Directory.Delete(testDirectory, recursive: true);
}

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static void RequireThrows(Action action, string message)
{
    try { action(); }
    catch { return; }
    throw new InvalidOperationException(message);
}
