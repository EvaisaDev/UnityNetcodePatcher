using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Cake.Common;
using Cake.Common.IO;
using Cake.Common.Tools.DotNet;
using Cake.Common.Tools.DotNet.MSBuild;
using Cake.Common.Tools.DotNet.Publish;
using Cake.Core;
using Cake.Core.IO;
using Cake.Frosting;
using Cake.Git;
using NetcodePatcher.Build.SymbolResolution;
using NuGet.Packaging;
using NuGet.Versioning;

namespace NetcodePatcher.Build;

public static class Program
{
    public static int Main(string[] args)
    {
        return new CakeHost()
            .UseContext<BuildContext>()
            .Run(args);
    }
}

public class BuildContext : FrostingContext
{
    public DirectoryPath RootDirectory { get; }

    public DirectoryPath NetcodeSubmoduleDirectory => RootDirectory.Combine("submodules/com.unity.netcode.gameobjects");

    public DirectoryPath UnityProjectDirectory => RootDirectory.Combine("UnityProject");
    public FilePath UnityProjectVersionFile =>
        UnityProjectDirectory.CombineWithFilePath("ProjectSettings/ProjectVersion.txt");
    public FilePath UnityProjectPackageManifestFile =>
        UnityProjectDirectory.CombineWithFilePath("Packages/manifest.json");
    public FilePath UnityProjectPackageLockFile =>
        UnityProjectDirectory.CombineWithFilePath("Packages/packages-lock.json");

    public DirectoryPath PatcherProjectDirectory => RootDirectory.Combine("NetcodePatcher");
    public FilePath PatcherProjectFile => PatcherProjectDirectory.CombineWithFilePath("NetcodePatcher.csproj");

    public DirectoryPath NetcodeRuntimeProjectDirectory => RootDirectory.Combine("Unity.Netcode.Runtime");

    public FilePath[] NetcodeRuntimeAsmDefFiles => [
        NetcodeRuntimeProjectDirectory.CombineWithFilePath("Unity/Netcode/Runtime/com.unity.netcode.runtime.asmdef"),
        NetcodeRuntimeProjectDirectory.CombineWithFilePath("Unity/Netcode/Runtime/Unity.Netcode.Runtime.asmdef"),
    ];
    public FilePath NetcodeRuntimeAsmDefFile => NetcodeRuntimeAsmDefFiles.First((file) => File.Exists(file.FullPath));
    public FilePath NetcodeRuntimeProjectFile => NetcodeRuntimeProjectDirectory.CombineWithFilePath("Unity.Netcode.Runtime.csproj");

    public Version UnityVersion { get; }
    public Version UnityNetcodeVersion { get; }
    public Version UnityTransportVersion { get; }
    public bool UnityNetcodeNativeCollectionSupport { get; }

    public DirectoryPath PatcherCommonOutputDirectory => PatcherProjectDirectory
        .Combine("dist")
        .Combine($"unity-v{UnityVersion}")
        .Combine($"unity-transport-v{UnityTransportVersion}");

    public DirectoryPath PatcherSpecificOutputDirectory => PatcherCommonOutputDirectory
        .Combine($"netcode-v{UnityNetcodeVersion}")
        .Combine(UnityNetcodeNativeCollectionSupport ? "with-native-collection-support" : "without-native-collection-support");

    public string[] MSBuildConstants = null!;

    public DirectoryPath? UnityEditorDir { get; }

    public BuildContext(ICakeContext context)
        : base(context)
    {
        UnityVersion = context.Argument<Version>("unity-version", new Version(2022, 3, 9));
        UnityNetcodeVersion = context.Argument<Version>("netcode-version", new Version(1, 5, 2));
        UnityTransportVersion = context.Argument<Version>("transport-version", new Version(1, 0, 0));
        UnityNetcodeNativeCollectionSupport = context.Argument<bool>("native-collection-support", false);
        UnityEditorDir = context.Argument<DirectoryPath?>("unity-editor-dir", null);

        RootDirectory = context.Environment.WorkingDirectory.GetParent();
    }
}

[TaskName("GatherConstants")]
public sealed class GatherConstantsTask : AsyncFrostingTask<BuildContext>
{
    public IEnumerable<string> ComputeUnityVersionConstants(BuildContext ctx)
    {
        if (ctx.UnityVersion < new Version(2021, 1, 0))
            throw new ArgumentOutOfRangeException(nameof(UnityVersion), "Unity version must be >=2020.1.0.");

        var versionConstants = new LinkedList<string>();

        for (int major = 2020; major < Math.Min(2024, ctx.UnityVersion.Major); major++) {
            for (int minor = 1; minor <= 4; minor++) {
                versionConstants.AddLast(VersionOrNewerConstant(major, minor));
            }
        }
        for (int major = 6000; major < ctx.UnityVersion.Major; major++) {
            for (int minor = 0; minor <= 4; minor++) {
                versionConstants.AddLast(VersionOrNewerConstant(major, minor));
            }
        }
        for (int minor = 0; minor <= ctx.UnityVersion.Minor; minor++) {
            if (minor == 0 && ctx.UnityVersion.Major < 6000) continue;
            versionConstants.AddLast(VersionOrNewerConstant(ctx.UnityVersion.Major, minor));
        }

        versionConstants.AddRange(VersionConstants(ctx.UnityVersion.Major, ctx.UnityVersion.Minor, ctx.UnityVersion.Build));
        return versionConstants;

        string VersionOrNewerConstant(int major, int minor) => $"UNITY_{major}_{minor}_OR_NEWER";
        IEnumerable<string> VersionConstants(int major, int minor, int build) {
            if (minor < 0 && build < 0)
                return [MajorConstant()];

            if (build < 0)
                return [MajorConstant(), MajorMinorConstant()];

            return [MajorConstant(), MajorMinorConstant(), MajorMinorBuildConstant()];

            string MajorConstant() => $"UNITY_{major}";
            string MajorMinorConstant() => $"UNITY_{major}_{minor}";
            string MajorMinorBuildConstant() => $"UNITY_{major}_{minor}_{build}";
        };
    }

