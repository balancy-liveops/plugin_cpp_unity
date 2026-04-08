using System.Collections.Generic;
using System.Linq;

namespace Balancy
{
    /// <summary>
    /// Return type for C# methods invoked by the RunFunction visual scripting node.
    /// Controls which exit port the graph continues from and what output values are passed forward.
    /// </summary>
    public struct NodeRunFunctionReturnType
    {
        private readonly string _exitPort;
        private readonly IReadOnlyDictionary<string, object> _outputs;

        public string ExitPort => _exitPort;
        public IReadOnlyDictionary<string, object> Outputs => _outputs;

        /// <param name="outputs">Named output values that will be set on the node's output ports.</param>
        /// <param name="exitPort">Which exit port to follow. Defaults to "Exit".</param>
        public NodeRunFunctionReturnType(IDictionary<string, object> outputs, string exitPort = "Exit")
        {
            _exitPort = exitPort;
            _outputs = outputs?.ToDictionary(p => p.Key, p => p.Value)
                       ?? new Dictionary<string, object>();
        }

        /// <summary>Convenience constructor for a single output value.</summary>
        public NodeRunFunctionReturnType(string outputKey, object outputValue, string exitPort = "Exit")
        {
            _exitPort = exitPort;
            _outputs = new Dictionary<string, object> { { outputKey, outputValue } };
        }

        /// <summary>Convenience constructor with no outputs — only controls the exit port.</summary>
        public NodeRunFunctionReturnType(string exitPort)
        {
            _exitPort = exitPort;
            _outputs = new Dictionary<string, object>();
        }
    }
}
