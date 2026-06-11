using System.Text.Json;
using Techne.Loom.AgentOrchestrator.Models;

namespace Techne.Loom.AgentOrchestrator.Cli;

internal sealed class AoPropertyWriter
{
    private readonly TextWriter _writer;
    private readonly object _gate = new();

    public AoPropertyWriter(TextWriter writer)
    {
        _writer = writer;
    }

    public void WriteAoProperty(AoPropertyEnvelope envelope)
    {
        lock (_gate)
        {
            _writer.WriteLine("<ao_property>");
            _writer.WriteLine(JsonSerializer.Serialize(envelope, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            _writer.WriteLine("</ao_property>");
            _writer.Flush();
        }
    }
}
