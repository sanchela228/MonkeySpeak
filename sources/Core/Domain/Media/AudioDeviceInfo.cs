namespace Core.Domain.Media;

/// <summary>
/// Platform-agnostic audio device descriptor.
/// SoundFlow uses IntPtr IDs internally — we expose string-based IDs for cross-platform portability.
/// </summary>
public sealed record AudioDeviceInfo(string Id, string Name, bool IsDefault);
