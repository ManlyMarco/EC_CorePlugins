## How to contribute?
Create a new [fork](https://help.github.com/articles/fork-a-repo/) of this repository. Upload your changes to your fork and then [submit a pull request](https://help.github.com/articles/about-pull-requests/). Your pull request will be reviewed and accepted after a quality check. If there are any issues we will let you know what to do to fix them, so don't worry too much about submitting!

## How to build the project?
1. At least Visual Studio 2017 Community is required. Older versions or different environments can work, but were not tested.
2. Clone the repository to your drive and open the .sln file with Visual Studio.
3. You have to fix the references first before you are able to build the project. You can simply go to your game directory and copy contents of `EmotionCreators_Data\Managed` and `BepInEx\core` to the `lib` folder next to the .sln file you've just opened. All references should now be working.
4. Hit Build to make sure that everything is set up properly.

## Naming conventions
The main goal of these conventions is to make the solution and `BepInEx\plugins` folder structure cleaner and easier to navigate and understand. These conventions are not rules, only suggestions.

1. Plugin names should be in format `EC.Core.<PluginName>` or more generally `<GameName>.<ProjectName>.<PluginName>`. For example `EC.Core.Screencap` for Screenshot Manager.
2. Plugin project names, namespaces and GUIDs should all be the same as the plugin name.
3. If the plugin is using Harmony patches, the patches should be placed inside a `Hooks` class.
4. Hook classes should be private and contained within another class that is using these hooks. For example, hooks that are necessary for `ScreenshotManager` class to work should be placed inside `ScreenshotManager.Hooks` private subclass.
