using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// manages a collection of flower plants
/// </summary>
public class FlowerArea : MonoBehaviour
{
    // the diameter of the area where the agent and flowers can be
    // used for observing relative distance from agent to flower
    public const float AreaDiameter = 20f;

    // the list of all flower plants in this flower area (flower plants have multiple flowers)
    private List<GameObject> flowerPlants;

    // a lookup dictionary for looking up a flower from a nectar collider
    private Dictionary<Collider, Flower> nectarFlowerDict;

    // the list of all flowers in the flower area
    public List<Flower> Flowers { get; private set; }

    // reset flowers and flower plants
    public void ResetFlowers()
    {
        // rotate each flower plant around the Y axis subtly around X and Z
        foreach (GameObject flowerPlant in flowerPlants)
        {
            float xRot = UnityEngine.Random.Range(-5f, 5f);
            float yRot = UnityEngine.Random.Range(-180f, 180f);
            float zRot = UnityEngine.Random.Range(-5f, 5f);

            flowerPlant.transform.localRotation = Quaternion.Euler(xRot, yRot, zRot);
        }

        foreach (Flower flower in Flowers)
        {
            flower.ResetFlower();
        }
    }

    /// <summary>
    /// get the <see cref="Flower"/> that a nectar collider belongs to
    /// </summary>
    /// <param name="collider">the nectar collider</param>
    /// <returns>the matching flower</returns>
    public Flower GetFlowerFromNectar(Collider collider)
    {
        return nectarFlowerDict[collider];
    }

    private void Awake()
    {
        flowerPlants = new List<GameObject>();
        nectarFlowerDict = new Dictionary<Collider, Flower>();
        Flowers = new List<Flower>();
    }

    private void Start()
    {
        FindChildFlowers(transform);
    }

    // recrusively find all flowers and flower plants that are children of a parent transform
    private void FindChildFlowers(Transform parent)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);

            if (child.CompareTag("FlowerPlant"))
            {
                flowerPlants.Add(child.gameObject);

                // look for flowers within this flower plant
                FindChildFlowers(child);
            }
            else
            {
                Flower flower = child.GetComponent<Flower>();
                if (flower != null)
                {
                    Flowers.Add(flower);
                    nectarFlowerDict.Add(flower.nectarCollider, flower);
                }
                else
                {
                    // check this child
                    FindChildFlowers(child);
                }
            }
        }
    }
}