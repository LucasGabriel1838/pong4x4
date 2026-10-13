using UnityEngine;
using UnityEngine.UI;

// Desenha o estado recebido do servidor. Tudo é feito com SpriteRenderer (2D) e shader
// "Sprites/Default" (sem luz), então aparece em qualquer template: 2D Built-in, URP, URP 2D.
public class ClientGameView : MonoBehaviour
{
    [Header("Referências")]
    public UDPClient udpClient;
    public Camera targetCamera;
    [Tooltip("Opcional. Precisa ser um sprite 1x1 unidade (ex.: Circle do Unity). A escala é ajustada pelo script.")]
    public GameObject ballPrefab;
    [Tooltip("Opcional. Precisa ser um sprite 1x1 unidade (ex.: Square do Unity). A escala é ajustada pelo script.")]
    public GameObject paddlePrefab;
    [Tooltip("Opcional. Se vazio, um texto simples é desenhado via OnGUI.")]
    public Text statusText;
    public Text scoreText;

    [Header("Arena (igual ao GameManager do servidor!)")]
    public float halfWidth = 8f;
    public float halfHeight = 5f;
    public float paddleLength = 1.8f;
    public float paddleThickness = 0.22f;
    public float ballRadius = 0.18f;
    public bool createArenaOnStart = true;

    private static readonly string[] SideNames = { "TOPO", "DIREITA", "BAIXO", "ESQUERDA" };
    private static readonly string[] KeyHints = { "A/D ou ←/→", "W/S ou ↑/↓", "A/D ou ←/→", "W/S ou ↑/↓" };

    private readonly Color[] colors = {
        new Color(1f, 0.22f, 0.58f),
        new Color(0.12f, 0.88f, 0.62f),
        new Color(0.15f, 0.65f, 1f),
        new Color(1f, 0.78f, 0.15f)
    };

    private Transform ballTransform;
    private readonly PlayerController[] paddles = new PlayerController[4];
    private readonly SpriteRenderer[] paddleRenderers = new SpriteRenderer[4];
    private bool built;

    private Sprite squareSprite;
    private Sprite circleSprite;
    private Material spriteMaterial;

    private Vector2 ballTarget;
    private Vector2 ballVelocity;
    private float ballStateTime;
    private bool hasBall;

    private string statusMessage = "Conectando...";
    private string hudStatus = "";
    private string hudScores = "";
    private int hudMyId = -1;

    private void Awake()
    {
        if (udpClient == null) udpClient = GetComponent<UDPClient>();
        if (targetCamera == null) targetCamera = Camera.main;
    }

    private void Start()
    {
        BuildView();
        if (udpClient != null)
        {
            udpClient.StateReceived += OnStateReceived;
            udpClient.StatusChanged += OnStatusChanged;
        }
    }

    private void OnDestroy()
    {
        if (udpClient != null)
        {
            udpClient.StateReceived -= OnStateReceived;
            udpClient.StatusChanged -= OnStatusChanged;
        }
    }

    // ---------------------------------------------------------------- construção

    private void BuildView()
    {
        if (built) return;
        built = true;

        CreateSharedSpriteAssets();

        if (targetCamera != null)
        {
            targetCamera.orthographic = true;
            targetCamera.transform.position = new Vector3(0f, 0f, -10f);
            targetCamera.clearFlags = CameraClearFlags.SolidColor;
            targetCamera.backgroundColor = new Color(0.055f, 0.07f, 0.12f);
            FitCamera();
        }

        if (createArenaOnStart)
            CreateArena();

        GameObject ball = ballPrefab != null ? Instantiate(ballPrefab) : CreateSpriteObject("Ball", circleSprite, Color.white, 2);
        ball.name = "NetworkBall";
        ballTransform = ball.transform;
        ballTransform.position = Vector3.zero;
        ballTransform.localScale = new Vector3(ballRadius * 2f, ballRadius * 2f, 1f);

        for (int i = 0; i < 4; i++)
        {
            GameObject paddle = paddlePrefab != null
                ? Instantiate(paddlePrefab)
                : CreateSpriteObject("Paddle_P" + (i + 1), squareSprite, colors[i], 1);

            paddle.name = "Paddle_P" + (i + 1);

            PlayerController controller = paddle.GetComponent<PlayerController>();
            if (controller == null) controller = paddle.AddComponent<PlayerController>();
            controller.playerId = i;
            controller.SetOrientation(i == 0 || i == 2, paddleLength, paddleThickness);
            paddles[i] = controller;

            SpriteRenderer sr = paddle.GetComponent<SpriteRenderer>();
            if (sr == null) sr = paddle.GetComponentInChildren<SpriteRenderer>();
            if (sr != null) sr.color = colors[i];
            paddleRenderers[i] = sr;
        }
    }

