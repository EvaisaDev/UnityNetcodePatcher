using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;

namespace NetcodePatcher.Cli.Extensions;

public static class NoUncArgumentExtension
{
    public static Argument<FileSystemInfo> NoUnc(this Argument<FileSystemInfo> argument)
    {
        argument.AddValidator(IsNotUnc);
        return argument;
    }
    
    public static Argument<T> NoUnc<T>(this Argument<T> argument) where T : IEnumerable<FileSystemInfo>
    {
        argument.AddValidator(IsNotUnc);
        return argument;
    }

    private static void IsNotUnc(ArgumentResult result)
    {
        foreach (var token in result.Tokens)
        {
            if (!token.Value.StartsWith(@"\\")) continue;
            result.ErrorMessage = "Argument cannot accept universal name specifiers - must provide local file/directory path.";
            return;
        }
    }
}