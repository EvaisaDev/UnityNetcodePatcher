# Unity Netcode Patcher

[![GitHub Build Status](https://img.shields.io/github/actions/workflow/status/EvaisaDev/UnityNetcodePatcher/build.yml?style=for-the-badge&logo=github)](https://github.com/EvaisaDev/UnityNetcodePatcher/actions/workflows/build.yml)
[![NetcodePatcher Nuget](https://img.shields.io/nuget/v/evaisa.netcodepatcher?style=for-the-badge&logo=nuget&label=Netcode%20Patcher)](https://www.nuget.org/packages/Evaisa.NetcodePatcher)
[![NetcodePatcher.MSBuild Nuget](https://img.shields.io/nuget/v/evaisa.netcodepatcher.cli?style=for-the-badge&logo=nuget&label=CLI)](https://www.nuget.org/packages/Evaisa.NetcodePatcher.Cli)
[![NetcodePatcher.Cli Nuget](https://img.shields.io/nuget/v/evaisa.netcodepatcher.msbuild?style=for-the-badge&logo=nuget&label=MSBuild)](https://www.nuget.org/packages/Evaisa.NetcodePatcher.MSBuild)


**This is an assembly patcher which replicates the IL Post Processing that unity does with its Netcode For GameObjects
package, allowing you to create custom `NetworkBehaviour` classes in mods as if you were doing it in a Unity project.**

- This was originally written for Lethal Company modding, only specific patcher combinations have been tested. YMMV.
- Please open an issue if you would like another game/patcher combination to be supported.

> [!IMPORTANT]
> This is a tool for **modders**.
> Mods should be patched before distribution; this tool should not be installed by end-users.

## Preparing mods for patching
- Make sure Debug Symbols are set to `Portable` or `Embedded` and not `Full`.
- To ensure that the patched NetworkBehaviours are initialized properly, add the following code snippet to your mod, in a place where it will only run once, such as `Awake()`
   - **It is quite important that it only runs once!**
   - If you have soft dependencies of some kind, you might need to wrap this in a try catch block.
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

   - The reason we need to do this is because NetcodePatcher generates methods marked with `[RuntimeInitializeOnLoadMethod]` for initializing the RPCs, which normally get ran when the class gets loaded.
    However, because the mod assembly is not managed by Unity, these methods will not run automatically.
    So using this snippet we manually run every method marked with `[RuntimeInitializeOnLoadMethodAttribute]`.
 - Make you register any custom NetworkObject prefabs with the unity NetworkManager.
	- e.g. `networkManager.GetComponent<NetworkManager>().AddNetworkPrefab(prefab)`;

## Usage

### CLI

The CLI is available as a .NET 9/10 tool. Install it using `dotnet`:

```bash
dotnet tool install -g Evaisa.NetcodePatcher.Cli
```

Then use the `netcode-patch` command to patch your plugin.

```bash
netcode-patch -uv 2022.3.62 -nv 1.12.2 -tv 1.0.0 [plugin] [dependencies]
```

- `plugin` should be the path to the patch target (your plugin assembly `.dll`)
- `dependencies` should be a path (or list of paths) containing all assemblies referenced by your project,
   the `Unity.Netcode.Runtime` assembly, etc. (e.g. the `Managed` folder of a game installation)

Run `netcode-patch --help` for usage information and available options.

### MSBuild

> [!IMPORTANT]
> Since Visual Studio 2022 uses 'full' MSBuild (which is based on .NET Framework), some dependencies targeting .NET Standard 2.1
> cannot be loaded into the build host process.
> Using the CLI tool and post build event is *required* if you are using Visual Studio 2022.
> NetcodePatcher's MSBuild SDK supports Visual Studio 2026.
> Update to Visual Studio 2026 if you wish to use the NetcodePatcher MSBuild SDK in Visual Studio.

NetcodePatcher has an MSBuild plugin that can be applied with minimal configuration.
Add the following snippet within the root `<Project>` tag of your `.csproj` project file
to automatically netcode patch the project's output assemblies.

```xml
<Sdk Name="Evaisa.NetcodePatcher.MSBuild" Version="4.*" />
<PropertyGroup>
  <NetcodePatcherUnityVersion>2022.3.62</NetcodePatcherUnityVersion>
  <NetcodePatcherNetcodeVersion>1.12.0</NetcodePatcherNetcodeVersion>
  <NetcodePatcherTransportVersion>1.0.0</NetcodePatcherTransportVersion>
</PropertyGroup>
<ItemGroup>
  <NetcodePatch Include="$(TargetPath)" />
</ItemGroup>
```

<details>
<summary>MSBuild advanced options</summary>

```xml
<Project>
  <PropertyGroup>
    // specify your game's Unity Editor/Runtime version
    <NetcodePatcherUnityVersion>2022.3.62</NetcodePatcherUnityVersion>
    // specify your game's Unity Netcode for GameObjects version (note not all versions are supported, notably 1.9.x through 1.11.x inclusive).
    <NetcodePatcherNetcodeVersion>1.12.0</NetcodePatcherNetcodeVersion>
    // specify your game's Unity Transport version (note for all v1.x versions, set 1.0.0)
    <NetcodePatcherTransportVersion>1.0.0</NetcodePatcherTransportVersion>
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

<details>
<summary>Usage with Visual Studio</summary>

If you want to support building in both environments (i.e. Visual Studio and `dotnet` SDK) you can use CLI tool for Visual Studio builds, with a `Condition="'$(MSBuildRuntimeType)' != 'Core'"`.

```xml
<Project>
  <ItemGroup>
    <!-- will be automatically skipped for Visual Studio -->
    <NetcodePatch Include="$(TargetPath)" />
  </ItemGroup>
  <PropertyGroup>
    <!-- silence the warning message that should have led you to this documentation -->
    <MSBuildWarningsAsMessages>$(MSBuildWarningsAsMessages);NCP0001</MSBuildWarningsAsMessages>
  </PropertyGroup>
  <Target Name="LegacyNetcodePatch" AfterTargets="PostBuildEvent" Condition="'$(MSBuildRuntimeType)' != 'Core'">
    <!-- run the CLI patcher only for MSBuilds that cannot load the dependencies -->
    <PropertyGroup>
        <NetcodePatcherDepsListFile>$(IntermediateOutputPath)ncp-deps.list</NetcodePatcherDepsListFile>
    </PropertyGroup>
    <WriteLinesToFile File="$(NetcodePatcherDepsListFile)" Lines="@(ReferencePathWithRefAssemblies)" Overwrite="true"/>
    <Exec Command="netcode-patch -uv 2022.3.62 -nv 1.12.2 -tv 1.0.0 &quot;$(TargetPath)&quot; --dependency-file &quot;$(NetcodePatcherDepsListFile)&quot;" />
  </Target>
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
   NetcodePatcher(.exe) -uv 2022.3.62 -nv 1.5.2 -tv 1.0.0 [plugins] [deps]
   ```

### Programmatic API

NetcodePatcher is also available programmatically.

Add the `dotnet-tools` NuGet source to your `NuGet.Config`:
```xml
<add key="dotnet-tools" value="https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-tools/nuget/v3/index.json" />
```

Then add a package reference to
`Evaisa.NetcodePatcher` to your `.csproj` project:

```xml
<ItemGroup>
  <PackageReference Include="Evaisa.NetcodePatcher" Version="4.*" />
</ItemGroup>
```

```csharp
using NetcodePatcher;

Patcher.Patch(string inputPath, string outputPath, string[] dependencyPaths);
```

## Usage as a Post Build Event

To ensure quotes are not escaped incorrectly, it is recommended you add this target by manually editing
your `.csproj` project file as opposed to using Visual Studio UI to add a post-build command.

> [!IMPORTANT]
> *if you installed the CLI tool locally instead of globally, you need to add `dotnet` infront of the command, so `dotnet netcode-patch`*

```xml
<Target Name="NetcodePatch" AfterTargets="PostBuildEvent">
    <PropertyGroup>
        <NetcodePatcherDepsListFile>$(IntermediateOutputPath)ncp-deps.list</NetcodePatcherDepsListFile>
    </PropertyGroup>
    <WriteLinesToFile File="$(NetcodePatcherDepsListFile)" Lines="@(ReferencePathWithRefAssemblies)" Overwrite="true"/>
    <Exec Command="netcode-patch -uv 2022.3.62 -nv 1.12.2 -tv 1.0.0 &quot;$(TargetPath)&quot; --dependency-file &quot;$(NetcodePatcherDepsListFile)&quot;" />
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
    <UnityEditorDir>$(ProgramFiles)/Unity/Hub/Editor/2022.3.62f2/Editor</UnityEditorDir>
  </PropertyGroup>
</Project>
```

## Credits

- **nickklmao** - for helping EvaisaDev test and find issues with the patcher.
- **[Lordfirespeed](https://github.com/Lordfirespeed)** - current maintainer.
