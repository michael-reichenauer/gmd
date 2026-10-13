using System.Reflection;
using System.Reflection.Emit;

namespace gmdTest.Git;

// gmd.Git is meant to stay a general git library, which could be one of its own: it uses nothing of
// gmd but itself and gmd.Utils (Result, Cmd, Log and small helpers), and nothing beyond .NET. The
// layers above know it, never the other way round, and the server's converters are the boundary
// between git's model and gmd's.
//
// Checked in the compiled code rather than the sources, since C# finds a type without its namespace
// being named, e.g. one in the namespace 'gmd' itself: every type a type of gmd.Git names, in its
// signatures, its attributes and the bodies of its methods.
[TestClass]
public class GitNamespaceTest
{
    [TestMethod]
    public void TestGmdGitUsesNothingButItselfGmdUtilsAndDotNet()
    {
        var gitTypes = typeof(gmd.Git.Private.Git).Assembly.GetTypes().Where(t => IsIn(t, "gmd.Git"));

        var uses = gitTypes
            .SelectMany(t => UsedTypes(t).Select(used => (By: t, Used: used)))
            .SelectMany(u => WithTypeArguments(u.Used).Select(used => (u.By, Used: used)))
            .Where(u => !IsAllowed(u.Used))
            .Select(u => $"{u.By.FullName} uses {u.Used.FullName ?? u.Used.Name}")
            .Distinct()
            .Order()
            .ToList();

        Assert.AreEqual("", string.Join("\n", uses));
    }

    static bool IsAllowed(Type type) =>
        type.IsGenericParameter
        || IsIn(type, "gmd.Git")
        || IsIn(type, "gmd.Utils")
        || IsIn(type, "System")
        || IsIn(type, "Microsoft")
        || (type.Namespace == null && type.Name.StartsWith('<')); // Made by the compiler

    static bool IsIn(Type type, string ns) => type.Namespace == ns || type.Namespace?.StartsWith($"{ns}.") == true;

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
                if (Resolve(method.Module, BitConverter.ToInt32(il, i), typeArguments, methodArguments) is Type used)
                    yield return used;
            }
            i += OperandSize(op, il, i);
        }
    }

    static Type? Resolve(Module module, int token, Type[]? typeArguments, Type[]? methodArguments)
    {
        try
        {
            return module.ResolveMember(token, typeArguments, methodArguments) switch
            {
                Type t => t,
                MemberInfo m => m.DeclaringType,
                _ => null,
            };
        }
        catch (ArgumentException)
        {
            return null; // A member of a generic the context does not resolve, e.g. in a lambda's closure
        }
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

    static GitNamespaceTest()
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
