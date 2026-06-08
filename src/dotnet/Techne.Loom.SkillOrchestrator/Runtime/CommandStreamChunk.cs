namespace Techne.Loom.SkillOrchestrator.Runtime;

public sealed record CommandStreamChunk(string CommandLine, string Stream, string Chunk);

public sealed record CommandStreamStart(string CommandLine);

public sealed record CommandStreamEnd(string CommandLine);
