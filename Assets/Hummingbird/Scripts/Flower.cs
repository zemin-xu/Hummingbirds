using UnityEngine;

public class Flower : MonoBehaviour
{
    /// <summary>
    /// Manages a single flower with nectar
    /// </summary>

    [Tooltip("The color when the flower is full")]
    public Color fullFlowerColor = new Color(1f, 0f, .3f);

    [Tooltip("The color when the flower is full")]
    public Color emptyFlowerColor = new Color(.5f, 0f, 1f);

    [HideInInspector]
    public Collider nectarCollider;

    private Collider flowerCollider;
    private Material flowerMaterial;

    public Vector3 FlowerUpVector
    {
        get
        {
            return nectarCollider.transform.up;
        }
    }

    public Vector3 FlowerCenterposition
    {
        get
        {
            return nectarCollider.transform.position;
        }
    }

    public float NectarAmount { get; private set; }

    /// <summary>
    /// whether the flower has any nectar remaining
    /// </summary>
    public bool HasNectar
    {
        get
        {
            return NectarAmount > 0f;
        }
    }

    /// <summary>
    /// attempts to remove nectar from the flower
    /// </summary>
    /// <param name="amount">the amount of nectar to remove</param>
    /// <returns>the amount successfully removed</returns>
    public float Feed(float amount)
    {
        // track how much nectar was successfully taken (cannot take more than is available)
        float nectarTaken = Mathf.Clamp(amount, 0f, NectarAmount);

        NectarAmount -= amount;

        if (NectarAmount <= 0)
        {
            NectarAmount = 0;

            flowerCollider.gameObject.SetActive(false);
            nectarCollider.gameObject.SetActive(false);

            flowerMaterial.SetColor("_BaseColor", emptyFlowerColor);
        }

        return nectarTaken;
    }

    public void ResetFlower()
    {
        NectarAmount = 1f;

        flowerCollider.gameObject.SetActive(true);
        nectarCollider.gameObject.SetActive(true);

        flowerMaterial.SetColor("_BaseColor", fullFlowerColor);
    }

    private void Awake()
    {
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        flowerMaterial = meshRenderer.material;

        flowerCollider = transform.Find("FlowerCollider").GetComponent<Collider>();
        nectarCollider = transform.Find("FlowerNectarCollider").GetComponent<Collider>();
    }
}