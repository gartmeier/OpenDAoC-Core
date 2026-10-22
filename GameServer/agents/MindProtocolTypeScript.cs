using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DOL.GS.Agents
{
    /// <summary>
    /// Generates the Mind's TypeScript for <see cref="MindProtocol"/>'s message types, from the same JSON attributes that decide what goes over the wire.
    /// A polymorphic C# type becomes a union of its derived types, and each derived type an interface with its discriminator as a literal. Any other record becomes a plain interface, and an enum a union of its names.
    /// A unit test fails when the checked-in file differs from what this generates.
    /// </summary>
    public static class MindProtocolTypeScript
    {
        // Relative to the repository root.
        public const string PATH = "mind/src/protocol.ts";
        public const string REGENERATE_COMMAND = "dotnet test Tests/Tests.csproj --filter UT_MindProtocolTypeScript -e REGENERATE_MIND_PROTOCOL=1";

        private static readonly Type[] _roots = [typeof(MindMessage), typeof(ServerMessage)];

        public static string Generate()
        {
            StringBuilder builder = new();
            builder.Append("// Generated from the C# types in GameServer/agents/MindProtocol.cs. Don't edit it by hand. Regenerate it with:\n");
            builder.Append("// ").Append(REGENERATE_COMMAND).Append('\n');

            HashSet<Type> generated = [];
            Queue<Type> pending = new(_roots);

            while (pending.TryDequeue(out Type union))
            {
                if (!generated.Add(union))
                    continue;

                if (union.IsEnum)
                {
                    builder.Append('\n');
                    builder.Append("export type ").Append(union.Name).Append(" = ").Append(string.Join(" | ", Enum.GetNames(union).Select(static name => $"\"{name}\""))).Append(";\n");
                    continue;
                }

                JsonPolymorphicAttribute polymorphic = union.GetCustomAttribute<JsonPolymorphicAttribute>();

                // A type that isn't polymorphic is a plain interface.
                if (polymorphic == null)
                {
                    builder.Append('\n');
                    AppendInterface(builder, union, null, pending);
                    continue;
                }

                JsonDerivedTypeAttribute[] cases = union.GetCustomAttributes<JsonDerivedTypeAttribute>().ToArray();

                builder.Append('\n');
                builder.Append("export type ").Append(union.Name).Append(" = ").Append(string.Join(" | ", cases.Select(static derived => derived.DerivedType.Name))).Append(";\n");

                foreach (JsonDerivedTypeAttribute derived in cases)
                {
                    builder.Append('\n');
                    AppendInterface(builder, derived.DerivedType, $"{polymorphic.TypeDiscriminatorPropertyName}: \"{derived.TypeDiscriminator}\"", pending);
                }
            }

            return builder.ToString();
        }

        // `discriminator` is the discriminator's line, or null for a type that isn't one case of a union.
        private static void AppendInterface(StringBuilder builder, Type type, string discriminator, Queue<Type> pending)
        {
            builder.Append("export interface ").Append(type.Name).Append(" {\n");

            if (discriminator != null)
                builder.Append("  ").Append(discriminator).Append(";\n");

            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                bool optional = property.GetCustomAttribute<JsonIgnoreAttribute>()?.Condition is JsonIgnoreCondition.WhenWritingNull;
                builder.Append("  ").Append(JsonNamingPolicy.CamelCase.ConvertName(property.Name)).Append(optional ? "?: " : ": ").Append(TypeName(property.PropertyType, pending)).Append(";\n");
            }

            builder.Append("}\n");
        }

        // Unions the property refers to are generated after the current one.
        private static string TypeName(Type type, Queue<Type> pending)
        {
            if (type == typeof(string))
                return "string";

            if (type == typeof(bool))
                return "boolean";

            if (type == typeof(int) || type == typeof(long) || type == typeof(float))
                return "number";

            // Null only when it's left out, which the property's JSON attribute marks as optional.
            if (Nullable.GetUnderlyingType(type) is Type underlying)
                return TypeName(underlying, pending);

            if (type.IsArray)
                return $"{TypeName(type.GetElementType(), pending)}[]";

            // An enum is the union of its names, as they go over the wire. A record is an interface of its own.
            if (type.IsEnum || type.GetCustomAttribute<JsonPolymorphicAttribute>() != null || IsRecord(type))
            {
                pending.Enqueue(type);
                return type.Name;
            }

            throw new NotSupportedException($"The Mind's TypeScript has no type for {type.Name}. Add it to {nameof(MindProtocolTypeScript)}.");
        }

        // The compiler gives every record class a clone method under this name.
        private static bool IsRecord(Type type)
        {
            return type.GetMethod("<Clone>$") != null;
        }
    }
}
