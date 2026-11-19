using System.Reflection;
using System.Reflection.Emit;

namespace gmdTest.Fixtures;

// The types that types of gmd use, read from the compiled code rather than the sources, since C# finds
// a type without its namespace being named: every type a type names in its signatures, its attributes
// and the bodies of its methods, and the type arguments of each. For the tests that keep a layer's
// boundary, GitNamespaceTest and CuiLayerTest.
static class TypeUses
{
    // Each use, as the type that uses and the type used, once per pair
    public static IEnumerable<(Type By, Type Used)> Of(IEnumerable<Type> types) =>
        types
            .SelectMany(t => UsedTypes(t).Select(used => (By: t, Used: used)))
            .SelectMany(u => WithTypeArguments(u.Used).Select(used => (u.By, Used: used)))
            .Distinct();

    // The types of gmd in a namespace or below it
    public static IEnumerable<Type> In(string ns) =>
        typeof(gmd.Git.Private.Git).Assembly.GetTypes().Where(t => IsIn(t, ns));

    public static bool IsIn(Type type, string ns) =>
        type.Namespace == ns || type.Namespace?.StartsWith($"{ns}.") == true;

    public static string Describe((Type By, Type Used) use) =>
        $"{use.By.FullName} uses {use.Used.FullName ?? use.Used.Name}";

    // A type, or an array, pointer or generic of others, as the types it is made of
    static IEnumerable<Type> WithTypeArguments(Type type)
    {
        if (type.HasElementType)
            return WithTypeArguments(type.GetElementType()!);
        if (!type.IsGenericType)
            return [type];
        return type.GetGenericArguments().SelectMany(WithTypeArguments).Prepend(type.GetGenericTypeDefinition());
    }

    const BindingFlags Declared =
        BindingFlags.Public
        | BindingFlags.NonPublic
        | BindingFlags.Instance
        | BindingFlags.Static
        | BindingFlags.DeclaredOnly;

    static IEnumerable<Type> UsedTypes(Type type)
    {
        if (type.BaseType != null)
            yield return type.BaseType;
        foreach (var i in type.GetInterfaces())
            yield return i;
        foreach (var a in type.GetCustomAttributesData())
            yield return a.AttributeType;
        foreach (var f in type.GetFields(Declared))
            yield return f.FieldType;
        foreach (var p in type.GetProperties(Declared))
            yield return p.PropertyType;

        IEnumerable<MethodBase> methods = [.. type.GetMethods(Declared), .. type.GetConstructors(Declared)];
        foreach (var m in methods)
        {
            if (m is MethodInfo mi)
                yield return mi.ReturnType;
            foreach (var p in m.GetParameters())
                yield return p.ParameterType;
            foreach (var used in TypesInBody(m))
                yield return used;
        }
    }

    // The types an IL body names: those of the methods it calls, the fields it reads and writes, and
    // the types it makes, casts to or loads
    static IEnumerable<Type> TypesInBody(MethodBase method)
    {
        var il = method.GetMethodBody()?.GetILAsByteArray() ?? [];
        var typeArguments =
            method.DeclaringType?.IsGenericType == true ? method.DeclaringType.GetGenericArguments() : null;
        var methodArguments = method.IsGenericMethod ? method.GetGenericArguments() : null;

        for (var i = 0; i < il.Length; )
        {
            var op = il[i] == 0xfe ? TwoByteOpCodes[il[i + 1]] : OneByteOpCodes[il[i]];
            i += op.Size;
            if (
                op.OperandType
                is OperandType.InlineMethod
                    or OperandType.InlineField
                    or OperandType.InlineType
                    or OperandType.InlineTok
            )
            {
                foreach (
                    var used in Resolve(method.Module, BitConverter.ToInt32(il, i), typeArguments, methodArguments)
                )
                    yield return used;
            }
            i += OperandSize(op, il, i);
        }

        foreach (var local in method.GetMethodBody()?.LocalVariables ?? [])
            yield return local.LocalType;
    }

    // A token as the types it names: a type itself, or a member's declaring type and the types of its
    // signature, with a generic method's type arguments, so that a call naming a type only there, e.g.
    // Resolve<IGit>(), or a field read whose type goes no further, is a use too
    static IEnumerable<Type> Resolve(Module module, int token, Type[]? typeArguments, Type[]? methodArguments)
    {
        MemberInfo? member;
        try
        {
            member = module.ResolveMember(token, typeArguments, methodArguments);
        }
        catch (ArgumentException)
        {
            return []; // A member of a generic the context does not resolve, e.g. in a lambda's closure
        }

        return member switch
        {
            Type t => [t],
            FieldInfo f => [f.DeclaringType!, f.FieldType],
            MethodBase m =>
            [
                m.DeclaringType!,
                .. m is MethodInfo mi ? [mi.ReturnType] : Array.Empty<Type>(),
                .. m.GetParameters().Select(p => p.ParameterType),
                .. m.IsGenericMethod ? m.GetGenericArguments() : [],
            ],
            MemberInfo m when m.DeclaringType != null => [m.DeclaringType],
            _ => [],
        };
    }

    static int OperandSize(OpCode op, byte[] il, int i) =>
        op.OperandType switch
        {
            OperandType.InlineNone => 0,
            OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
            OperandType.InlineVar => 2,
            OperandType.InlineI8 or OperandType.InlineR => 8,
            OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(il, i),
            _ => 4,
        };

    static readonly OpCode[] OneByteOpCodes = new OpCode[0x100];
    static readonly OpCode[] TwoByteOpCodes = new OpCode[0x100];

    static TypeUses()
    {
        foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            var op = (OpCode)field.GetValue(null)!;
            var value = (ushort)op.Value;
            if (op.Size == 1)
                OneByteOpCodes[value] = op;
            else
                TwoByteOpCodes[value & 0xff] = op;
        }
    }
}
