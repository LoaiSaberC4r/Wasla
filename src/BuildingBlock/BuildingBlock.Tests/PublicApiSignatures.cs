using System.Reflection;

namespace BuildingBlock.Tests;

// Deterministic managed public signatures, shared by the frozen-main capture and
// the compatibility assertion. Capture provenance is recorded in each baseline.
internal static class PublicApiSignatures
{
    internal static string[] Read(Assembly assembly) => assembly.GetExportedTypes()
        .SelectMany(Signatures).Order(StringComparer.Ordinal).ToArray();

    internal static string TypeName(Type type)
    {
        if (type.IsByRef) return TypeName(type.GetElementType()!) + "&";
        if (type.IsPointer) return TypeName(type.GetElementType()!) + "*";
        if (type.IsArray) return TypeName(type.GetElementType()!) + "[" + new string(',', type.GetArrayRank() - 1) + "]";
        if (type.IsGenericParameter) return type.Name;
        var name = type.Name; var tick = name.IndexOf('`');
        if (tick >= 0) name = name[..tick] + "<" + string.Join(", ", type.GetGenericArguments().Select(TypeName)) + ">";
        return type.DeclaringType is null ? type.Namespace + "." + name : TypeName(type.DeclaringType) + "." + name;
    }

    private static IEnumerable<string> Signatures(Type type)
    {
        var name = TypeName(type);
        yield return name;
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        foreach (var member in type.GetMembers(flags))
        {
            switch (member)
            {
                case ConstructorInfo constructor:
                    yield return name + ".ctor(" + Parameters(constructor.GetParameters()) + ") -> System.Void";
                    break;
                case MethodInfo method when !method.IsSpecialName || method.Name.StartsWith("op_", StringComparison.Ordinal):
                    var generic = method.IsGenericMethod ? "<" + string.Join(", ", method.GetGenericArguments().Select(TypeName)) + ">" : "";
                    yield return name + "." + method.Name + generic + "(" + Parameters(method.GetParameters()) + ") -> " + TypeName(method.ReturnType);
                    break;
                case PropertyInfo property:
                    var index = property.GetIndexParameters();
                    var suffix = index.Length > 0 ? "[" + Parameters(index) + "]" : "";
                    yield return name + "." + property.Name + suffix + " { " + (property.GetMethod?.IsPublic == true ? "get; " : "") + (property.SetMethod?.IsPublic == true ? "set; " : "") + "} -> " + TypeName(property.PropertyType);
                    break;
                case FieldInfo field:
                    yield return name + "." + field.Name + " -> " + TypeName(field.FieldType);
                    break;
                case EventInfo item:
                    yield return name + ".event " + item.Name + " -> " + TypeName(item.EventHandlerType!);
                    break;
            }
        }
    }

    private static string Parameters(IEnumerable<ParameterInfo> parameters) => string.Join(", ", parameters.Select(p =>
        (p.IsOut ? "out " : p.IsIn ? "in " : "") + TypeName(p.ParameterType)));
}
