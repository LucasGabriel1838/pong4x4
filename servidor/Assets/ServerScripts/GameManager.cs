using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Header("Arena (unidades Unity)")]
    public float halfWidth = 8f;
    public float halfHeight = 5f;
    public float paddleLength = 1.8f;
    public float paddleThickness = 0.22f;
    public float paddleSpeed = 6f;
    public float ballRadius = 0.18f;
    public float ballSpeed = 5f;
    [Tooltip("Distância do centro da raquete até a parede.")]
    public float paddleInset = 0.35f;
    [Tooltip("Espaço livre nos cantos para as raquetes não se cruzarem.")]
    public float cornerMargin = 0.6f;

    [Header("Regras")]
    public int maxPenaltyPoints = 5;
    public bool startOnlyWithFourPlayers = true;
    [Tooltip("Segundos sem receber pacotes para considerar o jogador desconectado.")]
    public float disconnectTimeout = 5f;
    [Tooltip("Segundos após o fim de jogo para reiniciar automaticamente.")]
    public float restartDelay = 6f;

    public readonly ServerPlayer[] players = new ServerPlayer[4];
    public Vector2 ballPosition;
    public Vector2 ballVelocity;
    public bool gameStarted;
    public bool gameOver;
    public int loserId = -1;

    private float serveTimer;
    private float restartTimer;
    private const float ServeDelay = 0.8f;
    private const float InputStaleTime = 0.5f;

    private void Awake()
    {
        for (int i = 0; i < players.Length; i++)
        {
            players[i] = new ServerPlayer(i);
            // Posiciona cada raquete na borda correta
            // mesmo antes de os clientes se conectarem.
            SetPaddleStartPosition(players[i]);
        }

        ResetBall();
    }

    public int ConnectedCount()
    {
        int count = 0;
        foreach (ServerPlayer p in players)
            if (p.connected) count++;
        return count;
    }

    // A bola só se move se a partida começou, não acabou e (se exigido) os 4 estão presentes.
    public bool IsPlaying
    {
        get { return gameStarted && !gameOver && (!startOnlyWithFourPlayers || ConnectedCount() == 4); }
    }

    public int AddOrReconnectPlayer(string name, System.Net.IPEndPoint endpoint)
    {
        // Mesmo endpoint (JOIN repetido): devolve o mesmo ID.
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i].connected && EndpointEquals(players[i].endpoint, endpoint))
            {
                if (!string.IsNullOrWhiteSpace(name)) players[i].playerName = name;
                players[i].lastSeen = Time.realtimeSinceStartup;
                return i;
            }
        }

        for (int i = 0; i < players.Length; i++)
        {
            if (!players[i].connected)
            {
                ServerPlayer p = players[i];
                p.connected = true;
                p.playerName = string.IsNullOrWhiteSpace(name) ? "Player " + (i + 1) : name;
                p.endpoint = endpoint;
                p.axis = 0f;
                p.lastSeen = Time.realtimeSinceStartup;
                SetPaddleStartPosition(p);
                Debug.Log("Cliente entrou: " + p.playerName + " (ID " + i + ") de " + endpoint);
                TryStartGame();
                return i;
            }
        }

        return -1;
    }

    public void ApplyInput(int playerId, float axis)
    {
        if (playerId < 0 || playerId >= players.Length)
            return;

        players[playerId].lastSeen = Time.realtimeSinceStartup;

        if (gameOver) return;
        players[playerId].axis = Mathf.Clamp(axis, -1f, 1f);
    }

    // Libera vagas de clientes que sumiram (fecharam o jogo, reiniciaram com outra porta etc.).
    public void CheckTimeouts()
    {
        float now = Time.realtimeSinceStartup;
        foreach (ServerPlayer p in players)
        {
            if (!p.connected) continue;

            float silence = now - p.lastSeen;
            if (silence > InputStaleTime) p.axis = 0f;

            if (silence > disconnectTimeout)
            {
                Debug.Log("Cliente " + p.playerName + " (ID " + p.playerId + ") caiu por timeout.");
                p.connected = false;
                p.endpoint = null;
                p.axis = 0f;
                SetPaddleStartPosition(p);
            }
        }
    }

    private void TryStartGame()
    {
        if (gameStarted || gameOver) return;

        int count = ConnectedCount();
        if (count >= 1 && (!startOnlyWithFourPlayers || count == 4))
        {
            gameStarted = true;
            Debug.Log("Partida iniciada.");
            ResetBall();
        }
    }

    public void Tick(float dt)
    {
        if (gameOver)
        {
            restartTimer -= dt;
            if (restartTimer <= 0f) RestartMatch();
            return;
        }

        if (!gameStarted)
        {
            TryStartGame();
            if (!gameStarted) return;
        }

        // Alguém caiu no meio da partida: pausa até voltar a ter 4 jogadores.
        if (!IsPlaying) return;

        MovePaddles(dt);

        if (serveTimer > 0f)
        {
            serveTimer -= dt;
            return;
        }

        ballPosition += ballVelocity * dt;

        // 1) raquetes  2) gols (bola passou da raquete)  3) paredes onde não há jogador.
        HandlePaddleCollisions();
        CheckGoals();

        if (!gameOver)
            HandleWallBounces();
    }

    private float HorizontalLimit() { return halfWidth - cornerMargin - paddleLength * 0.5f; }
    private float VerticalLimit() { return halfHeight - cornerMargin - paddleLength * 0.5f; }

    private void MovePaddles(float dt)
    {
        foreach (ServerPlayer p in players)
        {
            if (!p.connected) continue;

            if (p.playerId == 0 || p.playerId == 2)
            {
                p.paddleX += p.axis * paddleSpeed * dt;
                p.paddleX = Mathf.Clamp(p.paddleX, -HorizontalLimit(), HorizontalLimit());
                p.paddleY = p.playerId == 0 ? halfHeight - paddleInset : -halfHeight + paddleInset;
            }
            else
            {
                p.paddleY += p.axis * paddleSpeed * dt;
                p.paddleY = Mathf.Clamp(p.paddleY, -VerticalLimit(), VerticalLimit());
                p.paddleX = p.playerId == 1 ? halfWidth - paddleInset : -halfWidth + paddleInset;
            }
        }
    }

    // Só quica em parede onde NÃO existe jogador conectado (lado sem raquete).
    // Onde há jogador, a bola que passa da raquete vira penalidade em CheckGoals.
    private void HandleWallBounces()
    {
        if (!players[1].connected && ballPosition.x > halfWidth - ballRadius)
        {
            ballPosition.x = halfWidth - ballRadius;
            ballVelocity.x = -Mathf.Abs(ballVelocity.x);
        }
        if (!players[3].connected && ballPosition.x < -halfWidth + ballRadius)
        {
            ballPosition.x = -halfWidth + ballRadius;
            ballVelocity.x = Mathf.Abs(ballVelocity.x);
        }
        if (!players[0].connected && ballPosition.y > halfHeight - ballRadius)
        {
            ballPosition.y = halfHeight - ballRadius;
            ballVelocity.y = -Mathf.Abs(ballVelocity.y);
        }
        if (!players[2].connected && ballPosition.y < -halfHeight + ballRadius)
        {
            ballPosition.y = -halfHeight + ballRadius;
            ballVelocity.y = Mathf.Abs(ballVelocity.y);
        }
    }

    private void HandlePaddleCollisions()
    {
        // 0 = topo, 2 = baixo (raquetes horizontais); 1 = direita, 3 = esquerda (verticais).
        TryHorizontalPaddleCollision(0, halfHeight - paddleInset, true);
        TryHorizontalPaddleCollision(2, -halfHeight + paddleInset, false);
        TryVerticalPaddleCollision(1, halfWidth - paddleInset, true);
        TryVerticalPaddleCollision(3, -halfWidth + paddleInset, false);
    }

    private void TryHorizontalPaddleCollision(int id, float y, bool top)
    {
        ServerPlayer p = players[id];
        if (!p.connected) return;

        float dir = top ? 1f : -1f;
        float innerFace = y - dir * paddleThickness * 0.5f;   // face voltada para o centro
        float outerFace = y + dir * paddleThickness * 0.5f;

        // A borda da bola tocou a face interna e o centro ainda não passou da face externa.
        bool touching = top
            ? ballVelocity.y > 0f && ballPosition.y + ballRadius >= innerFace && ballPosition.y <= outerFace
            : ballVelocity.y < 0f && ballPosition.y - ballRadius <= innerFace && ballPosition.y >= outerFace;

        if (!touching) return;

        if (Mathf.Abs(ballPosition.x - p.paddleX) <= paddleLength * 0.5f + ballRadius)
        {
            ballPosition.y = innerFace - dir * ballRadius;
            ballVelocity.y = top ? -Mathf.Abs(ballVelocity.y) : Mathf.Abs(ballVelocity.y);
            AddBounceAngle(p, true);
        }
    }

    private void TryVerticalPaddleCollision(int id, float x, bool right)
    {
        ServerPlayer p = players[id];
        if (!p.connected) return;

        float dir = right ? 1f : -1f;
        float innerFace = x - dir * paddleThickness * 0.5f;
        float outerFace = x + dir * paddleThickness * 0.5f;

        bool touching = right
            ? ballVelocity.x > 0f && ballPosition.x + ballRadius >= innerFace && ballPosition.x <= outerFace
            : ballVelocity.x < 0f && ballPosition.x - ballRadius <= innerFace && ballPosition.x >= outerFace;

        if (!touching) return;

        if (Mathf.Abs(ballPosition.y - p.paddleY) <= paddleLength * 0.5f + ballRadius)
        {
            ballPosition.x = innerFace - dir * ballRadius;
            ballVelocity.x = right ? -Mathf.Abs(ballVelocity.x) : Mathf.Abs(ballVelocity.x);
            AddBounceAngle(p, false);
        }
    }

    private void AddBounceAngle(ServerPlayer p, bool horizontalPaddle)
    {
        // Altera o ângulo conforme o ponto de contato na raquete.
        float offset = horizontalPaddle
            ? (ballPosition.x - p.paddleX) / (paddleLength * 0.5f)
            : (ballPosition.y - p.paddleY) / (paddleLength * 0.5f);
        offset = Mathf.Clamp(offset, -1f, 1f);

        if (horizontalPaddle) ballVelocity.x += offset * 1.5f;
        else ballVelocity.y += offset * 1.5f;

        ballVelocity = ballVelocity.normalized * ballSpeed;

        // Evita a bola ficar quase paralela à raquete (vai e volta "para sempre").
        float minAlong = ballSpeed * 0.35f;
        if (horizontalPaddle)
        {
            float sign = Mathf.Sign(ballVelocity.y);
            if (Mathf.Abs(ballVelocity.y) < minAlong)
            {
                ballVelocity.y = sign * minAlong;
                ballVelocity = ballVelocity.normalized * ballSpeed;
            }
        }
        else
        {
            float sign = Mathf.Sign(ballVelocity.x);
            if (Mathf.Abs(ballVelocity.x) < minAlong)
            {
                ballVelocity.x = sign * minAlong;
                ballVelocity = ballVelocity.normalized * ballSpeed;
            }
        }
    }

    // Gol/penalidade: a bola chegou na parede de um lado que TEM jogador.
    // A linha do gol fica ANTES do limite de quique das paredes (bug original: o
    // quique em halfWidth - ballRadius acontecia antes da linha do gol e nunca pontuava).
    private void CheckGoals()
    {
        if (players[0].connected && ballVelocity.y > 0f && ballPosition.y >= halfHeight - ballRadius)
            ScoreFor(0);
        else if (players[1].connected && ballVelocity.x > 0f && ballPosition.x >= halfWidth - ballRadius)
            ScoreFor(1);
        else if (players[2].connected && ballVelocity.y < 0f && ballPosition.y <= -halfHeight + ballRadius)
            ScoreFor(2);
        else if (players[3].connected && ballVelocity.x < 0f && ballPosition.x <= -halfWidth + ballRadius)
            ScoreFor(3);
    }

    private void ScoreFor(int playerId)
    {
        if (gameOver) return;

        players[playerId].score++;
        Debug.Log("Penalidade para P" + (playerId + 1) + ": " + players[playerId].score);

        if (players[playerId].score >= maxPenaltyPoints)
        {
            gameOver = true;
            loserId = playerId;
            restartTimer = restartDelay;
            Debug.Log("Fim de jogo. P" + (playerId + 1) + " perdeu.");
            return;
        }

        ResetBall();
    }

    private void RestartMatch()
    {
        foreach (ServerPlayer p in players) p.score = 0;
        gameOver = false;
        loserId = -1;
        gameStarted = false;
        Debug.Log("Reiniciando partida.");
        TryStartGame();
        if (!gameStarted) ResetBall();
    }

    private void ResetBall()
    {
        ballPosition = Vector2.zero;
        float x = Random.value < 0.5f ? -1f : 1f;
        float y = Random.Range(-0.65f, 0.65f);
        if (Mathf.Abs(y) < 0.2f) y = 0.35f;
        ballVelocity = new Vector2(x, y).normalized * ballSpeed;
        serveTimer = ServeDelay;
    }

    private void SetPaddleStartPosition(ServerPlayer p)
    {
        if (p.playerId == 0) { p.paddleX = 0; p.paddleY = halfHeight - paddleInset; }
        if (p.playerId == 1) { p.paddleX = halfWidth - paddleInset; p.paddleY = 0; }
        if (p.playerId == 2) { p.paddleX = 0; p.paddleY = -halfHeight + paddleInset; }
        if (p.playerId == 3) { p.paddleX = -halfWidth + paddleInset; p.paddleY = 0; }
    }

    private bool EndpointEquals(System.Net.IPEndPoint a, System.Net.IPEndPoint b)
    {
        return a != null && b != null && a.Address.Equals(b.Address) && a.Port == b.Port;
    }

    public NetMessage CreateState(int assignedPlayerId)
    {
        NetMessage state = new NetMessage();
        state.type = "STATE";
        state.playerId = assignedPlayerId;
        state.ballX = ballPosition.x;
        state.ballY = ballPosition.y;

        // Velocidade 0 enquanto a bola está parada (saque/pausa), para o cliente não extrapolar.
        bool moving = IsPlaying && serveTimer <= 0f;
        state.ballVx = moving ? ballVelocity.x : 0f;
        state.ballVy = moving ? ballVelocity.y : 0f;

        // "gameStarted" para o cliente = partida realmente rodando (ou terminada).
        state.gameStarted = gameOver || IsPlaying;
        state.gameOver = gameOver;
        state.loserId = loserId;

        for (int i = 0; i < 4; i++)
        {
            state.scores[i] = players[i].score;
            state.paddleX[i] = players[i].paddleX;
            state.paddleY[i] = players[i].paddleY;
            state.connected[i] = players[i].connected;
        }

        return state;
    }
}
