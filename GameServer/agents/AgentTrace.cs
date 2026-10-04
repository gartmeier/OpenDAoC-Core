using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using DOL.Logging;

namespace DOL.GS.Agents
{
    /// <summary>
    /// Every call the server makes to tell one Agent's client something, in order and with its time and salient arguments: the inventory of future Percepts (ADR 0001).
    /// A research artifact for the Human to read after a session, not the Journal.
    /// Calls are formatted where they're made, because their arguments are live game objects, and written to the trace's own file off the game loop.
    /// </summary>
    public sealed class AgentTrace : IDisposable
    {
        private static readonly Logger log = LoggerManager.Create(MethodBase.GetCurrentMethod().DeclaringType);

        public const char COMMENT_PREFIX = '#';
        private const char SEPARATOR = '\t';

        // Collections longer than this show their first items and how many more there are.
        private const int MAX_COLLECTION_ITEMS = 10;

        private static readonly ConcurrentDictionary<MethodInfo, string[]> _parameterNames = new();
        private static readonly ConcurrentDictionary<Type, PropertyInfo> _nameProperties = new();

        private readonly string _characterName;
        private readonly DateTime _started = DateTime.UtcNow;
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private readonly Channel<string> _lines = Channel.CreateUnbounded<string>(new() { SingleReader = true });
        private long _sequence;
        private volatile bool _stopped;

        // The writer is opened and written to off the caller's thread. Calls recorded before disposal are all written; later ones are ignored.
        public AgentTrace(string characterName, Func<TextWriter> openWriter)
        {
            _characterName = characterName;
            Completion = Task.Run(() => WriteUntilDisposed(openWriter));
        }

        public string FilePath { get; private init; }

        // Completes once every recorded call is written and the writer is closed.
        public Task Completion { get; }

        // A trace in its own file under `agent-traces` in the server's root directory, one file per login.
        public static AgentTrace Start(string characterName)
        {
            string path = Path.Combine(GameServer.Instance.Configuration.RootDirectory, "agent-traces", $"{characterName}-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.tsv");

            return new(characterName, () =>
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                return new StreamWriter(path);
            })
            {
                FilePath = path
            };
        }

        // One line per call: sequence, elapsed milliseconds, client state, then the call with its arguments. Never blocks.
        public void Record(GameClient.eClientState clientState, MethodInfo method, object[] args)
        {
            if (_stopped)
                return;

            long sequence = Interlocked.Increment(ref _sequence);
            long elapsed = _stopwatch.ElapsedMilliseconds;

            string line = string.Join(SEPARATOR,
                sequence.ToString(CultureInfo.InvariantCulture),
                elapsed.ToString(CultureInfo.InvariantCulture),
                clientState.ToString(),
                FormatCall(method, args));

            _lines.Writer.TryWrite(line);
        }

        public void Dispose()
        {
            _stopped = true;
            _lines.Writer.TryComplete();
        }

        private async Task WriteUntilDisposed(Func<TextWriter> openWriter)
        {
            try
            {
                using TextWriter writer = openWriter();
                await writer.WriteLineAsync($"{COMMENT_PREFIX} Calls the server made to tell {_characterName}'s client something, started {_started:O}");
                await writer.WriteLineAsync($"{COMMENT_PREFIX} sequence{SEPARATOR}elapsed_ms{SEPARATOR}client_state{SEPARATOR}call");

                while (await _lines.Reader.WaitToReadAsync())
                {
                    while (_lines.Reader.TryRead(out string line))
                        await writer.WriteLineAsync(line);

                    await writer.FlushAsync();
                }
            }
            catch (Exception e)
            {
                // Stop recording rather than queueing calls nobody will write.
                _stopped = true;
                _lines.Writer.TryComplete();

                if (log.IsErrorEnabled)
                    log.Error($"The trace of {_characterName}'s client failed and has stopped", e);
            }
        }

        private static string FormatCall(MethodInfo method, object[] args)
        {
            string[] parameterNames = _parameterNames.GetOrAdd(method, static method => method.GetParameters().Select(static parameter => parameter.Name).ToArray());
            StringBuilder builder = new();
            builder.Append(method.Name).Append('(');

            for (int i = 0; i < parameterNames.Length; i++)
            {
                if (i > 0)
                    builder.Append(", ");

                builder.Append(parameterNames[i]).Append(": ");
                AppendArgument(builder, args?.Length > i ? args[i] : null);
            }

            return builder.Append(')').ToString();
        }

