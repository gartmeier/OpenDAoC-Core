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
    /// A polymorphic C# type becomes a union of its derived types, and each derived type an interface with its discriminator as a literal. An enum becomes a union of its names.
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

                string discriminator = union.GetCustomAttribute<JsonPolymorphicAttribute>().TypeDiscriminatorPropertyName;
                JsonDerivedTypeAttribute[] cases = union.GetCustomAttributes<JsonDerivedTypeAttribute>().ToArray();

                builder.Append('\n');
                builder.Append("export type ").Append(union.Name).Append(" = ").Append(string.Join(" | ", cases.Select(static derived => derived.DerivedType.Name))).Append(";\n");

                foreach (JsonDerivedTypeAttribute derived in cases)
                {
                    builder.Append('\n');
                    builder.Append("export interface ").Append(derived.DerivedType.Name).Append(" {\n");
                    builder.Append("  ").Append(discriminator).Append(": \"").Append(derived.TypeDiscriminator).Append("\";\n");

                    foreach (PropertyInfo property in derived.DerivedType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    {
                        bool optional = property.GetCustomAttribute<JsonIgnoreAttribute>()?.Condition is JsonIgnoreCondition.WhenWritingNull;
                        builder.Append("  ").Append(JsonNamingPolicy.CamelCase.ConvertName(property.Name)).Append(optional ? "?: " : ": ").Append(TypeName(property.PropertyType, pending)).Append(";\n");
                    }

                    builder.Append("}\n");
                }
            }

            return builder.ToString();
        }

        // Unions the property refers to are generated after the current one.
        private static string TypeName(Type type, Queue<Type> pending)
        {
            if (type == typeof(string))
                return "string";

            if (type == typeof(bool))
                return "boolean";

            if (type == typeof(int) || type == typeof(long))
                return "number";

            // An enum is the union of its names, as they go over the wire.
            if (type.IsEnum || type.GetCustomAttribute<JsonPolymorphicAttribute>() != null)
            {
                pending.Enqueue(type);
                return type.Name;
            }

            throw new NotSupportedException($"The Mind's TypeScript has no type for {type.Name}. Add it to {nameof(MindProtocolTypeScript)}.");
        }
    }
}
