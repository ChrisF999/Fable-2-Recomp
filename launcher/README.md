# Fable II Recomp Launcher

The launcher edits the ReXGlue cvar file `fable_2.toml` without discarding
unknown settings, then starts `fable_2.exe` from its own directory. Game data
may live elsewhere: the launcher passes the selected root with `--game_data_root`.

Managed settings:

- output/window resolution (`window_width`, `window_height`)
- internal 3D render scale (`resolution_scale`)
- anisotropic texture filtering (`anisotropic_override`)
- optional post-process anti-aliasing: Off, FXAA, FXAA Extreme (`swap_post_effect`)
- VSync and fullscreen mode

The launcher clears the combined `resolution` shortcut on save. This keeps the
emulated Xbox video mode at its original 720p and controls presentation size
separately, so a 3x internal scale means 3840x2160 rather than accidentally
multiplying an already overridden 4K guest mode.

FXAA uses the renderer's existing post-process option; Off leaves game-controlled
AA alone. Higher internal render scales also allow supersampling when displayed
at a lower output resolution. This is a graphics/settings launcher, not a texture,
font, remaster or save editor. Dropdowns use dark text on a light background,
with explicit selected/hover colors for readability.

The first save keeps the original config as
`fable_2.toml.launcher-backup`. The selected game directory is remembered in
the current user's local application data and is not written to the repo.

## Build

Requires the .NET 8 SDK on Windows:

```cmd
launcher\build-launcher.cmd
```

This creates a small framework-dependent single-file executable in
`out\launcher`. For a distributable build that bundles the .NET runtime:

```cmd
launcher\build-launcher.cmd self-contained
```

Place `Fable2Launcher.exe` next to `fable_2.exe` and the generated
`fable2_build.json`. Choose the original gamefiles folder containing
`default.xex` and `data` (not necessarily the EXE folder).

The launcher automatically identifies GOTY USA/Europe or German GOTY from
the exact original XEX hash and content markers. The dual-GOTY native build
(`FABLE2_BUILD_PROFILE=goty-compatible`, default) accepts both verified images
and seeds English or German only if the user has not explicitly set a language.
An old USA/EU executable without a build descriptor cannot launch German GOTY.
Unknown, incomplete and mixed retail/TU1 content is rejected before launching.
The native application independently repeats validation, so direct EXE startup
cannot bypass the profile check. This is not general support for arbitrary
German/Russian/retail XEXs.

Settings, saves, logs and shader caches remain beside the native EXE; game dumps
are only read. Public packages must contain the launcher, native EXE, matching
runtime/plugin and build descriptor, never original XEXs, game data or saves.
