# EC_CorePlugins
Collection of essential plugins for Emotion Creators. They are still under heavy development and will most likely have bugs or missing features. To get the latest nightlies visit https://builds.bepis.io/ec_coreplugins

## Installation
1. Install [Bepinex 5](https://builds.bepis.io/bepinex_be) - BepInEx build for post-Unity 2018 game (x64)
2. In \BepInEx\config\BepInEx.cfg set Assembly = UnityEngine.CoreModule.dll
3. Download the [latest build of EC_CorePlugins](https://builds.bepis.io/ec_coreplugins)
5. Extract the plugin .zip file to your EmotionCreators folder

## Plugin descriptions
### ExtensibleSaveFormat
Allows additional data to be saved to character, coordinate and scene cards. The cards are fully compatible with non-modded game, the additional data is lost in that case. This is used by sideloader and many other plugins to store used mod information.

### MessagePopups
Allows plugins to display on-screen messages.

### ResourceRedirector
Allows other plugins to intercept and modify assets as they are loaded. This is notibly used by sideloader for its core functionality.

### Screencap
Creates screenshots based on settings. Can create screenshots of much higher resolution than what the game is running at. It can make screen (F9 key) or character (F11 key) screenshots.

### Sideloader
Loads mods packaged in .zip archives from the Mods directory without modifying the game files at all. You don't unzip them, just drag and drop to Mods folder in the game root.

It prevents mods from colliding with each other (i.e. 2 mods have same item IDs and can't coexist; sideloader automatically assigns correct IDs). It also makes it easy to disable/remove mods with no lasting effects on your game install (just remove the .zip, no game files are changed at any point).

[More information and tutorial on sideloader-compatible mod creation.](https://github.com/bbepis/BepisPlugins/wiki/Creating-.zip-mods)

### SliderUnlocker
Allows user to set values outside of the standard 0-100 range on all sliders in the editor. Type a value to unlock.

## Plugin descriptions - Fixes
### Culture Fix
Override the system's current language settings to prevent errors.

### Download Renamer
Maps, scenes, poses, and characters downloaded in game will have their file names changed to match the ones used by the Illusion website.

### Import Fixes
Fixes some errors that can occur when importing modded items on characters.

### Maker FPS optimization
Improves FPS in character maker at the cost of slower switching between tabs. Automatically enables for low end PCs.

### Null Checks
Additional error handling for problems caused by some modded clothing and hair items.
