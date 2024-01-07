using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        var patchedAttributeDefinition = new TypeDefinition(
            $"{assemblyDefinition.Name.Name}.{AttributeNamespaceSuffix}",
            AttributeName,
            TypeAttributes.NestedPrivate,
            assemblyDefinition.MainModule.ImportReference(typeof(Attribute))
        );

        var attributeUsageAttributeConstructor =
            assemblyDefinition.MainModule.ImportReference(
                typeof(AttributeUsageAttribute).GetConstructor([typeof(AttributeTargets)])
            );
        var attributeUsageAttribute = new CustomAttribute(attributeUsageAttributeConstructor);
        attributeUsageAttribute.ConstructorArguments.Add(
            new CustomAttributeArgument(assemblyDefinition.MainModule.ImportReference(typeof(AttributeTargets)), AttributeTargets.Assembly)
        );
        patchedAttributeDefinition.CustomAttributes.Add(attributeUsageAttribute);
        
        var methodAttributes = MethodAttributes.Assembly | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName;
        var method = new MethodDefinition(".ctor", methodAttributes, assemblyDefinition.MainModule.TypeSystem.Void);
        method.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
        var baseCtorReference = new MethodReference(".ctor", assemblyDefinition.MainModule.TypeSystem.Void, patchedAttributeDefinition.BaseType){HasThis = true};
        method.Body.Instructions.Add(Instruction.Create(OpCodes.Call, baseCtorReference));
        method.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        patchedAttributeDefinition.Methods.Add(method);
        
        assemblyDefinition.MainModule.Types.Add(patchedAttributeDefinition);
        
        var attributeConstructor = assemblyDefinition.MainModule
            .ImportReference(patchedAttributeDefinition) 
            .Resolve()
            .GetConstructors()
            .First();
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