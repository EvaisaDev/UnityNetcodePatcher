

# Unity Netcode Patcher
**This is an assembly patcher which replicates the IL Post Processing that unity does with it's Netcode For Gameobjects Package, allowing you to create custom NetworkBehaviours in mods as if you were doing it in a Unity project.**

- This is somewhat experimental as it has not been tested properly yet.
- This was originally written for Lethal Company modding, and has only been tested with `com.unity.netcode.gameobjects@1.5.2`
- Currently only patches NetworkBehaviours. INetworkMessage and INetworkSerializable processing does not work yet.

*Note, this is intended to be a tool for modders, mods should be shipped after patching and this tool should not be installed by users.*

## Installation

1. Download the latest release from [Releases](https://github.com/EvaisaDev/UnityNetcodeWeaver/releases)
2. Move NetcodePatcher folder from the zip into any location, I will have it in `O:/NetcodePatcher` for this tutorial.
3. Move contents of `GameFolder/GameName_Data/Managed` into `NetcodePatcher/deps`

## Preparing mods for patching
To ensure that the patched NetworkBehaviours are initialized properly, add the following code snippet to your mod, in a place where it will only run once, such as `Awake()`
- **It is very important that it only runs once!**
	```cs
	var types = Assembly.GetExecutingAssembly().GetTypes();
	foreach (var type in types)
	{
	    var methods = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
	    foreach (var method in methods)
	    {
	        var attributes = method.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false);
	        if (attributes.Length > 0)
	        {
	            method.Invoke(null, null);
	        }
	    }
	}
	  ```

## Usage from command line

1. Take your compiled plugin, including any dependencies, and move it into `NetcodePatcher/plugins`.
	- You have to also include the plugin PDB file, the patcher requires this in order to work.
	- Plugins can be in sub directories, for example `NetcodePatcher/plugins/LethalThings/LethalThings.dll`
2. Open command line in plugin location, and run `NetcodePatcher.dll plugins/ deps/`
3. If everything went right, you should see `Patched (AssemblyName) successfully`
	- The patched assembly will replace the original in the NetcodePatcher plugins folder.

## Usage as a Post Build Event in VS

Example post build event:
```
xcopy "$(TargetPath)" "O:\NetcodePatcher\plugins\LethalThings" /Y
xcopy "$(TargetDir)$(AssemblyName).pdb" "O:\NetcodePatcher\plugins\LethalThings" /Y
cd O:\NetcodePatcher
NetcodePatcher.dll plugins/ deps/
xcopy "O:\NetcodePatcher\plugins\LethalThings\$(AssemblyName).dll" "C:\Program Files (x86)\Steam\steamapps\common\Lethal Company\BepInEx\plugins\LethalThings" /Y
```
Essentially what it is doing is copying the assembly and the pdb file from the output folder, and running the patcher.
Then copying the patched assembly to the plugins folder.

## Credits

- **nickklmao** 
	- for helping me test and find issues with the patcher.
