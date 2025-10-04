using System;
using System.Collections.Generic;
using System.Linq;
using Cake.Common;
using Cake.Common.IO;
using Cake.Common.Tools.DotNet;
using Cake.Common.Tools.DotNet.MSBuild;
using Cake.Common.Tools.DotNet.Publish;
using Cake.Core;
using Cake.Core.IO;
using Cake.Frosting;
using Cake.Git;
using NuGet.Packaging;

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

    public DirectoryPath PatcherProjectDirectory => RootDirectory.Combine("NetcodePatcher");
    public FilePath PatcherProjectFile => PatcherProjectDirectory.CombineWithFilePath("NetcodePatcher.csproj");

    public DirectoryPath NetcodeRuntimeProjectDirectory => RootDirectory.Combine("Unity.Netcode.Runtime");
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

    public IEnumerable<string> ComputeUnityVersionConstants()
    {
        if (UnityVersion < new Version(2021, 1, 0))
            throw new ArgumentOutOfRangeException(nameof(UnityVersion), "Unity version must be >=2020.1.0.");

        var versionConstants = new LinkedList<string>();

        for (int major = 2020; major < UnityVersion.Major; major++) {
            for (int minor = 1; minor <= 4; minor++) {
                versionConstants.AddLast(VersionOrNewerConstant(major, minor));
            }
        }

        for (int minor = 1; minor <= UnityVersion.Minor; minor++) {
            versionConstants.AddLast(VersionOrNewerConstant(UnityVersion.Major, minor));
        }

        versionConstants.AddRange(VersionConstants(UnityVersion.Major, UnityVersion.Minor, UnityVersion.Build));
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

    public IEnumerable<string> ComputeUnityTransportConstants()
    {
        var versionConstants = new LinkedList<string>();
        if (UnityTransportVersion is { Major: >= 2 }) versionConstants.AddLast("UTP_TRANSPORT_2_0_ABOVE");
        if (UnityTransportVersion is { Major: >= 2, Minor: >= 1 }) versionConstants.AddLast("UTP_TRANSPORT_2_1_ABOVE");
        if (UnityTransportVersion is { Major: >= 2, Minor: >= 4 }) versionConstants.AddLast("UTP_TRANSPORT_2_4_ABOVE");
        return versionConstants;
    }

    public IEnumerable<string> ComputeUnityNetcodeNativeCollectionSupportConstants()
    {
        if (UnityNetcodeNativeCollectionSupport) return ["UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT"];
        return [];
    }

    public IEnumerable<string> ComputeAllMSBuildConstants()
    {
        return ComputeUnityVersionConstants()
            .Concat(ComputeUnityTransportConstants())
            .Concat(ComputeUnityNetcodeNativeCollectionSupportConstants())
            .Concat(["UNITY_EDITOR", "UNITY_INCLUDE_TESTS"]);
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
public sealed class CompilePatcherTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        var buildSettings = new DotNetPublishSettings {
            Configuration = "Release",
            OutputDirectory = context.PatcherCommonOutputDirectory,
            MSBuildSettings = new() {
                Properties = {
                    {"DefineConstants", [ string.Join("%3B", context.ComputeAllMSBuildConstants().ToArray()) ] },
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