    private void CreateSharedSpriteAssets()
    {
        Texture2D whiteTex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        Color[] white = new Color[16];
        for (int i = 0; i < white.Length; i++) white[i] = Color.white;
        whiteTex.SetPixels(white);
        whiteTex.Apply();
        whiteTex.filterMode = FilterMode.Point;
        squareSprite = Sprite.Create(whiteTex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);

        const int size = 64;
        Texture2D circleTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] px = new Color[size * size];
        float r = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - r;
                float dy = y + 0.5f - r;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(r - d);   // borda suavizada
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }
        circleTex.SetPixels(px);
        circleTex.Apply();
        circleTex.filterMode = FilterMode.Bilinear;
        circleSprite = Sprite.Create(circleTex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);

        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null) spriteMaterial = new Material(shader);
    }

    private GameObject CreateSpriteObject(string objectName, Sprite sprite, Color color, int sortingOrder)
    {
        GameObject obj = new GameObject(objectName);
        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = sortingOrder;
        if (spriteMaterial != null) sr.sharedMaterial = spriteMaterial;
        return obj;
    }

    private void CreateArena()
    {
        float t = 0.12f;
        // Cada parede tem a cor do jogador que defende aquele lado.
        CreateRect("Wall_Top_P1", new Vector2(0, halfHeight + t * 0.5f), new Vector2(halfWidth * 2f + t * 2f, t), colors[0]);
        CreateRect("Wall_Right_P2", new Vector2(halfWidth + t * 0.5f, 0), new Vector2(t, halfHeight * 2f + t * 2f), colors[1]);
        CreateRect("Wall_Bottom_P3", new Vector2(0, -halfHeight - t * 0.5f), new Vector2(halfWidth * 2f + t * 2f, t), colors[2]);
        CreateRect("Wall_Left_P4", new Vector2(-halfWidth - t * 0.5f, 0), new Vector2(t, halfHeight * 2f + t * 2f), colors[3]);

        Color faint = new Color(1f, 1f, 1f, 0.12f);
        CreateRect("VerticalDivider", Vector2.zero, new Vector2(0.03f, halfHeight * 2f), faint);
        CreateRect("HorizontalDivider", Vector2.zero, new Vector2(halfWidth * 2f, 0.03f), faint);
    }

    private void CreateRect(string objectName, Vector2 position, Vector2 size, Color color)
    {
        GameObject obj = CreateSpriteObject(objectName, squareSprite, color, -10);
        obj.transform.position = new Vector3(position.x, position.y, 0f);
        obj.transform.localScale = new Vector3(size.x, size.y, 1f);
    }

    private void FitCamera()
    {
        if (targetCamera == null) return;
        float aspect = Mathf.Max(0.1f, targetCamera.aspect);
        float byHeight = halfHeight + 1.2f;
        float byWidth = (halfWidth + 1.2f) / aspect;
        targetCamera.orthographicSize = Mathf.Max(byHeight, byWidth);
    }

    // ---------------------------------------------------------------- atualização

    private void Update()
    {
        FitCamera();

        if (ballTransform == null || !hasBall) return;

        // Extrapola um pouco com a velocidade enviada pelo servidor (que é 0 quando a bola está parada).
        float age = Mathf.Min(Time.realtimeSinceStartup - ballStateTime, 0.1f);
        Vector2 predicted = ballTarget + ballVelocity * age;

        Vector3 current = ballTransform.position;
        Vector3 target = new Vector3(predicted.x, predicted.y, 0f);
        if ((target - current).sqrMagnitude > 2.25f)
            ballTransform.position = target;                                   // saque/reset: teleporta
        else
            ballTransform.position = Vector3.Lerp(current, target, 1f - Mathf.Exp(-35f * Time.deltaTime));
    }

    private void OnStateReceived(NetMessage state)
    {
        if (state == null || state.paddleX == null || state.paddleY == null ||
            state.scores == null || state.connected == null ||
            state.paddleX.Length < 4 || state.paddleY.Length < 4 ||
            state.scores.Length < 4 || state.connected.Length < 4)
            return;

        ballTarget = new Vector2(state.ballX, state.ballY);
        ballVelocity = new Vector2(state.ballVx, state.ballVy);
        ballStateTime = Time.realtimeSinceStartup;
        if (!hasBall && ballTransform != null)
            ballTransform.position = new Vector3(ballTarget.x, ballTarget.y, 0f);
        hasBall = true;

        for (int i = 0; i < 4; i++)
        {
            if (paddles[i] != null)
                paddles[i].SetServerPosition(new Vector2(state.paddleX[i], state.paddleY[i]));

            if (paddleRenderers[i] != null)
            {
                Color c = colors[i];
                c.a = state.connected[i] ? 1f : 0.3f;   // raquete de quem não entrou fica apagada
                paddleRenderers[i].color = c;
            }
        }

        hudMyId = udpClient != null ? udpClient.PlayerId : -1;

        if (state.gameOver)
            hudStatus = state.loserId >= 0 ? "P" + (state.loserId + 1) + " PERDEU! Nova partida em instantes..." : "FIM DE JOGO";
        else if (state.gameStarted)
            hudStatus = "PARTIDA EM ANDAMENTO";
        else
        {
            int count = 0;
            for (int i = 0; i < 4; i++) if (state.connected[i]) count++;
            hudStatus = "Aguardando 4 jogadores... (" + count + "/4)";
        }

        hudScores = "P1: " + state.scores[0] + "   P2: " + state.scores[1] +
                    "   P3: " + state.scores[2] + "   P4: " + state.scores[3];

        if (statusText != null) statusText.text = hudStatus;
        if (scoreText != null) scoreText.text = hudScores;
    }

    private void OnStatusChanged(string message)
    {
        statusMessage = message;
        if (statusText != null) statusText.text = message;
        Debug.Log(message);
    }

    // HUD de reserva: aparece mesmo sem Canvas/Text configurados.
    private void OnGUI()
    {
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = Mathf.Max(14, Screen.height / 36);
        style.fontStyle = FontStyle.Bold;
        style.normal.textColor = Color.white;

        float h = style.fontSize * 1.6f;
        float y = 6f;

        string me = (udpClient != null && udpClient.IsConnected && udpClient.PlayerId >= 0)
            ? "Você: P" + (udpClient.PlayerId + 1) + " (" + SideNames[udpClient.PlayerId] + ")  —  " + KeyHints[udpClient.PlayerId]
            : statusMessage;

        if (hudMyId >= 0 && hudMyId < 4 && udpClient != null && udpClient.IsConnected)
            style.normal.textColor = colors[hudMyId];
        GUI.Label(new Rect(10f, y, Screen.width - 20f, h), me, style);
        style.normal.textColor = Color.white;

        if (statusText == null && udpClient != null && udpClient.IsConnected)
        {
            y += h;
            GUI.Label(new Rect(10f, y, Screen.width - 20f, h), hudStatus, style);
        }

        if (scoreText == null && udpClient != null && udpClient.IsConnected)
        {
            y += h;
            GUI.Label(new Rect(10f, y, Screen.width - 20f, h), hudScores + "   (penalidades)", style);
        }
    }
}
