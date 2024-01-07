using System;
using System.Collections.Generic;
using System.IO;
using Mono.Cecil;
using Mono.Cecil.Cil;
using NetcodePatcher.Attributes;
using Unity.CompilationPipeline.Common.Diagnostics;
using Unity.CompilationPipeline.Common.ILPostProcessing;
using Unity.Netcode.Editor.CodeGen;

namespace NetcodePatcher.CodeGen;

public class ApplyPatchedAttributeILPP : ILPostProcessor
{
    public override ILPostProcessor GetInstance() => this;

    public override bool WillProcess(ICompiledAssembly compiledAssembly) => true;

    private readonly List<DiagnosticMessage> m_Diagnostics = [];
    private PostProcessorAssemblyResolver m_AssemblyResolver;

    public override ILPostProcessResult? Process(ICompiledAssembly compiledAssembly)
    {
        if (!WillProcess(compiledAssembly)) return null;
        
        m_Diagnostics.Clear();
        
        // read 
        var assemblyDefinition = CodeGenHelpers.AssemblyDefinitionFor(compiledAssembly, out m_AssemblyResolver);
        if (assemblyDefinition == null)
        {
            m_Diagnostics.AddError($"Cannot read assembly definition: {compiledAssembly.Name}");
            return null;
        }
        
        // do stuff
        var attributeConstructor =
            assemblyDefinition.MainModule.ImportReference(
                typeof(NetcodePatchedAssemblyAttribute).GetConstructor(Type.EmptyTypes));
        var attribute = new CustomAttribute(attributeConstructor);
        assemblyDefinition.CustomAttributes.Add(attribute);
        
        // write
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
    }
}