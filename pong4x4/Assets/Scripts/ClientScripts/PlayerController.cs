using UnityEngine;

// Componente visual de uma raquete. A posição oficial vem do servidor.
public class PlayerController : MonoBehaviour
{
    public int playerId;
    public bool horizontal = true;

    public void SetServerPosition(Vector2 position)
    {
        transform.position = new Vector3(position.x, position.y, 0f);
    }

    public void SetOrientation(bool isHorizontal)
    {
        horizontal = isHorizontal;
        transform.localScale = horizontal
            ? new Vector3(1.8f, 0.22f, 1f)
            : new Vector3(0.22f, 1.8f, 1f);
    }
}
