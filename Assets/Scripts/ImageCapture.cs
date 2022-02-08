using System;
using System.IO;
using UnityEngine;
using UnityEditor;

[Serializable]
public class ImageMetadata
{
    public string filename;
    public string objName;
    public int boundsMinX;
    public int boundsMinY;
    public int boundsMaxX;
    public int boundsMaxY;
}

public class ImageCapture : MonoBehaviour
{
    public int resWidth;
    public int resHeight;

    [CustomEditor(typeof(ImageCapture))]
    public class ImageCaptureCustomEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var bold = new GUIStyle();
            bold.fontStyle = FontStyle.Bold;

            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Res Width", GUILayout.MaxWidth(120));
            EditorGUI.BeginChangeCheck();
            int resWidth = System.Convert.ToInt32(GUILayout.TextField(((ImageCapture)target).resWidth.ToString(), GUILayout.MaxWidth(200)));
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(target, "Inspector");
                ((ImageCapture)target).resWidth = resWidth;
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Res Height", GUILayout.MaxWidth(120));
            EditorGUI.BeginChangeCheck();
            int resHeight = System.Convert.ToInt32(GUILayout.TextField(((ImageCapture)target).resHeight.ToString(), GUILayout.MaxWidth(200)));
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(target, "Inspector");
                ((ImageCapture)target).resHeight = resHeight;
            }
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Save RGB Image"))
            {
                ((ImageCapture)target).SaveRGB("RGB.png");
            }
            if (GUILayout.Button("Save Depth Image"))
            {
                ((ImageCapture)target).SaveDepth();
            }
            GUILayout.EndVertical();
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void SaveRGB(string filename)
    {
        if (filename != string.Empty)
        {
            if (!filename.EndsWith(".png"))
            {
                filename = string.Format("{0}.png", filename);
            }
            string dirPath = Path.GetDirectoryName(filename);

            if (!Directory.Exists(dirPath))
            {
                Debug.LogFormat("SaveRGB: creating directory at {0}", dirPath);
                DirectoryInfo dirInfo = Directory.CreateDirectory(dirPath);
            }

            RenderTexture rt = new RenderTexture(resWidth, resHeight, 24);
            Camera.main.targetTexture = rt;
            Texture2D screenShot = new Texture2D(resWidth, resHeight, TextureFormat.RGB24, false);
            Camera.main.Render();
            RenderTexture.active = rt;
            screenShot.ReadPixels(new Rect(0, 0, resWidth, resHeight), 0, 0);
            screenShot.Apply();
            Camera.main.targetTexture = null;
            RenderTexture.active = null;
            Destroy(rt);
            byte[] bytes = screenShot.EncodeToPNG();
            System.IO.File.WriteAllBytes(filename, bytes);
            Debug.Log(string.Format("Screenshot saved to: {0}", filename));
        }
    }

    public void SaveDepth()
    {
        Debug.Log("Doesn't work yet!");
        return;

        // TODO: make it work

        Camera.main.depthTextureMode = DepthTextureMode.Depth;
        Shader depthShader = Shader.Find("Depth/DepthBW");

        GameObject[] perceptibleObjects = GameObject.FindGameObjectsWithTag("Perceptible");

        foreach (GameObject obj in perceptibleObjects)
        {
            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
            foreach (Renderer r in renderers)
            {
                r.material.shader = depthShader;
            }
        }
    }
}
