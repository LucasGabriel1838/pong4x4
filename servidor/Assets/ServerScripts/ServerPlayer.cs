using System.Net;

[System.Serializable]
public class ServerPlayer
{
    public int playerId;
    public string playerName;
    public IPEndPoint endpoint;
    public float axis;
    public float paddleX;
    public float paddleY;
    public int score;
    public bool connected;

    // Último instante (Time.realtimeSinceStartup) em que chegou qualquer pacote deste jogador.
    public float lastSeen;

    public ServerPlayer(int id)
    {
        playerId = id;
        score = 0;
        axis = 0f;
        connected = false;
        lastSeen = 0f;
    }
}
