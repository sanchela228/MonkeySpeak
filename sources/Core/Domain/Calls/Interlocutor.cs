using System.Net;

namespace Core.Domain.Calls;

public class Interlocutor(string id, IPEndPoint remoteIp, CallState state)
{
    public string Id = id;
    public IPEndPoint RemoteIp = remoteIp;
    public CallState State = state;
    public bool IsMuted = false;
}