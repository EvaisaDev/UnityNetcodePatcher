using System;
using System.Collections.Generic;
using System.IO;
using Cecilifier.Runtime;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Unity.CompilationPipeline.Common.Diagnostics;
using Unity.CompilationPipeline.Common.ILPostProcessing;
using Unity.Netcode.Editor.CodeGen;
using MethodAttributes = Mono.Cecil.MethodAttributes;
using TypeAttributes = Mono.Cecil.TypeAttributes;

namespace NetcodePatcher.CodeGen;

public class ApplyPatchedAttributeILPP : ILPostProcessor
{
    public static readonly string AttributeNamespaceSuffix = "NetcodePatcher";

    public static readonly string AttributeName = "NetcodePatchedAssemblyAttribute";
    
    public override ILPostProcessor GetInstance() => this;

    public override bool WillProcess(ICompiledAssembly compiledAssembly) => true;

    private readonly List<DiagnosticMessage> m_Diagnostics = [];
    private PostProcessorAssemblyResolver m_AssemblyResolver;
    
    // This function's implementation was written with the help of https://cecilifier.me/
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

        // Class : NetcodePatchedAttribute
        var cls_NetcodePatchedAttribute = new TypeDefinition(
            $"{assemblyDefinition.Name.Name}.{AttributeNamespaceSuffix}",
            AttributeName,
            TypeAttributes.AnsiClass | TypeAttributes.BeforeFieldInit | TypeAttributes.NotPublic,
            assemblyDefinition.MainModule.ImportReference(typeof(Attribute))
        );
        assemblyDefinition.MainModule.Types.Add(cls_NetcodePatchedAttribute);
        
        // Add AttributeUsage(AttributeTargets.Assembly) to NetcodePatchedAttribute
        var attr_AttributeUsage = new CustomAttribute(assemblyDefinition.MainModule.ImportReference(typeof(AttributeUsageAttribute).GetConstructor([typeof(AttributeTargets)])));
        attr_AttributeUsage.ConstructorArguments.Add(new CustomAttributeArgument(assemblyDefinition.MainModule.ImportReference(typeof(AttributeTargets)), 4));
        cls_NetcodePatchedAttribute.CustomAttributes.Add(attr_AttributeUsage);
        
        // Method : NetcodePatchedAttribute.ctor
        var ctor_NetcodePatchedAttribute = new MethodDefinition(".ctor", MethodAttributes.Assembly | MethodAttributes.RTSpecialName | MethodAttributes.SpecialName | MethodAttributes.HideBySig, assemblyDefinition.MainModule.TypeSystem.Void);
        cls_NetcodePatchedAttribute.Methods.Add(ctor_NetcodePatchedAttribute);
        ctor_NetcodePatchedAttribute.Body.InitLocals = true;
        var il_ctor_NetcodePatchedAttribute = ctor_NetcodePatchedAttribute.Body.GetILProcessor();
        il_ctor_NetcodePatchedAttribute.Emit(OpCodes.Ldarg_0);
        il_ctor_NetcodePatchedAttribute.Emit(OpCodes.Call, assemblyDefinition.MainModule.ImportReference(TypeHelpers.DefaultCtorFor(cls_NetcodePatchedAttribute.BaseType)));
        il_ctor_NetcodePatchedAttribute.Emit(OpCodes.Ret);
        
        // Add NetcodePatchedAttribute to assembly definition
        var attribute = new CustomAttribute(assemblyDefinition.MainModule.ImportReference(TypeHelpers.DefaultCtorFor(cls_NetcodePatchedAttribute)));
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