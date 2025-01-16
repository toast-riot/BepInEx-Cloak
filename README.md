<h1 align="center">BepInEx Cloak · A plugin hider</h1>
A universal BepInEx patcher that prevents common methods of detecting if a specified plugin is loaded, effectively "cloaking" it.

## How to Use
1. Download the DLL from releases, then put it in your `BepInEx/patchers` folder.
2. Launch the game once, to generate the config file.
3. Open the config file (`BepInEx/config/Cloak.cfg`).
4. Add the GUIDs of the plugins you wish to hide, separated by commas, on the "plugins to hide" line.

> [!TIP]
> Cloak logs the name and GUID of all loaded plugins to the BepInEx log once the game has started.

> [!IMPORTANT]
> This is a preloader patcher, **not a plugin**. It must be placed in the `patchers` folder.

## Purpose
Sometimes modded content has a basic anticheat system that prevents certain plugins loading, inadvertently making it quite difficult to debug. These systems are usually easy but tedious to work around. To avoid needing to create a workaround for each individual case, I created this "fix-most" solution.

## Notes
- This is intended to be used by developers, for debugging and modding.
- Does not patch obsolete methods, and therefore may not prevent older systems of detecting plugins.
- Only tested on BepInEx 5.4.x (LTS).

## Troubleshooting
#### If the plugins are still being detected
- Ensure the DLL is placed in `BepInEx/patchers`, **not** `BepInEx/plugins`
- Verify the correct GUIDs are set in Cloak's config.
  - Check the log for any "did not find plugin" warnings.

#### If encountering errors
- Plugins may stop working if you hide one of their dependent plugins.

If you can replicate a bug, feel free to create an issue.

## Disclaimer
> [!CAUTION]
> This tool is **not** designed to, and most likely **will not** bypass detection by proper anti-cheat systems. **Do not use this tool to cheat.**