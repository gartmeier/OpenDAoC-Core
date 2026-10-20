using System;
using System.IO;
using System.Runtime.CompilerServices;
using DOL.GS.Agents;
using NUnit.Framework;

namespace DOL.GS.Tests
{
    /// <summary>
    /// Keeps the Mind's checked-in protocol types in step with their C# definitions, as rs-sdk's test keeps its generated API reference in sync.
    /// Run with `REGENERATE_MIND_PROTOCOL=1` in the environment, it writes the file instead of checking it.
    /// </summary>
    [TestFixture]
    public class UT_MindProtocolTypeScript
    {
        public const string REGENERATE_VARIABLE = "REGENERATE_MIND_PROTOCOL";

        [Test]
        public void CheckedInFile_ShouldMatchTheGeneratedTypeScript()
        {
            string path = CheckedInPath();
            string generated = MindProtocolTypeScript.Generate();

            if (Environment.GetEnvironmentVariable(REGENERATE_VARIABLE) == "1")
            {
                File.WriteAllText(path, generated);
                Assert.Pass($"Regenerated {path}.");
            }

            // Git may have checked the file out with CRLF line endings.
            string checkedIn = File.Exists(path) ? File.ReadAllText(path).ReplaceLineEndings("\n") : null;

            Assert.That(checkedIn, Is.EqualTo(generated), $"{path} is out of date with the C# protocol types. Regenerate it with: {MindProtocolTypeScript.REGENERATE_COMMAND}");
        }

        private static string CheckedInPath([CallerFilePath] string thisFile = null)
        {
            return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile), "..", "..", MindProtocolTypeScript.PATH));
        }
    }
}
