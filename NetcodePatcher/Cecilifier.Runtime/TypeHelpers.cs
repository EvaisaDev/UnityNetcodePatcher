// https://github.com/adrianoc/cecilifier/blob/main/Cecilifier.Runtime/TypeHelpers.cs

using System.Linq;
using Mono.Cecil;

namespace Cecilifier.Runtime;

public class TypeHelpers
{
    public static MethodReference DefaultCtorFor(TypeReference type)
    {
        var resolved = type.Resolve();
        if (resolved == null)
            return null;

        var ctor = resolved.Methods.SingleOrDefault(m => m.IsConstructor && m.Parameters.Count == 0 && !m.IsStatic);
        if (ctor == null)
            return DefaultCtorFor(resolved.BaseType);

        return new MethodReference(".ctor", type.Module.TypeSystem.Void, type) { HasThis = true };
    }
}
