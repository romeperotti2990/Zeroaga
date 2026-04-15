using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Camera))]
public class Minimap : MonoBehaviour
{
    public Camera minimapCamera;
    public RawImage minimapDisplay;
    public int minimapTextureSize = 512;

    private RenderTexture minimapRenderTexture;

    void Awake()
    {
        if (minimapCamera == null)
        {
            minimapCamera = GetComponent<Camera>();
        }

        if (minimapCamera == null)
        {
            Debug.LogError("Minimap requires a Camera reference.");
            enabled = false;
            return;
        }

        minimapRenderTexture = new RenderTexture(minimapTextureSize, minimapTextureSize, 16, RenderTextureFormat.ARGB32);
        minimapRenderTexture.Create();

        minimapCamera.targetTexture = minimapRenderTexture;
        minimapCamera.enabled = true;

        if (minimapDisplay != null)
        {
            minimapDisplay.texture = minimapRenderTexture;
        }
    }

    void OnDestroy()
    {
        if (minimapCamera != null && minimapCamera.targetTexture == minimapRenderTexture)
        {
            minimapCamera.targetTexture = null;
        }

        if (minimapRenderTexture != null)
        {
            minimapRenderTexture.Release();
            Destroy(minimapRenderTexture);
        }
    }
}
