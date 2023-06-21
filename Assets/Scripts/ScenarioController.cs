using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Timers;

using VoxSimPlatform.Core;
using VoxSimPlatform.Global;
using VoxSimPlatform.SpatialReasoning;
using VoxSimPlatform.Vox;

public class ScenarioController : MonoBehaviour
{
    public bool usingRLClient;
    public bool squareFOV;
    public float xMin;
    public float xMax;
    public float nearClipRadius;
    public float interactableRadius;
    public bool centerDestObj;
    public GameObject interactableObjects;
    public GameObject backgroundObjects;
    public GameObject surface;
    public Transform objectTypes;
    public List<Transform> interactableObjectTypes;
    public List<Material> materialTypes;
    public List<PhysicMaterial> physicMaterialTypes;
    public int numTotalObjs;
    public int numInteractableObjs;
    public bool instantiateObjectTypesInOrder;
    public bool attemptUniqueAttributes;
    public bool randomizeStartRotation;
    public bool circumventEventManager;
    public bool saveImages;

    public string imagesDest;

    // editable field: how long do we wait after an event is completed
    //  to assess the "post-event" consequences (e.g., did our structure fall?)
    public int postEventWaitTimerTime;
    public float timeScale;

    public Timer postEventWaitTimer;

    EventManager eventManager;
    VoxemeInit voxemeInit;
    ObjectSelector objSelector;
    RelationTracker relationTracker;

    ImageCapture imageCapture;

    Dictionary<string, List<string>> instantiatedAttributes = new Dictionary<string, List<string>>();

    Camera mainCamera;

    Vector3 floorPosition;

    bool objectsInited;

    bool _savePostEventImage = false;
    public bool savePostEventImage
    {
        get { return _savePostEventImage; }
        set
        {
            if (_savePostEventImage != value)
            {
                OnSavePostEventImageChanged(_savePostEventImage, value);
            }
            _savePostEventImage = value;
        }
    }

    Dictionary<string, string> objectToVoxemePredMap = new Dictionary<string, string>()
    {
        { "Cube","block" },
        { "Sphere","ball" },
        { "Cylinder","cylinder" },
        { "Capsule","capsule" },
        { "SmallCube","block" },
        { "Egg","egg" },
        { "RectPrism","block" },
        { "Cone","cone" },
        { "Pyramid","pyramid" },
        { "Banana","banana" }
    };

