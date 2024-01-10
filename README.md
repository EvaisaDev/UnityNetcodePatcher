

# Unity Netcode Patcher
**This is an assembly patcher which replicates the IL Post Processing that unity does with it's Netcode For Gameobjects Package, allowing you to create custom NetworkBehaviours in mods as if you were doing it in a Unity project.**

- This was originally written for Lethal Company modding, and has only been tested with `com.unity.netcode.gameobjects@1.5.2`
  
*Note, this is intended to be a tool for modders, mods should be shipped after patching and this tool should not be installed by users.*

## Preparing mods for patching
- Make sure Debug Symbols is set to `Portable` and not embedded.
- To ensure that the patched NetworkBehaviours are initialized properly, add the following code snippet to your mod, in a place where it will only run once, such as `Awake()`
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
 - Make you register any custom NetworkObject prefabs with the unity NetworkManager.
	- networkManager.GetComponent<NetworkManager>().AddNetworkPrefab(prefab);

## Usage

### CLI

The CLI is available as a .NET 7/8 tool. Install it using `dotnet`:

```bash
dotnet tool install -g Evaisa.NetcodePatcher.Cli
```

Then use the `netcode-patch` command to patch your plugin. 

```bash
netcode-patch [plugin] [dependencies]
```

- `plugin` should be the path to the patch target (your plugin assembly `.dll`)
- `dependencies` should be a path (or list of paths) containing all assemblies referenced by your project,  
   the `Unity.Netcode.Runtime` assembly, etc. (e.g. the `Managed` folder of a game installation)

Run `netcode-patch --help` for usage information and available options.

### MSBuild

NetcodePatcher has an MSBuild plugin that can be applied with minimal configuration. 
Add the following snippet within the root `<Project>` tag of your `.csproj` project file 
to automatically netcode patch the project's output assemblies. 

```xml
<ItemGroup>
  <PackageReference Include="Evaisa.NetcodePatcher.MSBuild" Version="3.*" PrivateAssets="all" />
</ItemGroup>
<ItemGroup>
  <NetcodePatch Include="$(TargetPath)" />
</ItemGroup>
```

<details>
<summary>MSBuild options</summary>

```xml
<Project>
  <PropertyGroup>
    // Output to `[assembly]_patched.dll` instead of renaming original assembly
    <NetcodePatcherNoOverwrite>true</NetcodePatcherNoOverwrite>
    // Don't publicize in parallel 
    <NetcodePatcherDisableParallel>true</NetcodePatcherDisableParallel> 
  </PropertyGroup>

  <ItemGroup>
    <NetcodePatch Include="$(TargetPath)">
      // Override patched output path 
      <OutputPath>./bin/foo/bar</OutputPath>
    </NetcodePatch>
  </ItemGroup>
</Project>
```

</details>

### Manual

1. Download the latest [release](https://github.com/EvaisaDev/UnityNetcodePatcher/releases) asset for your platform.
2. Unpack the `.zip` archive to a memorable location
3. Copy-paste the contents of your game's `[game]_Data/Managed` directory into the extracted `deps` folder
4. Place your patch target plugins in the extracted `plugins` folder
5. Use the extracted executable file (assuming your CWD is the extracted directory):
   ```bash
   NetcodePatcher(.exe) plugins deps 
   ```

### Programmatic API

NetcodePatcher is also available programmatically. Just add a package reference to 
`Evaisa.NetcodePatcher` to your `.csproj` project:

```xml
<ItemGroup>
  <PackageReference Include="Evaisa.NetcodePatcher" Version="3.*" />
</ItemGroup>
```

```csharp
using NetcodePatcher;

Patcher.Patch(string inputPath, string outputPath, string[] dependencyPaths);
```

## Usage as a Post Build Event in VS

To ensure quotes are not escaped incorrectly, it is recommended you add this target by manually editing
your `.csproj` project file as opposed to using Visual Studio UI to add a post-build command.

```xml
<Target Name="NetcodePatch" AfterTargets="PostBuildEvent">
    <Exec Command="dotnet netcode-patch &quot;$(TargetPath)&quot; @(ReferencePathWithRefAssemblies->'&quot;%(Identity)&quot;', ' ')"/>
</Target>
```

## Contributing 

You will need to `git submodule update --init --recursive` to fetch submodules, 
and create a `.csproj.user` file to tell the `NetcodePatcher` plugin where Unity Editor is installed.

### Template `NetcodePatcher/NetcodePatcher.csproj.user`

```xml
<?xml version="1.0" encoding="utf-8"?>
<Project>
  <PropertyGroup>
    <UnityEditorDir>$(ProgramFiles)/Unity/Hub/Editor/2022.3.9f1/Editor</UnityEditorDir>
  </PropertyGroup>
</Project>
```

## Credits

- **nickklmao** 
	- for helping me test and find issues with the patcher.
- **[Lordfirespeed](https://github.com/Lordfirespeed)**
