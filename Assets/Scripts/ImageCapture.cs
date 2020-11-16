using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class ImageCapture : MonoBehaviour
{
    [CustomEditor(typeof(ImageCapture))]
    public class ImageCaptureCustomEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var bold = new GUIStyle();
            bold.fontStyle = FontStyle.Bold;

            GUILayout.BeginVertical();
            if (GUILayout.Button("Save RGB Image"))
            {
                ((ImageCapture)target).SaveRGB("RGB.png");
            }
            if (GUILayout.Button("Save Depth Image"))
            {
                ((ImageCapture)target).SaveDepth();
            }
            GUILayout.EndVertical();

            //TODO: make resWidth/resHeight editable fields
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
        // TODO: automate generated and saving of images using REST API or similar
        int resWidth = 3840;
        int resHeight = 2160;
        
        RenderTexture rt = new RenderTexture(resWidth, resHeight, 24);
        Camera.main.targetTexture = rt;
        Texture2D screenShot = new Texture2D(resWidth, resHeight, TextureFormat.RGB24, false);
        Camera.main.Render();
        RenderTexture.active = rt;
        screenShot.ReadPixels(new Rect(0, 0, resWidth, resHeight), 0, 0);
        Camera.main.targetTexture = null;
        RenderTexture.active = null; // JC: added to avoid errors
        Destroy(rt);
        byte[] bytes = screenShot.EncodeToPNG();
        System.IO.File.WriteAllBytes(filename, bytes);
        Debug.Log(string.Format("Screenshot saved to: {0}", filename));
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