        // A server object can throw when inspected (a half-initialized object, say). That must not reach the server's call.
        private static void AppendArgument(StringBuilder builder, object value)
        {
            try
            {
                AppendValue(builder, value, true);
            }
            catch (Exception e)
            {
                builder.Append('<').Append(e.GetType().Name).Append('>');
            }
        }

        // What identifies a value to a reader: strings and plain values as they are, objects by type and name, collections by their items.
        private static void AppendValue(StringBuilder builder, object value, bool expandCollections)
        {
            switch (value)
            {
                case null:
                {
                    builder.Append("null");
                    return;
                }
                case string text:
                {
                    AppendString(builder, text);
                    return;
                }
                case IFormattable formattable when value.GetType().IsPrimitive || value is Enum or decimal:
                {
                    builder.Append(formattable.ToString(null, CultureInfo.InvariantCulture));
                    return;
                }
                case bool or char:
                {
                    builder.Append(value);
                    return;
                }
                case GameObject gameObject:
                {
                    builder.Append(TypeName(value.GetType())).Append(' ');
                    AppendString(builder, gameObject.Name);
                    builder.Append(" #").Append(gameObject.ObjectID);
                    return;
                }
                case ITuple tuple:
                {
                    builder.Append('(');

                    for (int i = 0; i < tuple.Length; i++)
                    {
                        if (i > 0)
                            builder.Append(", ");

                        AppendValue(builder, tuple[i], false);
                    }

                    builder.Append(')');
                    return;
                }
                case IDictionary dictionary when expandCollections:
                {
                    AppendItems(builder, '{', '}', dictionary.Count, dictionary.Cast<DictionaryEntry>().Select(static entry => (entry.Key, (object) entry.Value)));
                    return;
                }
                case ICollection collection when expandCollections:
                {
                    AppendItems(builder, '[', ']', collection.Count, collection.Cast<object>().Select(static item => (default(object), item)));
                    return;
                }
                case ICollection collection:
                {
                    builder.Append(TypeName(value.GetType())).Append('[').Append(collection.Count).Append(']');
                    return;
                }
            }

            builder.Append(TypeName(value.GetType()));

            if (NameProperty(value.GetType())?.GetValue(value) is string name)
            {
                builder.Append(' ');
                AppendString(builder, name);
            }
        }

        private static void AppendItems(StringBuilder builder, char open, char close, int count, IEnumerable<(object Key, object Item)> items)
        {
            builder.Append(open);
            int shown = 0;

            foreach ((object key, object item) in items.Take(MAX_COLLECTION_ITEMS))
            {
                if (shown++ > 0)
                    builder.Append(", ");

                if (key != null)
                {
                    AppendValue(builder, key, false);
                    builder.Append(": ");
                }

                AppendValue(builder, item, false);
            }

            if (count > shown)
                builder.Append(shown > 0 ? ", " : string.Empty).Append("+").Append(count - shown).Append(" more");

            builder.Append(close);
        }

        // Quoted and escaped, so that a call stays on one line of the trace.
        private static void AppendString(StringBuilder builder, string text)
        {
            builder.Append('"');

            foreach (char c in text)
            {
                string escaped = c switch
                {
                    '"' => "\\\"",
                    '\\' => "\\\\",
                    '\r' => "\\r",
                    '\n' => "\\n",
                    '\t' => "\\t",
                    _ => null
                };

                if (escaped != null)
                    builder.Append(escaped);
                else
                    builder.Append(c);
            }

            builder.Append('"');
        }

        private static PropertyInfo NameProperty(Type type)
        {
            return _nameProperties.GetOrAdd(type, static type =>
            {
                PropertyInfo property = type.GetProperties(BindingFlags.Public | BindingFlags.Instance).FirstOrDefault(static property => property.Name is "Name" && property.PropertyType == typeof(string) && property.GetIndexParameters().Length == 0);
                return property?.CanRead is true ? property : null;
            });
        }

        // `List<DbInventoryItem>` rather than ``List`1``.
        private static string TypeName(Type type)
        {
            if (!type.IsGenericType)
                return type.Name;

            string name = type.Name[..type.Name.IndexOf('`')];
            return $"{name}<{string.Join(", ", type.GetGenericArguments().Select(TypeName))}>";
        }
    }
}
