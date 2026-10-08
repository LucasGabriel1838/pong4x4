using System;

[Serializable]
public class NetMessage
{
    public string type;
    public string clientName;
    public int playerId = -1;
    public float axis;
    public float ballX;
    public float ballY;
    public float ballVx;
    public float ballVy;
    public bool gameStarted;
    public bool gameOver;
    public int loserId = -1;
    public int[] scores = new int[4];
    public float[] paddleX = new float[4];
    public float[] paddleY = new float[4];
    public bool[] connected = new bool[4];
}
