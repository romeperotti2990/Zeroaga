using UnityEngine;
using UnityEngine.UI;

// Attach to the Minimap camera (the camera rendering to the RenderTexture)
public class Minimap : MonoBehaviour
{
	[Header("References")]
	public Camera minimapCamera; // camera rendering the minimap (orthographic)
	public RawImage minimapImage; // RawImage showing the render texture
	public RectTransform iconContainer; // UI container (RectTransform) that overlays the RawImage

	[Header("Player Icon")]
	public GameObject playerIconPrefab; // optional: a UI prefab (with Image + RectTransform)
	public Sprite playerIconSprite; // fallback: sprite to create an Image if no prefab provided
	public Vector2 iconSize = new Vector2(24, 24);
	public Color iconColor = Color.white; // fallback color for the generated icon
	public bool useSpriteNativeSize = true;
	public bool debugIcon = false;

	[Header("Player")]
	public Transform player; // world-space player transform to track

	[Header("Rotation")]
	public bool rotateWithPlayer = true;
	public bool invertRotation = false;
	public float rotationOffset = 0f;

	RectTransform _playerIconRT;
	Image _playerIconImage;

	void Start()
	{
		if (iconContainer == null)
		{
			if (minimapImage != null && minimapImage.rectTransform != null)
			{
				var go = new GameObject("MinimapIconContainer", typeof(RectTransform));
				go.transform.SetParent(minimapImage.rectTransform, false);
				var rt = go.GetComponent<RectTransform>();
				rt.anchorMin = minimapImage.rectTransform.anchorMin;
				rt.anchorMax = minimapImage.rectTransform.anchorMax;
				rt.anchoredPosition = minimapImage.rectTransform.anchoredPosition;
				rt.sizeDelta = minimapImage.rectTransform.sizeDelta;
				iconContainer = rt;
				// ensure icons render on top of the minimap RawImage
				iconContainer.SetAsLastSibling();
			}
			else
			{
				Debug.LogError("Minimap: iconContainer and minimapImage are not assigned.");
				enabled = false;
				return;
			}
		}

		if (playerIconPrefab != null)
		{
			var go = Instantiate(playerIconPrefab, iconContainer);
			_playerIconRT = go.GetComponent<RectTransform>();
			_playerIconImage = go.GetComponent<Image>();
			if (_playerIconRT == null || _playerIconImage == null)
			{
				Debug.LogWarning("Minimap: provided playerIconPrefab does not contain UI Image/RectTransform. Falling back to generated UI icon.");
				// if prefab instance exists, destroy it
				if (go != null) Destroy(go);
				_playerIconRT = null;
				_playerIconImage = null;
			}
		}
		else
		{
			var go = new GameObject("PlayerIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
			go.transform.SetParent(iconContainer, false);
			_playerIconRT = go.GetComponent<RectTransform>();
			_playerIconImage = go.GetComponent<Image>();
			_playerIconImage.sprite = playerIconSprite;
			if (_playerIconImage.sprite == null)
			{
				var builtin = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
				if (builtin != null) _playerIconImage.sprite = builtin;
			}
			if (_playerIconImage.sprite != null)
			{
				_playerIconImage.color = Color.white; // preserve sprite original colors
				_playerIconImage.preserveAspect = true;
			}
			else
			{
				_playerIconImage.color = iconColor; // colored fallback square
			}
			_playerIconImage.raycastTarget = false;
		}

		// If prefab was invalid or components missing, create a UI fallback icon now
		if (_playerIconRT == null || _playerIconImage == null)
		{
			var go2 = new GameObject("PlayerIcon_Fallback", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
			go2.transform.SetParent(iconContainer, false);
			_playerIconRT = go2.GetComponent<RectTransform>();
			_playerIconImage = go2.GetComponent<Image>();
			_playerIconImage.sprite = playerIconSprite;
			if (_playerIconImage.sprite == null)
			{
				var builtin = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
				if (builtin != null) _playerIconImage.sprite = builtin;
			}
			if (_playerIconImage.sprite != null)
			{
				_playerIconImage.color = Color.white;
				_playerIconImage.preserveAspect = true;
			}
			else
			{
				_playerIconImage.color = iconColor;
			}
			_playerIconImage.raycastTarget = false;
		}

		// Ensure icon is active and visible
		if (_playerIconImage != null)
		{
			_playerIconImage.enabled = true;
			var col = _playerIconImage.color; col.a = 1f; _playerIconImage.color = col;
		}

		Debug.Log($"Minimap.Start: player={(player!=null?player.name:"null")}, minimapCamera={(minimapCamera!=null?minimapCamera.name:"null")}, minimapImage={(minimapImage!=null?minimapImage.name:"null")}, iconContainer={(iconContainer!=null?iconContainer.name:"null")}, iconCreated={_playerIconRT!=null}");

		if (_playerIconRT != null)
		{
			_playerIconRT.anchorMin = _playerIconRT.anchorMax = new Vector2(0.5f, 0.5f);
			_playerIconRT.pivot = new Vector2(0.5f, 0.5f);

			if (_playerIconImage != null && _playerIconImage.sprite != null && useSpriteNativeSize)
			{
				_playerIconImage.SetNativeSize();
				// Clamp native size to iconSize maximum
				var current = _playerIconRT.sizeDelta;
				float scale = Mathf.Min(1f, Mathf.Min(iconSize.x / current.x, iconSize.y / current.y));
				_playerIconRT.sizeDelta = current * scale;
			}
			else
			{
				_playerIconRT.sizeDelta = iconSize;
			}
		}
	}

	void LateUpdate()
	{
		if (player == null || minimapCamera == null || minimapImage == null || _playerIconRT == null)
			return;

		// Use camera projection to get viewport coords (handles camera rotation)
		Vector3 vp = minimapCamera.WorldToViewportPoint(player.position);

		// Get the world-space corners of the RawImage so we can map viewport -> world UI position
		RectTransform rt = minimapImage.rectTransform;
		Vector3[] corners = new Vector3[4];
		rt.GetWorldCorners(corners); // 0=bl,1=tl,2=tr,3=br

		Vector3 bl = corners[0];
		Vector3 tr = corners[2];
		Vector3 worldPos = new Vector3(
			Mathf.Lerp(bl.x, tr.x, vp.x),
			Mathf.Lerp(bl.y, tr.y, vp.y),
			bl.z
		);

		// Convert worldPos into local space of the iconContainer and place the icon there
		Vector3 localPos = iconContainer.InverseTransformPoint(worldPos);

		// Apply local position (z preserved)
		_playerIconRT.localPosition = new Vector3(localPos.x, localPos.y, _playerIconRT.localPosition.z);

		if (debugIcon)
		{
			Debug.Log($"Minimap icon vp={vp} worldPos={worldPos} localPos={localPos}");
		}

		// Rotation: align icon with player rotation (accounts for minimap camera z-rotation)
		if (rotateWithPlayer)
		{
			float playerZ = player.eulerAngles.z;
			float camZ = minimapCamera != null ? minimapCamera.transform.eulerAngles.z : 0f;
			float iconZ = playerZ - camZ + rotationOffset;
			if (invertRotation) iconZ = -iconZ;
			_playerIconRT.localEulerAngles = new Vector3(0f, 0f, iconZ);
		}
	}
}

