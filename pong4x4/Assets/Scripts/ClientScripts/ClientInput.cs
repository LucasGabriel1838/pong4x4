using UnityEngine;

public class ClientInput : MonoBehaviour
{
    public UDPClient udpClient;
    [Tooltip("Opcional: deixe vazio para usar o input conforme o ID recebido.")]
    public bool sendOnlyWhenConnected = true;
    public float sendRate = 30f;

    private float timer;

    private void Awake()
    {
        if (udpClient == null) udpClient = GetComponent<UDPClient>();
    }

    private void Update()
    {
        if (udpClient == null) return;
        if (sendOnlyWhenConnected && !udpClient.IsConnected) return;

        timer += Time.deltaTime;
        float interval = 1f / Mathf.Max(1f, sendRate);
        if (timer < interval) return;
        timer = 0f;

        float axis = ReadAxisForPlayer(udpClient.PlayerId);
        udpClient.SendInput(axis);
    }

    private float ReadAxisForPlayer(int playerId)
    {
        // Teclas independentes para testar quatro clientes no mesmo computador.
        // Topo: A/D; direita: W/S; baixo: setas esquerda/direita; esquerda: setas baixo/cima.
        // A leitura depende do ID atribuído pelo servidor.
        if (playerId == 0)
            return (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f);

        if (playerId == 1)
            return (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f);

        if (playerId == 2)
            return (Input.GetKey(KeyCode.RightArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);

        if (playerId == 3)
            return (Input.GetKey(KeyCode.UpArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);

        return 0f;
    }
}
