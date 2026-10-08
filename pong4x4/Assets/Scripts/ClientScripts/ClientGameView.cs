
using UnityEngine;
using UnityEngine.UI;

public class ClientGameView : MonoBehaviour
{
    [Header("Referencias")]
    public UDPClient udpClient;
    public Camera targetCamera;
    public GameObject ballPrefab;
    public GameObject paddlePrefab;
    public Text statusText;
    public Text scoreText;

    [Header("Arena")]
    public float halfWidth = 8f;
    public float halfHeight = 5f;
    public bool createArenaOnStart = true;

    private Transform ballTransform;
    private readonly Transform[] paddles = new Transform[4];
    private bool built;

    private static Sprite squareSprite;

    private readonly Color[] playerColors =
    {
        new Color(1.00f, 0.22f, 0.58f), // P1 - Rosa
        new Color(0.12f, 0.75f, 0.48f), // P2 - Verde
        new Color(0.15f, 0.48f, 1.00f), // P3 - Azul
        new Color(1.00f, 0.72f, 0.12f)  // P4 - Dourado
    };

    private void Awake()
    {
        if (udpClient == null)
            udpClient = GetComponent<UDPClient>();

        if (targetCamera == null)
            targetCamera = Camera.main;
    }

    private void Start()
    {
        BuildView();

        if (udpClient != null)
        {
            udpClient.StateReceived += OnStateReceived;
            udpClient.StatusChanged += OnStatusChanged;
        }
        else
        {
            Debug.LogError(
                "ClientGameView: UDPClient nao encontrado no objeto Client."
            );
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

    private void BuildView()
    {
        if (built)
            return;

        built = true;

        ConfigureCamera();
        RemoveOldObjects();

        if (createArenaOnStart)
            CreateArena();

        CreateBall();
        CreatePaddles();
    }

    private void ConfigureCamera()
    {
        if (targetCamera == null)
        {
            Debug.LogError("ClientGameView: nenhuma camera encontrada.");
            return;
        }

        targetCamera.orthographic = true;
        targetCamera.transform.position =
            new Vector3(0f, 0f, -10f);

        targetCamera.transform.rotation = Quaternion.identity;
        targetCamera.orthographicSize = halfHeight + 1.2f;
        targetCamera.backgroundColor =
            new Color(0.035f, 0.045f, 0.08f);
        targetCamera.nearClipPlane = 0.1f;
        targetCamera.farClipPlane = 100f;
    }

    private Sprite GetSquareSprite()
    {
        if (squareSprite != null)
            return squareSprite;

        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.name = "GeneratedWhitePixel";
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();

        squareSprite = Sprite.Create(
            texture,
            new Rect(0, 0, 1, 1),
            new Vector2(0.5f, 0.5f),
            1f
        );

        squareSprite.name = "GeneratedSquareSprite";
        return squareSprite;
    }

    private void RemoveOldObjects()
    {
        string[] oldNames =
        {
            "TopZone",
            "TopWall",
            "BottomWall",
            "LeftWall",
            "RightWall",
            "VerticalDivider",
            "HorizontalDivider",
            "Zone_P1_Top",
            "Zone_P2_Right",
            "Zone_P3_Bottom",
            "Zone_P4_Left",
            "NetworkBall",
            "Ball",
            "Paddle_P1",
            "Paddle_P2",
            "Paddle_P3",
            "Paddle_P4"
        };

        foreach (string objectName in oldNames)
        {
            GameObject oldObject = GameObject.Find(objectName);

            if (oldObject != null)
                Destroy(oldObject);
        }
    }

    private void CreateArena()
    {
        // Quatro quadrantes independentes.
        // Sorting Order garante que as linhas aparecam por cima.

        float zoneWidth = halfWidth - 0.045f;
        float zoneHeight = halfHeight - 0.045f;

        CreateSprite(
            "Zone_P1",
            new Vector2(-halfWidth / 2f, halfHeight / 2f),
            new Vector2(zoneWidth, zoneHeight),
            playerColors[0],
            -2f,
            0
        );

        CreateSprite(
            "Zone_P2",
            new Vector2(halfWidth / 2f, halfHeight / 2f),
            new Vector2(zoneWidth, zoneHeight),
            playerColors[1],
            -2f,
            0
        );

        CreateSprite(
            "Zone_P3",
            new Vector2(-halfWidth / 2f, -halfHeight / 2f),
            new Vector2(zoneWidth, zoneHeight),
            playerColors[2],
            -2f,
            0
        );

        CreateSprite(
            "Zone_P4",
            new Vector2(halfWidth / 2f, -halfHeight / 2f),
            new Vector2(zoneWidth, zoneHeight),
            playerColors[3],
            -2f,
            0
        );

        Color wallColor = Color.white;

        // Paredes externas.
        CreateSprite(
            "TopWall",
            new Vector2(0f, halfHeight),
            new Vector2(halfWidth * 2f, 0.12f),
            wallColor, 0f, 10
        );

        CreateSprite(
            "BottomWall",
            new Vector2(0f, -halfHeight),
            new Vector2(halfWidth * 2f, 0.12f),
            wallColor, 0f, 10
        );

        CreateSprite(
            "LeftWall",
            new Vector2(-halfWidth, 0f),
            new Vector2(0.12f, halfHeight * 2f),
            wallColor, 0f, 10
        );

        CreateSprite(
            "RightWall",
            new Vector2(halfWidth, 0f),
            new Vector2(0.12f, halfHeight * 2f),
            wallColor, 0f, 10
        );

        // Divisorias centrais.
        Color dividerColor = new Color(1f, 1f, 1f, 0.8f);

        CreateSprite(
            "VerticalDivider",
            Vector2.zero,
            new Vector2(0.045f, halfHeight * 2f),
            dividerColor, 0f, 11
        );

        CreateSprite(
            "HorizontalDivider",
            Vector2.zero,
            new Vector2(halfWidth * 2f, 0.045f),
            dividerColor, 0f, 11
        );
    }

    private GameObject CreateSprite(
        string objectName,
        Vector2 position,
        Vector2 size,
        Color color,
        float zPosition,
        int sortingOrder)
    {
        GameObject obj = new GameObject(objectName);

        obj.transform.position =
            new Vector3(position.x, position.y, zPosition);

        obj.transform.localScale =
            new Vector3(size.x, size.y, 1f);

        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = GetSquareSprite();
        sr.color = color;
        sr.sortingOrder = sortingOrder;

        return obj;
    }

    private void CreateBall()
    {
        GameObject ball;

        if (ballPrefab != null)
        {
            ball = Instantiate(ballPrefab);
            ball.name = "NetworkBall";
            ball.transform.position = new Vector3(0f, 0f, -0.2f);
        }
        else
        {
            ball = CreateSprite(
                "NetworkBall",
                Vector2.zero,
                new Vector2(0.28f, 0.28f),
                Color.white,
                -0.2f,
                20
            );
        }

        ballTransform = ball.transform;

        foreach (Collider col in ball.GetComponentsInChildren<Collider>())
            Destroy(col);

        foreach (Collider2D col in ball.GetComponentsInChildren<Collider2D>())
            Destroy(col);
    }

    private void CreatePaddles()
    {
        for (int i = 0; i < 4; i++)
        {
            bool horizontal = i == 0 || i == 2;

            GameObject paddle;

            if (paddlePrefab != null)
            {
                paddle = Instantiate(paddlePrefab);
                paddle.name = "Paddle_P" + (i + 1);
            }
            else
            {
                Vector2 size = horizontal
                    ? new Vector2(1.8f, 0.22f)
                    : new Vector2(0.22f, 1.8f);

                paddle = CreateSprite(
                    "Paddle_P" + (i + 1),
                    Vector2.zero,
                    size,
                    playerColors[i],
                    -0.1f,
                    20
                );
            }

            paddles[i] = paddle.transform;

            PlayerController controller =
                paddle.GetComponent<PlayerController>();

            if (controller == null)
                controller = paddle.AddComponent<PlayerController>();

            controller.playerId = i;
            controller.SetOrientation(horizontal);

            SpriteRenderer sr = paddle.GetComponent<SpriteRenderer>();

            if (sr == null)
                sr = paddle.GetComponentInChildren<SpriteRenderer>();

            if (sr != null)
            {
                sr.color = playerColors[i];
                sr.sortingOrder = 20;
            }

            foreach (Collider col in paddle.GetComponentsInChildren<Collider>())
                Destroy(col);

            foreach (Collider2D col in paddle.GetComponentsInChildren<Collider2D>())
                Destroy(col);
        }
    }

    private void OnStateReceived(NetMessage state)
    {
        if (state == null)
            return;

        if (ballTransform != null)
        {
            ballTransform.position =
                new Vector3(state.ballX, state.ballY, -0.2f);
        }

        for (int i = 0; i < 4; i++)
        {
            if (paddles[i] == null)
                continue;

            paddles[i].position =
                new Vector3(state.paddleX[i], state.paddleY[i], -0.1f);
        }

        if (statusText != null)
        {
            if (state.gameOver)
            {
                statusText.text = state.loserId >= 0
                    ? "P" + (state.loserId + 1) + " PERDEU!"
                    : "FIM DE JOGO";
            }
            else if (state.gameStarted)
            {
                statusText.text = "PARTIDA EM ANDAMENTO";
            }
            else
            {
                statusText.text = "Aguardando 4 jogadores...";
            }
        }

        if (scoreText != null)
        {
            scoreText.text =
                "P1: " + state.scores[0] +
                "    P2: " + state.scores[1] +
                "    P3: " + state.scores[2] +
                "    P4: " + state.scores[3];
        }
    }

    private void OnStatusChanged(string message)
    {
        if (statusText != null)
            statusText.text = message;

        Debug.Log(message);
    }
}