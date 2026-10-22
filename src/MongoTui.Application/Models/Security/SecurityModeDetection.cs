namespace MongoTui.Application.Models;

/// <summary>Result of inspecting the persisted security configuration.</summary>
public sealed record SecurityModeDetection(SecurityState State, SecurityMode? Mode, SecurityProblem? Problem);
