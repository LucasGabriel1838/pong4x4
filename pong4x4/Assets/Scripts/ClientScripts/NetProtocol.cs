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

    // Configuração da arena (o servidor manda; o cliente desenha igual).
    public float arenaHalf;
    public float paddleLength;
    public float paddleThickness;
    public float ballRadius;
    public float cornerSize;
    public float cannonRadius;

    // Canhões: 0 = topo-direita, 1 = baixo-direita, 2 = baixo-esquerda, 3 = topo-esquerda.
    public float[] cannonAngle = new float[4];   // graus
    public int heldCannon = -1;                  // canhão que está segurando a bola (-1 = nenhum)

    // Bumpers (pinball): 0 = (+,+), 1 = (+,-), 2 = (-,-), 3 = (-,+). bumperRadius = 0 -> desligados.
    public float bumperRadius;
    public float bumperOffset;                   // distância do centro da arena a cada bumper
    public int[] bumperHits = new int[4];        // contador de batidas (o cliente pisca quando muda)

    // Power-up de velocidade
    public float powerupRadius;
    public bool powerupActive;
    public float powerupX;
    public float powerupY;
    public bool boostActive;                     // bola em turbo
    public int powerupType;                      // 0 turbo, 1 raquete grande, 2 escudo, 3 bola fantasma, 4 congelar

    // Crédito dos power-ups: última raquete que tocou na bola (-1 = ninguém)
    public int lastHitter = -1;
    public bool ghost;                           // bola fantasma ativa
    public float[] paddleLen = new float[4];     // comprimento atual de cada raquete (raquete grande)
    public bool[] shield = new bool[4];          // jogador com escudo
    public bool[] frozen = new bool[4];          // jogador congelado

    // Aviso "Px pegou ..." (o cliente mostra quando pickupSeq muda)
    public int pickupSeq;
    public int pickupType;
    public int pickupPlayer = -1;
}
