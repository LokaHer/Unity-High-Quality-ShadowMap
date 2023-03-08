using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

[RequireComponent(typeof(Light))]
public class HighQualityHeroShadows : MonoBehaviour
{
    public int HighQualityHeroLayer = 9;
        private Bounds bounds = new Bounds();
            private List<Vector3> boundsVertexList = new List<Vector3>();
    private Camera lightCam = null;
        public float CameraSize = 1;
        private Shader captureDepthShader = null;
        private RenderTexture depthRT = null;
            private int rtWidth = 1024;
            private int rtHeight = 1024;
        private static CommandBuffer shadowCamCmd = null;
                      
    private Light directionalLight = null;
        private Matrix4x4 m_LightVP;   



    void Awake()
    {
        captureDepthShader = Shader.Find("HighQualityHeroShadows/capture depth");

        SkinnedMeshRenderer[] skinnedMeshRenderers = Resources.FindObjectsOfTypeAll(typeof(SkinnedMeshRenderer)) as SkinnedMeshRenderer[];
        foreach(var renderer in skinnedMeshRenderers )
        {
            if(renderer.gameObject.activeInHierarchy && renderer.gameObject.layer == HighQualityHeroLayer &&  renderer != null){
                bounds.Encapsulate(renderer.bounds);
            }
        }    
        float x = bounds.extents.x;
        float y = bounds.extents.y;
        float z = bounds.extents.z;
        boundsVertexList.Add(new Vector3(x, y, z)+ bounds.center);
        boundsVertexList.Add(new Vector3(x, -y, z)+ bounds.center);
        boundsVertexList.Add(new Vector3(x, y, -z)+ bounds.center);
        boundsVertexList.Add(new Vector3(x, -y, -z)+ bounds.center);
        boundsVertexList.Add(new Vector3(-x, y, z)+ bounds.center);
        boundsVertexList.Add(new Vector3(-x, -y, z)+ bounds.center);
        boundsVertexList.Add(new Vector3(-x, y, -z)+ bounds.center);
        boundsVertexList.Add(new Vector3(-x, -y, -z)+ bounds.center);
        
        directionalLight = GetComponent<Light>();
        depthRT = InitRT();
        Shader.SetGlobalTexture("_ShadowDepthTex", depthRT);
        lightCam = InitLightCam(gameObject, depthRT);

        
    }

    RenderTexture InitRT()
    {
        RenderTexture rt = new RenderTexture(rtWidth, rtHeight, 24, RenderTextureFormat.Depth);
        return rt;
    }
    Camera InitLightCam(GameObject parentObj, RenderTexture rt)
    {
        GameObject lightCamObj = new GameObject("DepthCamera");
        Camera lightCam = lightCamObj.AddComponent<Camera>();
        lightCamObj.transform.SetParent(parentObj.transform, false);

        if (null == lightCam) 
            return null;
        lightCam.orthographic = true;
        lightCam.backgroundColor = Color.black;
        lightCam.clearFlags = CameraClearFlags.Color;
        lightCam.targetTexture = rt;
        lightCam.cullingMask = 1<<HighQualityHeroLayer;

        if(shadowCamCmd == null){
            shadowCamCmd = new CommandBuffer();
            shadowCamCmd.name = "SetViewPort";
        }
        lightCam.AddCommandBuffer(CameraEvent.BeforeForwardOpaque, shadowCamCmd);
        shadowCamCmd.SetViewport(new Rect(1, 1, rtWidth - 2, rtHeight - 2));

        return lightCam;
    }

