using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Funciona com o Input Manager antigo, com o Input System novo ou com "Both"
// (Project Settings > Player > Active Input Handling). Não precisa mudar nada.
public class ClientInput : MonoBehaviour
{
    public UDPClient udpClient;
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

        // Também serve de "heartbeat": o servidor derruba quem fica sem enviar nada.
        udpClient.SendInput(ReadAxisForPlayer(udpClient.PlayerId));
    }

    // Topo (0) e baixo (2) movem na horizontal: A/D ou setas esquerda/direita.
    // Direita (1) e esquerda (3) movem na vertical: W/S ou setas cima/baixo.
    // Só a janela em foco recebe teclado, então não há conflito ao testar vários clientes no mesmo PC.
    private float ReadAxisForPlayer(int playerId)
    {
        if (playerId < 0) return 0f;

        bool horizontal = playerId == 0 || playerId == 2;
        float positive;
        float negative;

        if (horizontal)
        {
            positive = (KeyHeld(KeyCode.D) || KeyHeld(KeyCode.RightArrow)) ? 1f : 0f;
            negative = (KeyHeld(KeyCode.A) || KeyHeld(KeyCode.LeftArrow)) ? 1f : 0f;
        }
        else
        {
            positive = (KeyHeld(KeyCode.W) || KeyHeld(KeyCode.UpArrow)) ? 1f : 0f;
            negative = (KeyHeld(KeyCode.S) || KeyHeld(KeyCode.DownArrow)) ? 1f : 0f;
        }

        return positive - negative;
    }

    private static bool KeyHeld(KeyCode key)
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        Keyboard kb = Keyboard.current;
        if (kb == null) return false;
        switch (key)
        {
            case KeyCode.A: return kb.aKey.isPressed;
            case KeyCode.D: return kb.dKey.isPressed;
            case KeyCode.W: return kb.wKey.isPressed;
            case KeyCode.S: return kb.sKey.isPressed;
            case KeyCode.LeftArrow: return kb.leftArrowKey.isPressed;
            case KeyCode.RightArrow: return kb.rightArrowKey.isPressed;
            case KeyCode.UpArrow: return kb.upArrowKey.isPressed;
            case KeyCode.DownArrow: return kb.downArrowKey.isPressed;
            default: return false;
        }
#else
        return Input.GetKey(key);
#endif
    }
}
