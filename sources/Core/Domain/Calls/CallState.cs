namespace Core.Domain.Calls;

public enum CallState
{
    Idle = 0,
    Waiting = 1,
    HolePunching = 2,
    Connected = 3,
    Failed = 4,
    Closed = 5
}