#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Serilog;
using Unity.CompilationPipeline.Common.Diagnostics;
using Unity.CompilationPipeline.Common.ILPostProcessing;
using Unity.Netcode.Editor.CodeGen;

namespace NetcodePatcher.CodeGen;

public class ReplacePatcherReferencesILPP : ILPostProcessor
{
    private readonly List<DiagnosticMessage> m_Diagnostics = [];
    private PostProcessorAssemblyResolver m_AssemblyResolver;

    public override ILPostProcessor GetInstance()
    {
        return this;
    }

    public override bool WillProcess(ICompiledAssembly compiledAssembly)
    {
        return true;
    }

    public override ILPostProcessResult? Process(ICompiledAssembly compiledAssembly)
    {
        if (!WillProcess(compiledAssembly)) return null;

        m_Diagnostics.Clear();

        #region Read

        var assemblyDefinition = CodeGenHelpers.AssemblyDefinitionFor(compiledAssembly, out m_AssemblyResolver);
        if (assemblyDefinition is null)
        {
            m_Diagnostics.AddError($"Cannot read assembly definition: {compiledAssembly.Name}");
            return null;
        }

        #endregion

        #region Process

        Log.Information("Now processing");

        var thisReference = assemblyDefinition.MainModule.AssemblyReferences
            .FirstOrDefault(reference => reference.Name == GetType().Assembly.GetName().Name);

        if (thisReference is not null)
        {
            assemblyDefinition.MainModule.AssemblyReferences.Remove(thisReference);
            Log.Information("Removed shit reference");
        }

        #endregion

        #region Write

        var pe = new MemoryStream();
        var pdb = new MemoryStream();

        var writerParameters = new WriterParameters
        {
            SymbolWriterProvider = new PortablePdbWriterProvider(),
            SymbolStream = pdb,
            WriteSymbols = true
        };

        assemblyDefinition.Write(pe, writerParameters);

        return new ILPostProcessResult(new InMemoryAssembly(pe.ToArray(), pdb.ToArray()), m_Diagnostics);

        #endregion
    }
}
