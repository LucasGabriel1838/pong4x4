using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class UDPClient : MonoBehaviour
{
    [Header("Servidor")]
    public string serverIP = "127.0.0.1";
    public int serverPort = 7777;
    public string clientName = "Player";

    public int PlayerId { get; private set; } = -1;
    public bool IsConnected { get; private set; }

    private UdpClient udp;
    private Thread receiveThread;
    private volatile bool running;
    private readonly ConcurrentQueue<string> incoming = new ConcurrentQueue<string>();
    private IPEndPoint serverEndpoint;

    public event Action<NetMessage> StateReceived;
    public event Action<string> StatusChanged;

    private void Start()
    {
        Connect();
    }

    public void Connect()
    {
        if (running) return;

        try
        {
            serverEndpoint = new IPEndPoint(IPAddress.Parse(serverIP), serverPort);
            udp = new UdpClient();
            udp.Connect(serverEndpoint);
            running = true;
            receiveThread = new Thread(ReceiveLoop) { IsBackground = true };
            receiveThread.Start();

            NetMessage join = new NetMessage();
            join.type = "JOIN";
            join.clientName = clientName;
            Send(join);
            StatusChanged?.Invoke("Solicitando entrada...");
        }
        catch (Exception e)
        {
            StatusChanged?.Invoke("Falha de conexão: " + e.Message);
            Debug.LogError("UDPClient: " + e.Message);
        }
    }

    private void ReceiveLoop()
    {
        IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
        while (running)
        {
            try
            {
                byte[] data = udp.Receive(ref remote);
                incoming.Enqueue(Encoding.UTF8.GetString(data));
            }
            catch (ObjectDisposedException) { break; }
            catch (SocketException) { if (!running) break; }
            catch (Exception e) { if (running) Debug.LogWarning("Recepção UDP: " + e.Message); }
        }
    }

    private void Update()
    {
        while (incoming.TryDequeue(out string json))
        {
            NetMessage message;
            try { message = JsonUtility.FromJson<NetMessage>(json); }
            catch { continue; }

            if (message == null) continue;

            if (message.type == "WELCOME")
            {
                PlayerId = message.playerId;
                IsConnected = PlayerId >= 0;
                StatusChanged?.Invoke(IsConnected ? "Conectado como P" + (PlayerId + 1) : "Não foi possível entrar");
            }
            else if (message.type == "FULL")
            {
                IsConnected = false;
                StatusChanged?.Invoke("Servidor cheio (4 jogadores).");
            }

            if (message.type == "STATE" || message.type == "WELCOME")
                StateReceived?.Invoke(message);
        }
    }

    public void SendInput(float axis)
    {
        if (!running || !IsConnected) return;
        NetMessage message = new NetMessage();
        message.type = "INPUT";
        message.axis = Mathf.Clamp(axis, -1f, 1f);
        Send(message);
    }

    private void Send(NetMessage message)
    {
        if (udp == null) return;
        try
        {
            byte[] data = Encoding.UTF8.GetBytes(JsonUtility.ToJson(message));
            udp.Send(data, data.Length);
        }
        catch (Exception e) { Debug.LogWarning("Envio UDP: " + e.Message); }
    }

    private void OnApplicationQuit() { Disconnect(); }

    public void Disconnect()
    {
        if (!running) return;
        running = false;
        IsConnected = false;
        try { udp?.Close(); } catch { }
        try
        {
            if (receiveThread != null && receiveThread.IsAlive)
                receiveThread.Join(200);
        }
        catch { }
        StatusChanged?.Invoke("Desconectado.");
    }
}
