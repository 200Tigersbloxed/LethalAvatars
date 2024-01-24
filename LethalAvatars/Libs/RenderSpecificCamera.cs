using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LethalAvatars.Libs;

/// <summary>
/// Simple script that excludes a Renderer from a specific Camera
/// </summary>
public class RenderSpecificCamera : MonoBehaviour
{
    public List<Camera?> RenderCameras = new();
    public Renderer? TargetRenderer;
    public Action<Camera, Renderer>? OnHide = (camera, renderer) => { };
    public Action<Camera, Renderer>? OnShow = (camera, renderer) => { };

    private void BeginRendering(ScriptableRenderContext arg1, Camera arg2)
    {
        if(TargetRenderer == null) return;
        TargetRenderer.enabled = RenderCameras.Contains(arg2);
        OnHide?.Invoke(arg2, TargetRenderer);
    }
    
    private void EndRendering(ScriptableRenderContext arg1, Camera arg2)
    {
        if(TargetRenderer == null) return;
        TargetRenderer.enabled = false;
        OnShow?.Invoke(arg2, TargetRenderer);
    }
    
    private void Start()
    {
        RenderPipelineManager.beginCameraRendering += BeginRendering;
        RenderPipelineManager.endCameraRendering += EndRendering;
    }

    private void OnDestroy()
    {
        RenderPipelineManager.beginCameraRendering -= BeginRendering;
        RenderPipelineManager.endCameraRendering -= EndRendering;
    }
}