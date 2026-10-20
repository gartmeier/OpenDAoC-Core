using DOL.GS.Agents;
using NUnit.Framework;

namespace DOL.GS.Tests
{
    /// <summary>
    /// Agent Accounts are ordinary Accounts recognised by an `agent` prefix, so that a Mind can't take over a Human's Character.
    /// </summary>
    [TestFixture]
    public class UT_AgentAccount
    {
        [TestCase("agent1")]
        [TestCase("agent")]
        [TestCase("agentsmith")]
        // Account names are case-insensitive, as the server's login check treats them.
        [TestCase("Agent2")]
        [TestCase("AGENT3")]
        public void IsAgentAccount_ShouldAcceptAgentPrefix(string accountName)
        {
            Assert.That(AgentAccount.IsAgentAccount(accountName), Is.True);
        }

        [TestCase("joshua")]
        [TestCase("myagent1")]
        [TestCase("agen")]
        [TestCase(" agent1")]
        [TestCase("")]
        [TestCase(null)]
        public void IsAgentAccount_ShouldRejectOtherNames(string accountName)
        {
            Assert.That(AgentAccount.IsAgentAccount(accountName), Is.False);
        }
    }
}
