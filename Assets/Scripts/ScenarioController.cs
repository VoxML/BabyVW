﻿using UnityEngine;
using System;
using System.Collections.Generic;
using System.Timers;

using VoxSimPlatform.Core;
using VoxSimPlatform.Global;
using VoxSimPlatform.Vox;

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
    public bool attemptUniqueAttributes;
    public int postEventWaitTimerTime;

    Timer postEventWaitTimer;

    EventManager eventManager;
    VoxemeInit voxemeInit;

    ImageCapture imageCapture;

    Dictionary<string, List<string>> instantiatedAttributes = new Dictionary<string, List<string>>();

    Camera mainCamera;

    Vector3 floorPosition;

    bool objectsInited;

    bool savePostEventImage = false;

    Dictionary<string, string> objectToVoxemePredMap = new Dictionary<string, string>()
    {
        { "Cube","block" },
        { "Sphere","ball" },
        { "Cylinder","cylinder" }
    };

    // Start is called before the first frame update
    void Start()
    {
        eventManager = GameObject.Find("BehaviorController").GetComponent<EventManager>();
        eventManager.ExecuteEvent += ExecutingEvent;
        eventManager.QueueEmpty += CompletedEvent;

        postEventWaitTimer = new Timer(postEventWaitTimerTime);
        postEventWaitTimer.Enabled = false;
        postEventWaitTimer.Elapsed += PostEventWaitComplete;

        imageCapture = GameObject.Find("ImageCapture").GetComponent<ImageCapture>();

        mainCamera = Camera.main;

        floorPosition = new Vector3(mainCamera.transform.position.x, 0.0f, mainCamera.transform.position.z);

        foreach (string objType in objectToVoxemePredMap.Keys)
        {
            instantiatedAttributes[objType] = new List<string>();
        }

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

        if (savePostEventImage)
        {
            imageCapture.SaveRGB("RGB3.png");
            savePostEventImage = false;
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
                newObj.name = newObj.name.Replace("(Clone)", string.Format("{0}",i));
                newObj.transform.position = new Vector3(coord.x,
                    coord.y + GlobalHelper.GetObjectWorldSize(newObj.gameObject).extents.y, coord.z);
                newObj.AddComponent<Voxeme>();
                newObj.GetComponent<Voxeme>().predicate = objectToVoxemePredMap[t.name];
                newObj.GetComponent<Voxeme>().targetPosition = newObj.transform.position;

                // add material
                int materialIndex = 0;
                MaterialOptions materials = newObj.GetComponent<MaterialOptions>();

                if (attemptUniqueAttributes)
                {
                    for (int m = 0; m < materials.materialOptions.Count; m++)
                    {
                        if (!instantiatedAttributes[t.name].Contains(materials.materialOptions[m].name.ToLower())) 
                        {
                            materialIndex = m;
                            break;
                        }
                    }

                    if (materialIndex == materials.materialOptions.Count)
                    {
                        materialIndex = RandomHelper.RandomInt(0, materials.materialOptions.Count - 1,
                            (int)(RandomHelper.RangeFlags.MinInclusive | RandomHelper.RangeFlags.MaxInclusive));
                    }

                    newObj.GetComponent<Renderer>().material = materials.materialOptions[materialIndex];
                }
                else
                {
                    materialIndex = RandomHelper.RandomInt(0, materials.materialOptions.Count - 1,
                            (int)(RandomHelper.RangeFlags.MinInclusive | RandomHelper.RangeFlags.MaxInclusive));
                    newObj.GetComponent<Renderer>().material = materials.materialOptions[materialIndex];
                }

                // add material name to instantiated attributes
                if (!instantiatedAttributes[t.name].Contains(materials.materialOptions[materialIndex].name.ToLower()))
                {
                    instantiatedAttributes[t.name].Add(materials.materialOptions[materialIndex].name.ToLower());
                }

                // add material name as attribute
                newObj.AddComponent<AttributeSet>();
                newObj.GetComponent<AttributeSet>().attributes.Add(materials.materialOptions[materialIndex].name.ToLower());

                // tag
                newObj.tag = "Perceptible";

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
            newObj.name = newObj.name.Replace("(Clone)", string.Format("{0}", i+numInteractableObjs));
            newObj.transform.position = new Vector3(coord.x,
                coord.y + GlobalHelper.GetObjectWorldSize(newObj.gameObject).extents.y, coord.z);
            newObj.AddComponent<Voxeme>();
            newObj.GetComponent<Voxeme>().predicate = objectToVoxemePredMap[t.name];
            newObj.GetComponent<Voxeme>().targetPosition = newObj.transform.position;

            // add material
            int materialIndex = 0;
            MaterialOptions materials = newObj.GetComponent<MaterialOptions>();

            if (attemptUniqueAttributes)
            {
                for (int m = 0; m < materials.materialOptions.Count; m++)
                {
                    if (!instantiatedAttributes[t.name].Contains(materials.materialOptions[m].name.ToLower()))
                    {
                        materialIndex = m;
                        break;
                    }
                }

                if (materialIndex == materials.materialOptions.Count)
                {
                    materialIndex = RandomHelper.RandomInt(0, materials.materialOptions.Count - 1,
                        (int)(RandomHelper.RangeFlags.MinInclusive | RandomHelper.RangeFlags.MaxInclusive));
                }

                newObj.GetComponent<Renderer>().material = materials.materialOptions[materialIndex];
            }
            else
            {
                materialIndex = RandomHelper.RandomInt(0, materials.materialOptions.Count - 1,
                        (int)(RandomHelper.RangeFlags.MinInclusive | RandomHelper.RangeFlags.MaxInclusive));
                newObj.GetComponent<Renderer>().material = materials.materialOptions[materialIndex];
            }

            // add material name to instantiated attributes
            if (!instantiatedAttributes[t.name].Contains(materials.materialOptions[materialIndex].name.ToLower()))
            {
                instantiatedAttributes[t.name].Add(materials.materialOptions[materialIndex].name.ToLower());
            }

            // add material name as attribute
            newObj.AddComponent<AttributeSet>();
            newObj.GetComponent<AttributeSet>().attributes.Add(materials.materialOptions[materialIndex].name.ToLower());

            // tag
            newObj.tag = "Perceptible";

            newObj.transform.parent = backgroundObjects.transform;
            voxemeInit.InitializeVoxemes();
        }
    }

    void ExecutingEvent(object sender, EventArgs e)
    {
        Debug.LogFormat("ExecutingEvent: {0}", ((EventManagerArgs)e).EventString);
        if (GlobalHelper.GetTopPredicate(((EventManagerArgs)e).EventString) == "put")
        {
            imageCapture.SaveRGB("RGB1.png");
        }
    }

    void CompletedEvent(object sender, EventArgs e)
    {
        Debug.LogFormat("CompletedEvent: {0}", ((EventManagerArgs)e).EventString);
        postEventWaitTimer.Enabled = true;
        imageCapture.SaveRGB("RGB2.png");
    }

    void PostEventWaitComplete(object sender, ElapsedEventArgs e)
    {
        Debug.LogFormat("PostEventWaitComplete");
        postEventWaitTimer.Interval = postEventWaitTimerTime;
        postEventWaitTimer.Enabled = false;
        savePostEventImage = true;
    }
}
