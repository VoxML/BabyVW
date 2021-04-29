using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;

using VoxSimPlatform.Core;
using VoxSimPlatform.Global;
using VoxSimPlatform.SpatialReasoning;
using VoxSimPlatform.Vox;

public class ScenarioController : MonoBehaviour
{
    public bool usingRLClient;
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

    // editable field: how long do we wait after an event is completed
    //  to assess the "post-event" consequences (e.g., did our structure fall?)
    public int postEventWaitTimerTime;
    public float timeScale;

    Timer postEventWaitTimer;

    EventManager eventManager;
    VoxemeInit voxemeInit;
    ObjectSelector objSelector;
    RelationTracker relationTracker;

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

    public event EventHandler ObjectsInited;

    public void OnObjectsInited(object sender, EventArgs e)
    {
        if (ObjectsInited != null)
        {
            ObjectsInited(this, e);
        }
    }

    public event EventHandler EventExecuting;

    public void OnEventExecuting(object sender, EventArgs e)
    {
        if (EventExecuting != null)
        {
            EventExecuting(this, e);
        }
    }

    public event EventHandler PostEventWaitCompleted;

    public void OnPostEventWaitCompleted(object sender, EventArgs e)
    {
        if (PostEventWaitCompleted != null)
        {
            PostEventWaitCompleted(this, e);
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        // get the event manager component and add event handler callbacks when the
        //  for when the event starts executing and when it finishes
        eventManager = GameObject.Find("BehaviorController").GetComponent<EventManager>();
        eventManager.ExecuteEvent += ExecutingEvent;
        eventManager.QueueEmpty += CompletedEvent;

        relationTracker = GameObject.Find("BehaviorController").GetComponent<RelationTracker>();
        
        // create the pose event wait timer (do not start it) and
        //  and timer callback
        postEventWaitTimer = new Timer(postEventWaitTimerTime/timeScale);
        postEventWaitTimer.Enabled = false;
        postEventWaitTimer.Elapsed += PostEventWaitComplete;

        // get the image capture component to save images
        imageCapture = GameObject.Find("ImageCapture").GetComponent<ImageCapture>();

        mainCamera = Camera.main;

        floorPosition = new Vector3(mainCamera.transform.position.x, 0.0f, mainCamera.transform.position.z);

        foreach (string objType in objectToVoxemePredMap.Keys)
        {
            instantiatedAttributes[objType] = new List<string>();
        }

        objSelector = GameObject.Find("VoxWorld").GetComponent<ObjectSelector>();
        voxemeInit = GameObject.Find("VoxWorld").GetComponent<VoxemeInit>();
        objectsInited = false;
    }

    // Update is called once per frame
    void Update()
    {
        if (savePostEventImage)
        {
            // save the "after wait" image
            imageCapture.SaveRGB("RGB3.png");   // TODO: create unique filename

            // reset flag
            savePostEventImage = false;
        }

        if (!usingRLClient)
        {
            if (!objectsInited)
            { 
                if (surface != null)
                {
                    PlaceRandomly(surface);
                }
            }
        }
    }

    public void PlaceRandomly(GameObject surface)
    {
        List<GameObject> instantiatedVoxemeObjs = interactableObjects.GetComponentsInChildren<Voxeme>().Select(v => v.gameObject).ToList();
        for (int i = 0; i < instantiatedVoxemeObjs.Count; i++)
        {
            objSelector.allVoxemes.Remove(instantiatedVoxemeObjs[i].GetComponent<Voxeme>());
            instantiatedAttributes[instantiatedVoxemeObjs[i].transform.name.Split(new char[]{ '0','1','2','3','4','5','6','7','8','9' })[0]].Clear();
            Destroy(interactableObjects.GetComponentsInChildren<Voxeme>().Select(v => v.gameObject).ToList()[i]);
            
        }

        relationTracker.relations.Clear();

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

                //coord = new Vector3(i / 2f, 0, 0);

                GameObject newObj = Instantiate(t.gameObject);
                newObj.name = newObj.name.Replace("(Clone)", string.Format("{0}",i));
                newObj.transform.position = new Vector3(coord.x,
                    coord.y + GlobalHelper.GetObjectWorldSize(newObj.gameObject).extents.y, coord.z);
                newObj.AddComponent<Voxeme>();
                newObj.GetComponent<Voxeme>().predicate = objectToVoxemePredMap[t.name];
                //newObj.GetComponent<Voxeme>().targetPosition = newObj.transform.position;

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

            objectsInited = true;
            OnObjectsInited(this, null);
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

    public void ClearEventManager()
    {
        eventManager.ClearEvents();
    }


    public void SendToEventManager(string eventStr)
    {
        eventManager.InsertEvent("", 0);
        eventManager.InsertEvent(eventStr, 1);
    }

    void ExecutingEvent(object sender, EventArgs e)
    {
        Debug.LogFormat("ExecutingEvent: {0}", ((EventManagerArgs)e).EventString);
        if (GlobalHelper.GetTopPredicate(((EventManagerArgs)e).EventString) == "put")
        {
            // save the "before event" image
            imageCapture.SaveRGB("RGB1.png");   // TODO: create unique filename
        }

        OnEventExecuting(this, null);
    }

    void CompletedEvent(object sender, EventArgs e)
    {
        Debug.LogFormat("CompletedEvent: {0}", ((EventManagerArgs)e).EventString);

        // start the wait timer
        postEventWaitTimer.Enabled = true;

        // save the "after event" image
        imageCapture.SaveRGB("RGB2.png");   // TODO: create unique filename
    }

    void PostEventWaitComplete(object sender, ElapsedEventArgs e)
    {
        // stop and reset the wait timer
        postEventWaitTimer.Interval = postEventWaitTimerTime;
        postEventWaitTimer.Enabled = false;

        // set flag
        savePostEventImage = true;

        OnPostEventWaitCompleted(this, null);
    }

    public bool IsValidAction(Vector2 action)
    {
        return !action.Equals(new Vector2(-Mathf.Infinity, -Mathf.Infinity));
    }
}