    Vector3[] validPlacementRotations = new Vector3[]
{
            Vector3.zero,
            new Vector3(90,0,0),
            new Vector3(0,90,0),
            new Vector3(0,0,90),
            new Vector3(-90,0,0),
            new Vector3(0,-90,0),
            new Vector3(0,0,-90),
            new Vector3(180,0,0),
            new Vector3(0,180,0),
            new Vector3(0,0,180)
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

    public event EventHandler EventCompleted;

    public void OnEventCompleted(object sender, EventArgs e)
    {
        if (EventCompleted != null)
        {
            EventCompleted(this, e);
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

    public event EventHandler AbortAction;

    public void OnAbortAction(object sender, EventArgs e)
    {
        if (AbortAction != null)
        {
            AbortAction(this, e);
        }
    }

    public event EventHandler ForceEndEpisode;

    public void OnForceEndEpisode(object sender, EventArgs e)
    {
        if (ForceEndEpisode != null)
        {
            ForceEndEpisode(this, e);
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
        eventManager.InvalidPositionError += InvalidPosition;

        relationTracker = GameObject.Find("BehaviorController").GetComponent<RelationTracker>();
        
        // create the pose event wait timer (do not start it) and
        //  and timer callback
        postEventWaitTimer = new Timer(postEventWaitTimerTime);
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
        //if (saveImages)
        //{ 
        //    if (savePostEventImage)
        //    {
        //        // save the "after wait" image
        //        imageCapture.SaveRGB(string.Format("{0}/{1}.png",
        //                imagesDest, DateTime.Now.ToString("yyyyMMddHHmmss")));

        //        // reset flag
        //        savePostEventImage = false;
        //    }
        //}

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

    public void SavePostEventImage(GameObject theme, string filenamePrefix = "")
    {
        if (!saveImages)
        {
            return;
        }

        if (!Directory.Exists(imagesDest))
        {
            Debug.LogFormat("SavePostEventImage: creating directory at {0}", imagesDest);
            DirectoryInfo dirInfo = Directory.CreateDirectory(imagesDest);
        }

        DirectoryInfo dir = new DirectoryInfo(imagesDest);

        string filename;

        if (filenamePrefix == string.Empty)
        {
            filename = DateTime.Now.ToString("yyyyMMddHHmmss");
        }
        else
        {
            filename = string.Format("{0}",filenamePrefix);
        }

        string path = string.Format("{0}/{1}.png",
                imagesDest, filename);

        int num = dir.GetFiles("*.png").Where(f => f.Name.StartsWith(filename)).ToList().Count;

        if (num > 0)
        {
            path = string.Format("{0}-{1}", path.Replace(".png",""), num + 1);
        }

        // save the "after wait" image
        imageCapture.SaveRGB(path);

        Bounds bounds = GlobalHelper.GetObjectWorldSize(theme);
        float[] xs = new float[] { mainCamera.WorldToScreenPoint(new Vector3(bounds.min.x,bounds.min.y,bounds.min.z)).x,
                                    mainCamera.WorldToScreenPoint(new Vector3(bounds.min.x,bounds.min.y,bounds.max.z)).x,
                                    mainCamera.WorldToScreenPoint(new Vector3(bounds.min.x,bounds.max.y,bounds.min.z)).x,
                                    mainCamera.WorldToScreenPoint(new Vector3(bounds.min.x,bounds.max.y,bounds.max.z)).x,
                                    mainCamera.WorldToScreenPoint(new Vector3(bounds.max.x,bounds.min.y,bounds.min.z)).x,
                                    mainCamera.WorldToScreenPoint(new Vector3(bounds.max.x,bounds.min.y,bounds.max.z)).x,
                                    mainCamera.WorldToScreenPoint(new Vector3(bounds.max.x,bounds.max.y,bounds.min.z)).x,
                                    mainCamera.WorldToScreenPoint(new Vector3(bounds.max.x,bounds.max.y,bounds.max.z)).x };
        float[] ys = new float[] { mainCamera.WorldToScreenPoint(new Vector3(bounds.min.x,bounds.min.y,bounds.min.z)).y,
                                    mainCamera.WorldToScreenPoint(new Vector3(bounds.min.x,bounds.min.y,bounds.max.z)).y,
                                    mainCamera.WorldToScreenPoint(new Vector3(bounds.min.x,bounds.max.y,bounds.min.z)).y,
                                    mainCamera.WorldToScreenPoint(new Vector3(bounds.min.x,bounds.max.y,bounds.max.z)).y,
                                    mainCamera.WorldToScreenPoint(new Vector3(bounds.max.x,bounds.min.y,bounds.min.z)).y,
                                    mainCamera.WorldToScreenPoint(new Vector3(bounds.max.x,bounds.min.y,bounds.max.z)).y,
                                    mainCamera.WorldToScreenPoint(new Vector3(bounds.max.x,bounds.max.y,bounds.min.z)).y,
                                    mainCamera.WorldToScreenPoint(new Vector3(bounds.max.x,bounds.max.y,bounds.max.z)).y };

        float heightScale = Screen.height / (float)imageCapture.resHeight;

        ImageMetadata metadata = new ImageMetadata();
        metadata.filename = filename;
        metadata.objName = theme.name;
        metadata.boundsMinX = (int)((xs.Min() / heightScale) - (((Screen.width / heightScale) - imageCapture.resWidth) / 2.0f));
        metadata.boundsMinY = (int)(ys.Min() / heightScale);
        metadata.boundsMaxX = (int)((xs.Max() / heightScale) - (((Screen.width / heightScale) - imageCapture.resWidth) / 2.0f));
        metadata.boundsMaxY = (int)(ys.Max() / heightScale);
        metadata.boundsMinX -= 5;
        metadata.boundsMinY -= 5;
        metadata.boundsMaxX += 5;
        metadata.boundsMaxY += 5;

        string json = JsonUtility.ToJson(metadata);

        using (StreamWriter writer = new StreamWriter(string.Format("{0}/{1}.json", imagesDest, dir.Name), true))
        {
            writer.WriteLine(json);
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
                Transform t;

                if (instantiateObjectTypesInOrder)
                {
                    t = interactableObjectTypes[i % interactableObjectTypes.Count];
                    Debug.Log(string.Format("ScenarioController.PlaceRandomly: Creating new {0}", t.name));
                }
                else
                {
                    // choose a random object type (as long as it's an interactable object type)
                    do
                    {
                        t = objectTypes.GetChild(RandomHelper.RandomInt(0, objectTypes.childCount - 1,
                            (int)(RandomHelper.RangeFlags.MinInclusive | RandomHelper.RangeFlags.MaxInclusive)));
                    } while (!interactableObjectTypes.Contains(t));
                    Debug.Log(string.Format("ScenarioController.PlaceRandomly: Creating new {0}", t.name));
                }

                // find a clear coordinate on the play surface within the interactable radius
                //  (and beyond the near clip pane)
                Vector3 coord;
                do
                {
                    coord = GlobalHelper.FindClearRegion(surface, t.gameObject).center;
                } while (Vector3.Magnitude(coord - floorPosition) > interactableRadius
                    || Vector3.Magnitude(coord - floorPosition) < nearClipRadius
                    || !PointIsInCameraView(coord, mainCamera));

                //coord = new Vector3(i / 2f, 0, 0);

                GameObject newObj = Instantiate(t.gameObject);
                Debug.Log(string.Format("ScenarioController.PlaceRandomly: {0}, parent = {1}", newObj.name, newObj.transform.parent));
                newObj.name = newObj.name.Replace("(Clone)", string.Format("{0}",i));
                newObj.transform.position = new Vector3(coord.x,
                    coord.y + GlobalHelper.GetObjectWorldSize(newObj.gameObject).extents.y, coord.z);

                if (randomizeStartRotation)
                {
                    newObj.transform.eulerAngles = validPlacementRotations[RandomHelper.RandomInt(0, validPlacementRotations.Length)];
                }

                newObj.AddComponent<Voxeme>();
                newObj.GetComponent<Voxeme>().predicate = objectToVoxemePredMap[t.name];
                //newObj.GetComponent<Voxeme>().targetPosition = newObj.transform.position;

                // add material
                int materialIndex = 0;
                MaterialOptions materials = newObj.GetComponent<MaterialOptions>();

                if (materials != null)
                {
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
                }

                // tag
                newObj.tag = "Perceptible";

                newObj.transform.parent = interactableObjects.transform;
                Debug.Log(string.Format("ScenarioController.PlaceRandomly: {0}, parent = {1}", newObj.name, newObj.transform.parent));
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
            Debug.Log(string.Format("ScenarioController.PlaceRandomly: Creating new {0}", t.name));

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

            if (randomizeStartRotation)
            {
                newObj.transform.eulerAngles = validPlacementRotations[RandomHelper.RandomInt(0, validPlacementRotations.Length)];
            }

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

    public void PlaceMaterialBlock()
    {
        Transform t = interactableObjectTypes[0];
        GameObject newBlock = Instantiate(t.gameObject);
        newBlock.transform.position = new Vector3(0, 0.05f, 0);
        newBlock.transform.parent = interactableObjects.transform;
        newBlock.AddComponent<Voxeme>();
        newBlock.GetComponent<Voxeme>().predicate = objectToVoxemePredMap[t.name];
        int materialTypeIndex = UnityEngine.Random.Range(0, materialTypes.Count);
        newBlock.GetComponent<Renderer>().material = materialTypes[materialTypeIndex];
        newBlock.GetComponent<Collider>().material = physicMaterialTypes[materialTypeIndex];
        newBlock.name = materialTypes[materialTypeIndex].name + " Block";

        voxemeInit.InitializeVoxemes();
        objectsInited = true;
        OnObjectsInited(this, null);
    }

    public void CenterObjectInView(GameObject obj)
    {
        mainCamera.transform.LookAt(new Vector3(obj.transform.position.x,
            GlobalHelper.GetObjectWorldSize(obj).max.y,
            obj.transform.position.z));
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

    public List<string> GetRelations(GameObject obj1, GameObject obj2)
    {
        List<string> relations = new List<string>();

        List<GameObject> pair = new List<GameObject>(new GameObject[] { obj1, obj2 });

        foreach (DictionaryEntry dictEntry in relationTracker.relations)
        {
            if (((List<GameObject>)dictEntry.Key).SequenceEqual(pair))
            {
                string[] rels = (relationTracker.relations[dictEntry.Key] as string).Split(',');
                relations.AddRange(rels);
            }
        }

        Debug.LogFormat("ScenarioController.GetRelations: Got relations [{0}] between {1} and {2}",
            string.Join(", ", relations.ToArray()), obj1.name, obj2.name);

        return relations;
    }

    void ExecutingEvent(object sender, EventArgs e)
    {
        Debug.LogFormat("ScenarioController.ExecutingEvent: {0}", ((EventManagerArgs)e).EventString);

        OnEventExecuting(this, null);
    }

    void CompletedEvent(object sender, EventArgs e)
    {
        OnEventCompleted(this, null);

        Debug.LogFormat("ScenarioController.CompletedEvent: {0}", ((EventManagerArgs)e).EventString);

        // start the wait timer
        postEventWaitTimer.Enabled = true;
    }

    void PostEventWaitComplete(object sender, ElapsedEventArgs e)
    {
        // stop and reset the wait timer
        postEventWaitTimer.Interval = postEventWaitTimerTime;
        postEventWaitTimer.Enabled = false;

        OnPostEventWaitCompleted(this, null);

        // set flag
        if (objectsInited)
        {
            savePostEventImage = true;
        }
    }

    void InvalidPosition(object sender, EventArgs e)
    {
        Debug.LogWarningFormat("ScenarioController.InvalidPositionError: {0} {1} {2} {3}",
            ((CalculatedPositionArgs)e).Formula,
            GlobalHelper.VectorToParsable(((CalculatedPositionArgs)e).Position),
            GlobalHelper.VectorToParsable(((CalculatedPositionArgs)e).Direction),
            ((CalculatedPositionArgs)e).Distance);
        OnAbortAction(this, null);  // rather inelegant solution
    }

    bool PointIsInCameraView(Vector3 point, Camera cam)
    {
        Vector3 viewportPoint = cam.WorldToViewportPoint(point);

        xMin = 0.0f;
        xMax = 1.0f;

        if (squareFOV)
        {
            xMin = ((Screen.width / 2.0f) - (Screen.height / 4.0f)) / Screen.width;
            xMax = 1.0f - xMin;
        }

        if ((viewportPoint.x > xMin) && (viewportPoint.x < xMax) &&
            (viewportPoint.y > 0.0f) && (viewportPoint.y < 1.0f) &&
            (viewportPoint.z > 0.0f))
        {
            Debug.LogFormat("{0} {1} {2} {3}", GlobalHelper.VectorToParsable(point), xMin, viewportPoint.x, xMax);
            return true;
        }
        else
        {
            return false;
        }
    }

    public bool IsValidAction(Vector2 action)
    {
        return !action.Equals(new Vector2(-Mathf.Infinity, -Mathf.Infinity));
    }

    /// <summary>
    /// Triggered when the savePostEventImage flag changes
    /// </summary>
    // IN: oldVal -- previous value of savePostEventImage
    //      newVal -- new or current value of savePostEventImage
    protected void OnSavePostEventImageChanged(bool oldVal, bool newVal)
    {
        Debug.Log(string.Format("==================== savePostEventImage flag changed ==================== {0}->{1}", oldVal, newVal));
    }
}