    void Update()
    {
        Shader.EnableKeyword("_HIGH_QUALITY_SHADOW_REVEIVE");

        SkinnedMeshRenderer[] skinnedMeshRenderers = Resources.FindObjectsOfTypeAll(typeof(SkinnedMeshRenderer)) as SkinnedMeshRenderer[];
        
        bounds.size = Vector3.zero;        
        foreach(var renderer in skinnedMeshRenderers )
        {
            if(renderer.gameObject.activeInHierarchy && renderer.gameObject.layer == HighQualityHeroLayer &&  renderer != null){
                bounds.Encapsulate(renderer.bounds);
            }
        }    
        float x = bounds.extents.x;                               
        float y = bounds.extents.y;
        float z = bounds.extents.z;
        boundsVertexList[0] = (new Vector3(x, y, z)+ bounds.center);
        boundsVertexList[1] = (new Vector3(x, -y, z)+ bounds.center);
        boundsVertexList[2] = (new Vector3(x, y, -z)+ bounds.center);
        boundsVertexList[3] = (new Vector3(x, -y, -z)+ bounds.center);
        boundsVertexList[4] = (new Vector3(-x, y, z)+ bounds.center);
        boundsVertexList[5] = (new Vector3(-x, -y, z)+ bounds.center);
        boundsVertexList[6] = (new Vector3(-x, y, -z)+ bounds.center);
        boundsVertexList[7] = (new Vector3(-x, -y, -z)+ bounds.center);

        Graphics.SetRenderTarget(depthRT);

        GL.Clear(true, true, Color.white);

        UpdateLightCam(lightCam, directionalLight, bounds);

        UpdateShaderVP();

        lightCam.RenderWithShader(captureDepthShader, "RenderType");
    }
    void UpdateLightCam(Camera lightCam, Light light, Bounds bounds)
    {
        Vector3 pos = new Vector3();
        Vector3 lightDir = light.transform.forward;
        Vector3 maxDistance = new Vector3(bounds.extents.x, bounds.extents.y, bounds.extents.z);
        float length = maxDistance.magnitude;
        pos = bounds.center - lightDir * length;
        lightCam.transform.position = pos;

        
        Vector2 xMinMax = new Vector2(float.MinValue, float.MaxValue);
        Vector2 yMinMax = new Vector2(float.MinValue, float.MaxValue);
        Vector2 zMinMax = new Vector2(float.MinValue, float.MaxValue);
        
        Matrix4x4 world2LightMatrix = lightCam.transform.worldToLocalMatrix;
        for (int i = 0; i < boundsVertexList.Count; i++)
        {
            Vector4 pointLS = world2LightMatrix * boundsVertexList[i];

            if (pointLS.x > xMinMax.x)
                xMinMax.x = pointLS.x;
            if (pointLS.x < xMinMax.y)
                xMinMax.y = pointLS.x;

            if (pointLS.y > yMinMax.x)
                yMinMax.x = pointLS.y;
            if (pointLS.y < yMinMax.y)
                yMinMax.y = pointLS.y;

            if (pointLS.z > zMinMax.x)
                zMinMax.x = pointLS.z;
            if (pointLS.z < zMinMax.y)
                zMinMax.y = pointLS.z;
        }

        lightCam.nearClipPlane = 0;
        lightCam.farClipPlane = zMinMax.x - zMinMax.y;
        lightCam.orthographicSize = CameraSize *(yMinMax.x - yMinMax.y) / 2;//宽高中的高度
        lightCam.aspect = (xMinMax.x - xMinMax.y) / (yMinMax.x - yMinMax.y);
    } 

    void UpdateShaderVP()
    {
        Matrix4x4 world2View = lightCam.worldToCameraMatrix;
        Matrix4x4 projection = GL.GetGPUProjectionMatrix(lightCam.projectionMatrix, false);
        m_LightVP = projection * world2View;

        Shader.SetGlobalMatrix("_LightVP", m_LightVP);
    }

    void OnDisable() 
    {
        Shader.DisableKeyword("_HIGH_QUALITY_SHADOW_REVEIVE");
    }
	private void OnDestroy()
	{
		if (shadowCamCmd != null)
		{
			shadowCamCmd.Dispose();
			shadowCamCmd = null;
		}
	}
}