    public IEnumerable<string> ComputeUnityNetcodeNativeCollectionSupportConstants(BuildContext ctx)
    {
        if (ctx.UnityNetcodeNativeCollectionSupport) return ["UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT"];
        return [];
    }

    public async Task<Dictionary<string, NuGetVersion>> ReadPackageVersions(BuildContext context)
    {
        await using var openLockfileStream = File.OpenRead(context.UnityProjectPackageLockFile.FullPath);
        var lockFile = await JsonSerializer.DeserializeAsync<UnityPackagesLock>(openLockfileStream);
        return lockFile!.Dependencies.ToDictionary(entry => entry.Key, entry => entry.Value.Version);
    }

    public async Task<IEnumerable<AsmDefVersionDefine>> ReadVersionDefines(BuildContext context)
    {
        await using var openAsmDefStream = File.OpenRead(context.NetcodeRuntimeAsmDefFile.FullPath);
        var asmDef = await JsonSerializer.DeserializeAsync<AsmDef>(openAsmDefStream);
        return asmDef!.VersionDefines;
    }

    public async Task<IEnumerable<string>> ResolveAsmDefConstants(BuildContext context)
    {
        var unityVersion = UnityVersion.Parse("6000.4.1f1"); // todo: replace with context member
        var packageVersions = await ReadPackageVersions(context);
        LinkedList<string> constants = new();
        foreach (var versionDefine in await ReadVersionDefines(context)) {
            if (versionDefine.ResourceIsPackage) {
                if (!packageVersions.TryGetValue(versionDefine.ResourceName, out var version)) continue;
                if (!versionDefine.PackageVersionRange.Satisfies(version)) continue;
                constants.AddLast(versionDefine.DefineSymbol);
                continue;
            }
            if (versionDefine.ResourceIsUnity) {
                if (!versionDefine.UnityVersionRange.Satisfies(unityVersion)) continue;
                constants.AddLast(versionDefine.DefineSymbol);
                continue;
            }
        }
        return constants;
    }

    public override async Task RunAsync(BuildContext ctx)
    {
        var asmDefConstants = await ResolveAsmDefConstants(ctx);

        ctx.MSBuildConstants = ComputeUnityVersionConstants(ctx)
            .Concat(ComputeUnityNetcodeNativeCollectionSupportConstants(ctx))
            .Concat(asmDefConstants)
            .Concat(["UNITY_EDITOR", "UNITY_INCLUDE_TESTS"])
            .ToArray();
    }
}

[TaskName("Clean")]
public sealed class CleanTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        CleanProject(context.PatcherProjectDirectory);
        CleanProject(context.NetcodeRuntimeProjectDirectory);
        return;

        void CleanProject(DirectoryPath projectDirectory)
        {
            context.CleanDirectories(projectDirectory.Combine("bin").FullPath);
            context.CleanDirectories(projectDirectory.Combine("obj").FullPath);
            context.CleanDirectories(projectDirectory.Combine("dist").FullPath);
        }
    }
}

[TaskName("Checkout Netcode Release")]
public sealed class CheckoutNetcodeReleaseTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        context.GitCheckout(context.NetcodeSubmoduleDirectory, $"release/{context.UnityNetcodeVersion}");
    }
}

[TaskName("Compile Patcher")]
[IsDependentOn(typeof(CleanTask))]
[IsDependentOn(typeof(CheckoutNetcodeReleaseTask))]
[IsDependentOn(typeof(GatherConstantsTask))]
public sealed class CompilePatcherTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        var buildSettings = new DotNetPublishSettings {
            Configuration = "Release",
            OutputDirectory = context.PatcherCommonOutputDirectory,
            MSBuildSettings = new() {
                Properties = {
                    {"DefineConstants", [ string.Join("%3B", context.MSBuildConstants) ] },
                },
            },
        };

        if (context.UnityEditorDir is not null)
        {
            buildSettings.MSBuildSettings = buildSettings.MSBuildSettings
                .WithProperty("UnityEditorDir", context.UnityEditorDir.FullPath);
        }

        context.DotNetPublish(context.PatcherProjectFile.FullPath, buildSettings);

        context.EnsureDirectoryExists(context.PatcherSpecificOutputDirectory);
        MoveFileToSpecificOutputDirectory(CommonOutputFilePath("NetcodePatcher.deps.json"));
        MoveAssemblyToSpecificOutputDirectory("NetcodePatcher");
        MoveAssemblyToSpecificOutputDirectory("Unity.Netcode.Runtime");
        return;

        void MoveAssemblyToSpecificOutputDirectory(string assemblyName)
        {
            MoveFileToSpecificOutputDirectory(CommonOutputFilePath($"{assemblyName}.dll"));
            MoveFileToSpecificOutputDirectory(CommonOutputFilePath($"{assemblyName}.pdb"));
        }

        void MoveFileToSpecificOutputDirectory(FilePath file)
        {
            context.MoveFileToDirectory(file, context.PatcherSpecificOutputDirectory);
        }

        FilePath CommonOutputFilePath(string outputFileName)
        {
            return context.PatcherCommonOutputDirectory.CombineWithFilePath(outputFileName);
        }
    }
}

[TaskName("Default")]
[IsDependentOn(typeof(CompilePatcherTask))]
public class DefaultTask : FrostingTask { }
