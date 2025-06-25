// IPythonDecomposerAgent.cs
using System.Threading.Tasks;

namespace LLMTextToSql.Interfaces.Agents
{
    public interface IPythonDecomposerAgent
    {
        /// <summary>
        /// Returns the raw chain-of-thought from the Python decomposer,
        /// including all Sub-question / SQL n: / Final SQL sections.
        /// </summary>
        Task<string> DecomposeChainOfThoughtAsync(string question);
    }
}
