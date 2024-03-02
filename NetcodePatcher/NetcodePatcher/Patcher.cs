using System;
using System.IO;
using JetBrains.Annotations;
using NetcodePatcher.CodeGen;
using Serilog;

namespace NetcodePatcher;

public static class Patcher
{
    private static readonly string[] AssemblyNameBlacklist = [
        "Unity.Netcode.Runtime",
        "UnityEngine.CoreModule",
        "Unity.Netcode.Components",
        "Unity.Networking.Transport",
        "Assembly-CSharp",
        "ClientNetworkTransform",
    ];

    [UsedImplicitly]
    public static void Patch(string assemblyPath, string outputPath, string[] references)
    {
        if (assemblyPath.ToLower().Contains("mmhook"))
        {
            Log.Warning("Skipping {FileName} as it appears to be a MonoMod hooks file", Path.GetFileName(assemblyPath));
            return;
        }

        // check if contains blacklisted phrases
        foreach (string blacklisted in AssemblyNameBlacklist)
        {
            if (!assemblyPath.ToLowerInvariant().Contains(blacklisted.ToLowerInvariant())) continue;

            Log.Warning("Skipping {FileName} as it contains a blacklisted phrase '{Phrase}'", Path.GetFileName(assemblyPath), blacklisted);
        }

        try
        {
            string FormatWhitespace(string input) => input.Replace("||  ", "\r\n").Replace("||", " ");

            void OnWarning(string warning)
            {
                Log.Warning($"Warning when patching ({Path.GetFileName(assemblyPath)}): {FormatWhitespace(warning)}");
            }

            void OnError(string error)
            {
                throw new Exception(FormatWhitespace(error));
            }

            var applicator = new NetcodeILPPApplicator(assemblyPath, outputPath, references)
            {
                OnWarning = OnWarning,
                OnError = OnError,
            };
            applicator.ApplyProcesses();
        }
        catch (Exception exception)
        {
            Log.Error($"Failed to patch ({Path.GetFileName(assemblyPath)}): {exception}");
            throw;
        }
    }
}
