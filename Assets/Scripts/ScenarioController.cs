using UnityEngine;
using System.Collections;
using System.Collections.Generic;

using VoxSimPlatform.Vox;
using VoxSimPlatform.Global;

public class ScenarioController : MonoBehaviour
{
    public float nearClipRadius;
    public float interactableRadius;
    public GameObject interactableObjects;
    public GameObject backgroundObjects;
    public GameObject surface;
    public Transform objectTypes;
    public List<Transform> interactableObjectTypes;
    public int numTotalObjs;
    public int numInteractableObjs;

    Camera mainCamera;

    Vector3 floorPosition;

    bool objectsInited;

    VoxemeInit voxemeInit;

    // Start is called before the first frame update
    void Start()
    {
        mainCamera = Camera.main;

        floorPosition = new Vector3(mainCamera.transform.position.x, 0.0f, mainCamera.transform.position.z);

        voxemeInit = GameObject.Find("VoxWorld").GetComponent<VoxemeInit>();
        objectsInited = false;
    }

    // Update is called once per frame
    void Update()
    {
        if (!objectsInited)
        {
            PlaceRandomly(surface);
            objectsInited = true;
        }
    }

    void PlaceRandomly(GameObject surface)
    {
        // place interactable objects
        // make sure some object types are set as interactable
        if (interactableObjectTypes.Count > 0)
        {
            for (int i = 0; i < numInteractableObjs; i++)
            {
                // choose a random object type (as long as it's an interactable object type)
                Transform t;
                do
                {
                    t = objectTypes.GetChild(RandomHelper.RandomInt(0, objectTypes.childCount - 1,
                        (int)(RandomHelper.RangeFlags.MinInclusive | RandomHelper.RangeFlags.MaxInclusive)));
                } while (!interactableObjectTypes.Contains(t));
                Debug.Log(string.Format("Creating new {0}", t.name));

                // find a clear coordinate on the play surface within the interactable radius
                //  (and beyond the near clip pane)
                Vector3 coord;
                do
                {
                    coord = GlobalHelper.FindClearRegion(surface, t.gameObject).center;
                } while (Vector3.Magnitude(coord - floorPosition) > interactableRadius
                    || Vector3.Magnitude(coord - floorPosition) < nearClipRadius);

                GameObject newObj = Instantiate(t.gameObject);
                newObj.name = newObj.name.Replace("(Clone)", "");
                newObj.transform.position = new Vector3(coord.x,
                    coord.y + GlobalHelper.GetObjectWorldSize(newObj.gameObject).extents.y, coord.z);
                newObj.AddComponent<Voxeme>();
                newObj.GetComponent<Voxeme>().targetPosition = newObj.transform.position;

                // add material
                MaterialOptions materials = newObj.GetComponent<MaterialOptions>();
                int materialIndex = RandomHelper.RandomInt(0, materials.materialOptions.Count - 1,
                        (int)(RandomHelper.RangeFlags.MinInclusive | RandomHelper.RangeFlags.MaxInclusive));
                newObj.GetComponent<Renderer>().material = materials.materialOptions[materialIndex];

                newObj.transform.parent = interactableObjects.transform;
                voxemeInit.InitializeVoxemes();
            }
        }

        // place non-interactable objects
        for (int i = 0; i < numTotalObjs-numInteractableObjs; i++)
        {
            // choose a random object type
            Transform t = objectTypes.GetChild(RandomHelper.RandomInt(0, objectTypes.childCount - 1,
                (int)(RandomHelper.RangeFlags.MinInclusive | RandomHelper.RangeFlags.MaxInclusive)));
            Debug.Log(string.Format("Creating new {0}", t.name));

            // find a clear coordinate on the play surface beyond the interactable radius
            Vector3 coord;
            do
            {
                coord = GlobalHelper.FindClearRegion(surface, t.gameObject).center;
            } while (Vector3.Magnitude(coord - floorPosition) < interactableRadius);

            GameObject newObj = Instantiate(t.gameObject);
            newObj.name = newObj.name.Replace("(Clone)", "");
            newObj.transform.position = new Vector3(coord.x,
                coord.y + GlobalHelper.GetObjectWorldSize(newObj.gameObject).extents.y, coord.z);
            newObj.AddComponent<Voxeme>();
            newObj.GetComponent<Voxeme>().targetPosition = newObj.transform.position;

            // add material
            MaterialOptions materials = newObj.GetComponent<MaterialOptions>();
            int materialIndex = RandomHelper.RandomInt(0, materials.materialOptions.Count - 1,
                    (int)(RandomHelper.RangeFlags.MinInclusive | RandomHelper.RangeFlags.MaxInclusive));
            newObj.GetComponent<Renderer>().material = materials.materialOptions[materialIndex];

            newObj.transform.parent = backgroundObjects.transform;
            voxemeInit.InitializeVoxemes();
        }
    }
}
