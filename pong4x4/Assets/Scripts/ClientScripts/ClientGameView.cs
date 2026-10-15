using UnityEngine;
using UnityEngine.UI;

// Desenha o estado recebido do servidor. Tudo é feito com SpriteRenderer (2D) e shader
// "Sprites/Default" (sem luz), então aparece em qualquer template: 2D Built-in, URP, URP 2D.
// O tamanho da arena, das raquetes e dos cantos vem do servidor (GameManager): não há
// mais valores para "casar" na mão entre os dois projetos.
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
    public bool createArenaOnStart = true;

    private static readonly string[] SideNames = { "TOPO", "DIREITA", "BAIXO", "ESQUERDA" };
    private static readonly string[] KeyHints = { "A/D ou ←/→", "W/S ou ↑/↓", "A/D ou ←/→", "W/S ou ↑/↓" };

    private readonly Color[] colors = {
        new Color(1f, 0.22f, 0.58f),
        new Color(0.12f, 0.88f, 0.62f),
        new Color(0.15f, 0.65f, 1f),
        new Color(1f, 0.78f, 0.15f)
    };

    // Configuração recebida do servidor (valores padrão só valem até o primeiro pacote).
    private float arenaHalf = 5f;
    private float paddleLength = 1.8f;
    private float paddleThickness = 0.22f;
    private float ballRadius = 0.18f;
    private float cornerSize = 1f;
    private float cannonRadius = 0.45f;

    private float bumperRadius = 0f;      // 0 = sem bumpers (desligados no servidor)
    private float bumperOffset = 2.25f;
    private float powerupRadius = 0.3f;

    private Transform ballTransform;
    private SpriteRenderer ballRenderer;
    private readonly PlayerController[] paddles = new PlayerController[4];
    private readonly SpriteRenderer[] paddleRenderers = new SpriteRenderer[4];
    private bool built;

    // Canhões
    private readonly Transform[] cannonPivots = new Transform[4];
    private readonly SpriteRenderer[] cannonBases = new SpriteRenderer[4];
    private readonly float[] cannonTargetAngle = new float[4];
    private readonly float[] cannonCurrentAngle = new float[4];
    private int heldCannon = -1;

    // Bumpers e power-up
    private readonly Transform[] bumperTransforms = new Transform[4];
    private readonly SpriteRenderer[] bumperCores = new SpriteRenderer[4];
    private readonly int[] lastBumperHits = new int[4];
    private readonly float[] bumperFlash = new float[4];
    private bool bumperHitsInit;
    private GameObject powerupObject;
    private SpriteRenderer powerupBody;
    private bool boostActive;

    // Tipos de power-up (iguais ao servidor): 0 turbo, 1 raquete grande, 2 escudo, 3 fantasma, 4 congelar.
    private static readonly string[] PowerupNames = { "TURBO", "RAQUETE +", "ESCUDO", "FANTASMA", "CONGELAR" };
    private static readonly Color[] PowerupColors = {
        new Color(1f, 0.82f, 0.15f),
        new Color(0.30f, 0.90f, 0.40f),
        new Color(0.30f, 0.60f, 1f),
        new Color(0.80f, 0.80f, 0.92f),
        new Color(0.55f, 1f, 1f)
    };
    private static readonly Color BarrelColor = new Color(0.62f, 0.66f, 0.74f);
    private readonly GameObject[] shieldRings = new GameObject[4];
    private readonly SpriteRenderer[] cannonBarrels = new SpriteRenderer[4];
    private SpriteRenderer ballAura;
    private int powerupType;
    private int lastHitter = -1;
    private bool ghost;
    private int lastPickupSeq = -1;
    private string toast = "";
    private Color toastColor = Color.white;
    private float toastUntil;

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
        // A arena só é desenhada quando o primeiro pacote do servidor chegar (ele traz as medidas).
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

    private void ApplyConfig(NetMessage state)
    {
        if (state.arenaHalf > 0.5f) arenaHalf = state.arenaHalf;
        if (state.paddleLength > 0.01f) paddleLength = state.paddleLength;
        if (state.paddleThickness > 0.01f) paddleThickness = state.paddleThickness;
        if (state.ballRadius > 0.01f) ballRadius = state.ballRadius;
        if (state.cannonRadius > 0.01f) cannonRadius = state.cannonRadius;
        cornerSize = Mathf.Max(0f, state.cornerSize);   // 0 = canhões desligados no servidor
        bumperRadius = Mathf.Max(0f, state.bumperRadius);
        if (state.bumperOffset > 0.01f) bumperOffset = state.bumperOffset;
        if (state.powerupRadius > 0.01f) powerupRadius = state.powerupRadius;
    }

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

        if (cornerSize > 0.01f)
            CreateCannons();

        if (bumperRadius > 0.01f)
            CreateBumpers();

        CreatePowerup();

        GameObject ball = ballPrefab != null ? Instantiate(ballPrefab) : CreateSpriteObject("Ball", circleSprite, Color.white, 2);
        ball.name = "NetworkBall";
        ballTransform = ball.transform;
        ballTransform.position = Vector3.zero;
        ballTransform.localScale = new Vector3(ballRadius * 2f, ballRadius * 2f, 1f);
        ballRenderer = ball.GetComponent<SpriteRenderer>();
        if (ballRenderer == null) ballRenderer = ball.GetComponentInChildren<SpriteRenderer>();

        // Aura dourada do turbo (filha da bola: a escala é relativa a ela).
        GameObject aura = CreateSpriteObject("BallAura", circleSprite, new Color(1f, 0.82f, 0.2f, 0.45f), 1);
        aura.transform.SetParent(ballTransform, false);
        aura.transform.localScale = new Vector3(1.9f, 1.9f, 1f);
        ballAura = aura.GetComponent<SpriteRenderer>();
        aura.SetActive(false);

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

            // Halo do escudo (filho da raquete; eixos invertidos nas raquetes verticais).
            bool horizontalPaddle = i == 0 || i == 2;
            GameObject ring = CreateSpriteObject("ShieldRing_" + i, squareSprite, new Color(0.35f, 0.70f, 1f, 0.55f), 0);
            ring.transform.SetParent(paddle.transform, false);
            ring.transform.localScale = horizontalPaddle ? new Vector3(1.12f, 2.8f, 1f) : new Vector3(2.8f, 1.12f, 1f);
            ring.SetActive(false);
            shieldRings[i] = ring;
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
        float h = arenaHalf;
        float t = 0.12f;
        // Cada parede tem a cor do jogador que defende aquele lado.
        CreateRect("Wall_Top_P1", new Vector2(0, h + t * 0.5f), new Vector2(h * 2f + t * 2f, t), colors[0]);
        CreateRect("Wall_Right_P2", new Vector2(h + t * 0.5f, 0), new Vector2(t, h * 2f + t * 2f), colors[1]);
        CreateRect("Wall_Bottom_P3", new Vector2(0, -h - t * 0.5f), new Vector2(h * 2f + t * 2f, t), colors[2]);
        CreateRect("Wall_Left_P4", new Vector2(-h - t * 0.5f, 0), new Vector2(t, h * 2f + t * 2f), colors[3]);

        Color faint = new Color(1f, 1f, 1f, 0.12f);
        CreateRect("VerticalDivider", Vector2.zero, new Vector2(0.03f, h * 2f), faint);
        CreateRect("HorizontalDivider", Vector2.zero, new Vector2(h * 2f, 0.03f), faint);
    }

    private void CreateRect(string objectName, Vector2 position, Vector2 size, Color color)
    {
        GameObject obj = CreateSpriteObject(objectName, squareSprite, color, -10);
        obj.transform.position = new Vector3(position.x, position.y, 0f);
        obj.transform.localScale = new Vector3(size.x, size.y, 1f);
    }

    // Canhões: 0 = topo-direita, 1 = baixo-direita, 2 = baixo-esquerda, 3 = topo-esquerda (igual ao servidor).
    private void CreateCannons()
    {
        for (int i = 0; i < 4; i++)
        {
            float sx = (i == 0 || i == 1) ? 1f : -1f;
            float sy = (i == 0 || i == 3) ? 1f : -1f;
            float offset = arenaHalf - cornerSize * 0.5f;
            Vector3 center = new Vector3(sx * offset, sy * offset, 0f);

            // Zona do canto (onde o canhão captura a bola).
            GameObject zone = CreateSpriteObject("CannonZone_" + i, squareSprite, new Color(1f, 1f, 1f, 0.07f), -9);
            zone.transform.position = center;
            zone.transform.localScale = new Vector3(cornerSize, cornerSize, 1f);

            // Base redonda.
            GameObject baseObj = CreateSpriteObject("CannonBase_" + i, circleSprite, new Color(0.30f, 0.33f, 0.40f), -5);
            baseObj.transform.position = center;
            baseObj.transform.localScale = new Vector3(cannonRadius * 2f, cannonRadius * 2f, 1f);
            cannonBases[i] = baseObj.GetComponent<SpriteRenderer>();

            // Cano: o pivô fica no centro da base e gira; o cano é filho dele, deslocado para fora.
            GameObject pivot = new GameObject("CannonPivot_" + i);
            pivot.transform.position = center;
            cannonPivots[i] = pivot.transform;

            float length = cannonRadius * 1.7f;
            GameObject barrel = CreateSpriteObject("CannonBarrel_" + i, squareSprite, new Color(0.62f, 0.66f, 0.74f), -4);
            barrel.transform.SetParent(pivot.transform, false);
            barrel.transform.localPosition = new Vector3(length * 0.5f, 0f, 0f);
            barrel.transform.localScale = new Vector3(length, cannonRadius * 0.55f, 1f);
            cannonBarrels[i] = barrel.GetComponent<SpriteRenderer>();

            // Ângulo inicial: diagonal apontando para o centro da arena.
            float baseAngle = Mathf.Atan2(-sy, -sx) * Mathf.Rad2Deg;
            cannonCurrentAngle[i] = baseAngle;
            cannonTargetAngle[i] = baseAngle;
            pivot.transform.localRotation = Quaternion.Euler(0f, 0f, baseAngle);
        }
    }

    // Bumpers de pinball: um por quadrante (mesma ordem dos cantos).
    private void CreateBumpers()
    {
        for (int i = 0; i < 4; i++)
        {
            float sx = (i == 0 || i == 1) ? 1f : -1f;
            float sy = (i == 0 || i == 3) ? 1f : -1f;

            GameObject outer = CreateSpriteObject("Bumper_" + i, circleSprite, new Color(0.55f, 0.25f, 0.95f), -6);
            outer.transform.position = new Vector3(sx * bumperOffset, sy * bumperOffset, 0f);
            outer.transform.localScale = new Vector3(bumperRadius * 2f, bumperRadius * 2f, 1f);
            bumperTransforms[i] = outer.transform;

            // Miolo claro (filho: a escala é relativa ao bumper).
            GameObject core = CreateSpriteObject("BumperCore_" + i, circleSprite, new Color(0.85f, 0.70f, 1f), -5);
            core.transform.SetParent(outer.transform, false);
            core.transform.localScale = new Vector3(0.55f, 0.55f, 1f);
            bumperCores[i] = core.GetComponent<SpriteRenderer>();
        }
    }

    private void CreatePowerup()
    {
        powerupObject = CreateSpriteObject("Powerup", circleSprite, new Color(1f, 0.82f, 0.15f), 0);
        powerupObject.transform.localScale = new Vector3(powerupRadius * 2f, powerupRadius * 2f, 1f);
        powerupBody = powerupObject.GetComponent<SpriteRenderer>();

        GameObject core = CreateSpriteObject("PowerupCore", circleSprite, Color.white, 1);
        core.transform.SetParent(powerupObject.transform, false);
        core.transform.localScale = new Vector3(0.45f, 0.45f, 1f);

        powerupObject.SetActive(false);   // só aparece quando o servidor mandar
    }

    private void FitCamera()
    {
        if (targetCamera == null) return;
        float aspect = Mathf.Max(0.1f, targetCamera.aspect);
        float byHeight = arenaHalf + 1.2f;
        float byWidth = (arenaHalf + 1.2f) / aspect;
        targetCamera.orthographicSize = Mathf.Max(byHeight, byWidth);
    }

    // ---------------------------------------------------------------- atualização

    private void Update()
    {
        if (!built) return;

        FitCamera();

        // Gira o cano suavemente até o ângulo que o servidor mandou.
        for (int i = 0; i < 4; i++)
        {
            if (cannonPivots[i] == null) continue;
            float t = 1f - Mathf.Exp(-18f * Time.deltaTime);
            cannonCurrentAngle[i] = Mathf.LerpAngle(cannonCurrentAngle[i], cannonTargetAngle[i], t);
            cannonPivots[i].localRotation = Quaternion.Euler(0f, 0f, cannonCurrentAngle[i]);

            if (cannonBases[i] != null)
            {
                // A base acende (laranja) enquanto o canhão está mirando.
                bool charging = heldCannon == i;
                cannonBases[i].color = charging ? new Color(1f, 0.55f, 0.15f) : new Color(0.30f, 0.33f, 0.40f);
            }
        }

        // Bumper "pisca" e dá um soquinho de escala quando leva uma batida.
        for (int i = 0; i < 4; i++)
        {
            if (bumperTransforms[i] == null) continue;
            bumperFlash[i] = Mathf.Max(0f, bumperFlash[i] - Time.deltaTime);
            float f = bumperFlash[i] / 0.2f;
            float size = bumperRadius * 2f * (1f + 0.25f * f);
            bumperTransforms[i].localScale = new Vector3(size, size, 1f);
            if (bumperCores[i] != null)
                bumperCores[i].color = new Color(0.85f + 0.15f * f, 0.70f + 0.30f * f, 1f);
        }

        // Power-up pulsa para chamar atenção.
        if (powerupObject != null && powerupObject.activeSelf)
        {
            float pulse = 1f + 0.15f * Mathf.Sin(Time.realtimeSinceStartup * 6f);
            float size = powerupRadius * 2f * pulse;
            powerupObject.transform.localScale = new Vector3(size, size, 1f);
        }

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

        if (!built)
        {
            ApplyConfig(state);
            BuildView();
        }

        ballTarget = new Vector2(state.ballX, state.ballY);
        ballVelocity = new Vector2(state.ballVx, state.ballVy);
        ballStateTime = Time.realtimeSinceStartup;
        if (!hasBall && ballTransform != null)
            ballTransform.position = new Vector3(ballTarget.x, ballTarget.y, 0f);
        hasBall = true;

        heldCannon = state.heldCannon;
        if (state.cannonAngle != null && state.cannonAngle.Length >= 4)
            for (int i = 0; i < 4; i++) cannonTargetAngle[i] = state.cannonAngle[i];

        if (state.bumperHits != null && state.bumperHits.Length >= 4)
        {
            for (int i = 0; i < 4; i++)
            {
                if (bumperHitsInit && state.bumperHits[i] != lastBumperHits[i]) bumperFlash[i] = 0.2f;
                lastBumperHits[i] = state.bumperHits[i];
            }
            bumperHitsInit = true;
        }

        if (powerupObject != null)
        {
            powerupObject.SetActive(state.powerupActive);
            if (state.powerupActive)
                powerupObject.transform.position = new Vector3(state.powerupX, state.powerupY, 0f);
        }

        boostActive = state.boostActive;
        lastHitter = state.lastHitter;
        ghost = state.ghost;
        powerupType = Mathf.Clamp(state.powerupType, 0, PowerupNames.Length - 1);

        // A bola ganha a cor de quem tocou nela por último (é quem recebe o crédito dos power-ups).
        Color ballColor = (lastHitter >= 0 && lastHitter < 4) ? colors[lastHitter] : Color.white;
        if (ghost) ballColor.a = 0.07f;   // bola fantasma: quase invisível
        if (ballRenderer != null) ballRenderer.color = ballColor;
        if (ballAura != null) ballAura.gameObject.SetActive(boostActive && !ghost);
        if (powerupBody != null) powerupBody.color = PowerupColors[powerupType];

        // O cano do canhão que está segurando a bola também fica na cor de quem tocou por último.
        for (int i = 0; i < 4; i++)
        {
            if (cannonBarrels[i] == null) continue;
            bool holdingColor = heldCannon == i && lastHitter >= 0 && lastHitter < 4;
            cannonBarrels[i].color = holdingColor ? colors[lastHitter] : BarrelColor;
        }

        // Aviso "P2 pegou ESCUDO!" quando o servidor registra uma coleta nova.
        if (state.pickupSeq != lastPickupSeq)
        {
            if (lastPickupSeq >= 0 && state.pickupSeq > lastPickupSeq)
            {
                int t = Mathf.Clamp(state.pickupType, 0, PowerupNames.Length - 1);
                bool hasOwner = state.pickupPlayer >= 0 && state.pickupPlayer < 4;
                toast = (hasOwner ? "P" + (state.pickupPlayer + 1) + " pegou " : "") + PowerupNames[t] + "!";
                toastColor = hasOwner ? colors[state.pickupPlayer] : PowerupColors[t];
                toastUntil = Time.realtimeSinceStartup + 2.5f;
            }
            lastPickupSeq = state.pickupSeq;
        }

        for (int i = 0; i < 4; i++)
        {
            if (paddles[i] != null)
                paddles[i].SetServerPosition(new Vector2(state.paddleX[i], state.paddleY[i]));

            bool horizontalPaddle = i == 0 || i == 2;
            if (paddles[i] != null && state.paddleLen != null && state.paddleLen.Length >= 4 && state.paddleLen[i] > 0.1f)
                paddles[i].SetOrientation(horizontalPaddle, state.paddleLen[i], paddleThickness);   // raquete grande

            if (shieldRings[i] != null)
                shieldRings[i].SetActive(state.shield != null && state.shield.Length >= 4 && state.shield[i]);

            if (paddleRenderers[i] != null)
            {
                Color c = colors[i];
                bool frozen = state.frozen != null && state.frozen.Length >= 4 && state.frozen[i];
                if (frozen)   // congelado: raquete azul-gelo
                    c = new Color(c.r * 0.3f + 0.7f * 0.75f, c.g * 0.3f + 0.7f * 0.95f, c.b * 0.3f + 0.7f, 1f);
                c.a = state.connected[i] ? 1f : 0.3f;   // raquete de quem não entrou fica apagada
                paddleRenderers[i].color = c;
            }
        }

        hudMyId = udpClient != null ? udpClient.PlayerId : -1;

        if (state.gameOver)
            hudStatus = state.loserId >= 0 ? "P" + (state.loserId + 1) + " PERDEU! Nova partida em instantes..." : "FIM DE JOGO";
        else if (state.gameStarted)
            hudStatus = boostActive ? "PARTIDA EM ANDAMENTO  -  BOLA TURBO!" : "PARTIDA EM ANDAMENTO";
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

        // Nome do power-up em cima dele.
        if (powerupObject != null && powerupObject.activeSelf && targetCamera != null)
        {
            Vector3 sp = targetCamera.WorldToScreenPoint(powerupObject.transform.position);
            GUIStyle label = new GUIStyle(style);
            label.alignment = TextAnchor.MiddleCenter;
            label.fontSize = Mathf.Max(12, style.fontSize * 3 / 4);
            label.normal.textColor = PowerupColors[powerupType];
            float lh = label.fontSize * 1.6f;
            GUI.Label(new Rect(sp.x - 100f, Screen.height - sp.y - powerupRadius * 60f - lh, 200f, lh), PowerupNames[powerupType], label);
        }

        // Aviso de coleta, no centro da tela por alguns segundos.
        if (Time.realtimeSinceStartup < toastUntil)
        {
            GUIStyle big = new GUIStyle(style);
            big.alignment = TextAnchor.MiddleCenter;
            big.fontSize = Mathf.Max(18, Screen.height / 22);
            big.normal.textColor = toastColor;
            GUI.Label(new Rect(0f, Screen.height * 0.5f - big.fontSize, Screen.width, big.fontSize * 2f), toast, big);
        }
    }
}
