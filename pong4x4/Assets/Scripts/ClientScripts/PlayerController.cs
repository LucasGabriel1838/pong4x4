using UnityEngine;

// Componente visual de uma raquete. A posição oficial vem do servidor;
// aqui só suavizamos o movimento entre um pacote e outro.
public class PlayerController : MonoBehaviour
{
    public int playerId;
    public bool horizontal = true;
    public float smoothing = 25f;

    private Vector3 targetPosition;
    private bool hasTarget;

    // Tamanho do sprite base: 1x1 unidade; a escala define o comprimento/espessura.
    public void SetOrientation(bool isHorizontal, float length, float thickness)
    {
        horizontal = isHorizontal;
        transform.localScale = horizontal
            ? new Vector3(length, thickness, 1f)
            : new Vector3(thickness, length, 1f);
    }

    public void SetServerPosition(Vector2 position)
    {
        Vector3 p = new Vector3(position.x, position.y, transform.position.z);
        if (!hasTarget)
        {
            transform.position = p;
            hasTarget = true;
        }
        targetPosition = p;
    }

    private void Update()
    {
        if (!hasTarget) return;
        float t = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, targetPosition, t);
    }
}
