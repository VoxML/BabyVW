using UnityEngine;
using System.Collections;
using System.Collections.Generic;

using VoxSimPlatform.Vox;
using VoxSimPlatform.Global;

public class ScenarioController : MonoBehaviour
{
    public float interactableRadius;
    public GameObject interactableObjects;
    public GameObject surface;
    public Transform objectTypes;

    Camera mainCamera;

    Vector3 floorPosition;

    bool objectsInited;

    // Start is called before the first frame update
    void Start()
    {
        mainCamera = Camera.main;

        floorPosition = new Vector3(mainCamera.transform.position.x, 0.0f, mainCamera.transform.position.z);

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
        // place focus objects
        foreach (Transform t in objectTypes)
        {
            Debug.Log(t);
            Vector3 coord = GlobalHelper.FindClearRegion(surface, t.gameObject).center;
            GameObject newObj = Instantiate(t.gameObject);
            Debug.Log(newObj);
            newObj.transform.position = new Vector3(coord.x,
                coord.y + GlobalHelper.GetObjectWorldSize(newObj.gameObject).extents.y, coord.z);
            //newObj.GetComponent<Voxeme>().targetPosition = newObj.transform.position;

            newObj.transform.parent = interactableObjects.transform;
        }
    }
}
