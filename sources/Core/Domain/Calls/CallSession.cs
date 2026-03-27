using System.Collections.ObjectModel;
using System.Net;

namespace Core.Domain.Calls;

public class CallSession
{
    public string RoomCode { get; private set; } = string.Empty;

    public Interlocutor Self { get; private set; }

    public CallState State { get; private set; } = CallState.Idle;
    public int LocalUdpPort { get; private set; }
    public IPEndPoint? PublicEndPoint { get; private set; }
    public IPEndPoint? LocalEndPoint { get; private set; }

    public ObservableCollection<Interlocutor> Interlocutors = [];
    
    public void SetRoomCode(string code)
    {
        RoomCode = code ?? string.Empty;
    }

    public void SetSelf(Interlocutor self)
    {
        Self = self;
    }

    public void SetLocal(int localUdpPort, IPEndPoint? publicEp, IPEndPoint? localEp)
    {
        LocalUdpPort = localUdpPort;
        PublicEndPoint = publicEp;
        LocalEndPoint = localEp;
    }

    public void TransitionTo(CallState newState)
    {
        State = newState;
    }
}