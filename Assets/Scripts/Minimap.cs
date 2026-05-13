using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Minimal Minimap: shows a UI icon on a RawImage render texture and orients it to the player.
public class Minimap : MonoBehaviour
{
	[Header("References")]
	public Camera minimapCamera;
	public RawImage minimapImage;
	public RectTransform mapViewport;
	public CanvasGroup mapBackdrop;
	public Image mapBackdropImage;

	[Header("Icon")]
	public Sprite playerIconSprite;
	public Vector2 iconSize = new Vector2(24, 24);

	[Header("Player")]
	public Transform player;
	public Transform worldCenterOverride;

	[Header("Map Controls")]
	public float panSpeed = 150f;
	public float zoomSpeed = 120f;
	public float minMapZoom = 6f;
	public float maxMapZoom = 80f;
	public float openMapZoomMultiplier = 6f;
	public float openMapMoveLerp = 70f;
	public float openMapZoomLerp = 80f;
	public float closedMapMoveLerp = 18f;
	public float closedMapZoomLerp = 24f;

	public bool IsMapOpen => _isMapOpen;

	RectTransform _iconContainer;
	RectTransform _playerIconRT;
	RectTransform _mapRect;
	RenderTexture _sourceRenderTexture;
	Texture _sourceImageTexture;
	RenderTexture _runtimeRenderTexture;
	Vector2 _closedAnchoredPosition;
	Vector2 _closedSizeDelta;
	Vector2 _closedAnchorMin;
	Vector2 _closedAnchorMax;
	Vector2 _closedOffsetMin;
	Vector2 _closedOffsetMax;
	Vector2 _closedPivot;
	float _closedZoom;
	float _openZoom;
	float _currentOpenZoom;
	Vector2 _panOffset;
	bool _isMapOpen;
	bool _toggleMapRequested;
	Vector3 _worldCenter;

	void Start()
	{
		if (minimapImage == null || minimapCamera == null)
		{
			enabled = false;
			return;
		}

		if (player == null)
		{
			PlayerController playerController = FindObjectOfType<PlayerController>();
			if (playerController != null)
			{
				player = playerController.transform;
			}
		}

		ResolveWorldCenter();
		_sourceRenderTexture = minimapCamera.targetTexture;
		_sourceImageTexture = minimapImage.texture;

		// use the RawImage as parent for icons
		_iconContainer = minimapImage.rectTransform;
		_mapRect = mapViewport != null ? mapViewport : minimapImage.rectTransform;
		EnsureBackdrop();
		_cacheClosedLayout();
		_cacheZoomValues();
		SetMapOpen(false, true);

		// create UI icon
		if (_playerIconRT == null)
		{
			var go = new GameObject("PlayerIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
			go.transform.SetParent(_iconContainer, false);
			_playerIconRT = go.GetComponent<RectTransform>();
			Image playerIconImage = go.GetComponent<Image>();
			playerIconImage.sprite = playerIconSprite ? playerIconSprite : Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
			playerIconImage.color = Color.white;
			playerIconImage.preserveAspect = true;
			playerIconImage.raycastTarget = false;
		}

		_playerIconRT.anchorMin = _playerIconRT.anchorMax = new Vector2(0.5f, 0.5f);
		_playerIconRT.pivot = new Vector2(0.5f, 0.5f);
		_playerIconRT.sizeDelta = iconSize;
	}

	public void ToggleMap()
	{
		SetMapOpen(!_isMapOpen, false);
	}

	public void RequestToggleMap()
	{
		_toggleMapRequested = true;
	}

	public void SetMapOpen(bool open, bool instant)
	{
		_isMapOpen = open;
		_panOffset = Vector2.zero;
		if (open)
		{
			_currentOpenZoom = _openZoom;
		}

		if (mapBackdrop != null)
		{
			mapBackdrop.alpha = open ? 1f : 0f;
			mapBackdrop.blocksRaycasts = open;
			mapBackdrop.interactable = open;
		}

		if (mapBackdropImage != null)
		{
			mapBackdropImage.gameObject.SetActive(open);
		}

		ApplyLayout(open, instant);
		SyncOpenMapTexture(open);
	}

	void Update()
	{
		Keyboard keyboard = Keyboard.current;
		if (keyboard != null && keyboard.vKey.wasPressedThisFrame)
		{
			_toggleMapRequested = true;
		}

		if (_toggleMapRequested)
		{
			_toggleMapRequested = false;
			SetMapOpen(!_isMapOpen, false);
		}
	}

	void LateUpdate()
	{
		if (player == null || minimapCamera == null || minimapImage == null)
			return;

		UpdateMapCamera();
		UpdateMapInput();

		if (_playerIconRT == null)
		{
			return;
		}

		var vp = minimapCamera.WorldToViewportPoint(player.position);

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

		Vector3 localPos = _iconContainer.InverseTransformPoint(worldPos);
		_playerIconRT.localPosition = new Vector3(localPos.x, localPos.y, _playerIconRT.localPosition.z);

		float playerZ = player.eulerAngles.z;
		float camZ = minimapCamera.transform.eulerAngles.z;
		float iconZ = playerZ - camZ;
		_playerIconRT.localEulerAngles = new Vector3(0f, 0f, iconZ);
	}

	void UpdateMapInput()
	{
		if (!_isMapOpen)
		{
			return;
		}

		Vector2 panInput = Vector2.zero;

		Keyboard keyboard = Keyboard.current;
		if (keyboard != null)
		{
			panInput.x += keyboard.dKey.isPressed ? 1f : 0f;
			panInput.x -= keyboard.aKey.isPressed ? 1f : 0f;
			panInput.y += keyboard.wKey.isPressed ? 1f : 0f;
			panInput.y -= keyboard.sKey.isPressed ? 1f : 0f;

			if (keyboard.upArrowKey.isPressed) panInput.y += 1f;
			if (keyboard.downArrowKey.isPressed) panInput.y -= 1f;
			if (keyboard.rightArrowKey.isPressed) panInput.x += 1f;
			if (keyboard.leftArrowKey.isPressed) panInput.x -= 1f;
		}

		Gamepad gamepad = Gamepad.current;
		if (gamepad != null)
		{
			panInput += gamepad.leftStick.ReadValue();
		}

		if (panInput.sqrMagnitude > 1f)
		{
			panInput.Normalize();
		}

		float zoomRatio = Mathf.Approximately(_openZoom, 0f) ? 1f : Mathf.Max(_currentOpenZoom, minMapZoom) / _openZoom;

		if (panInput.sqrMagnitude > 0.0001f)
		{
			_panOffset += panInput * panSpeed * zoomRatio * Time.unscaledDeltaTime;
		}

		Keyboard zoomKeyboard = Keyboard.current;
		if (zoomKeyboard != null)
		{
			float zoomDirection = 0f;
			if (zoomKeyboard.xKey.isPressed)
			{
				zoomDirection -= 1f;
			}
			if (zoomKeyboard.cKey.isPressed)
			{
				zoomDirection += 1f;
			}

			if (!Mathf.Approximately(zoomDirection, 0f))
			{
				_currentOpenZoom = Mathf.Clamp(_currentOpenZoom - zoomDirection * zoomSpeed * 0.01f * Time.unscaledDeltaTime, minMapZoom, maxMapZoom);
			}
		}
	}

	void UpdateMapCamera()
	{
		if (player == null || minimapCamera == null)
		{
			return;
		}

		Vector3 mapBasePosition = _isMapOpen ? player.position : _worldCenter;
		Vector3 targetPosition = mapBasePosition + new Vector3(_panOffset.x, _panOffset.y, 0f);
		Vector3 cameraPosition = minimapCamera.transform.position;
		float moveLerp = _isMapOpen ? openMapMoveLerp : closedMapMoveLerp;
		float zoomLerp = _isMapOpen ? openMapZoomLerp : closedMapZoomLerp;
		cameraPosition.x = Mathf.Lerp(cameraPosition.x, targetPosition.x, Time.unscaledDeltaTime * moveLerp);
		cameraPosition.y = Mathf.Lerp(cameraPosition.y, targetPosition.y, Time.unscaledDeltaTime * moveLerp);
		minimapCamera.transform.position = cameraPosition;

		float desiredZoom = _isMapOpen ? _currentOpenZoom : _closedZoom;
		if (minimapCamera.orthographic)
		{
			minimapCamera.orthographicSize = Mathf.Lerp(minimapCamera.orthographicSize, desiredZoom, Time.unscaledDeltaTime * zoomLerp);
		}
		else
		{
			minimapCamera.fieldOfView = Mathf.Lerp(minimapCamera.fieldOfView, desiredZoom, Time.unscaledDeltaTime * zoomLerp);
		}

		if (!_isMapOpen)
		{
			_panOffset = Vector2.Lerp(_panOffset, Vector2.zero, Time.unscaledDeltaTime * closedMapMoveLerp);
		}
	}

	void ApplyLayout(bool open, bool instant)
	{
		if (_mapRect == null)
		{
			return;
		}

		if (open)
		{
			_mapRect.anchorMin = Vector2.zero;
			_mapRect.anchorMax = Vector2.one;
			_mapRect.offsetMin = Vector2.zero;
			_mapRect.offsetMax = Vector2.zero;
			_mapRect.pivot = new Vector2(0.5f, 0.5f);
			_mapRect.localScale = Vector3.one;
		}
		else
		{
			_mapRect.anchorMin = _closedAnchorMin;
			_mapRect.anchorMax = _closedAnchorMax;
			_mapRect.offsetMin = _closedOffsetMin;
			_mapRect.offsetMax = _closedOffsetMax;
			_mapRect.pivot = _closedPivot;
			_mapRect.anchoredPosition = _closedAnchoredPosition;
			_mapRect.sizeDelta = _closedSizeDelta;
			_mapRect.localScale = Vector3.one;
		}

		if (instant && minimapCamera != null)
		{
			if (minimapCamera.orthographic)
			{
				minimapCamera.orthographicSize = open ? _currentOpenZoom : _closedZoom;
			}
			else
			{
				minimapCamera.fieldOfView = open ? _currentOpenZoom : _closedZoom;
			}
		}
	}

	void _cacheClosedLayout()
	{
		if (_mapRect == null)
		{
			return;
		}

		_closedAnchoredPosition = _mapRect.anchoredPosition;
		_closedSizeDelta = _mapRect.sizeDelta;
		_closedAnchorMin = _mapRect.anchorMin;
		_closedAnchorMax = _mapRect.anchorMax;
		_closedOffsetMin = _mapRect.offsetMin;
		_closedOffsetMax = _mapRect.offsetMax;
		_closedPivot = _mapRect.pivot;
	}

	void _cacheZoomValues()
	{
		if (minimapCamera == null)
		{
			return;
		}

		_closedZoom = minimapCamera.orthographic ? minimapCamera.orthographicSize : minimapCamera.fieldOfView;
		_openZoom = Mathf.Clamp(_closedZoom * openMapZoomMultiplier, minMapZoom, maxMapZoom);
		_currentOpenZoom = _openZoom;
	}

	void EnsureBackdrop()
	{
		if (mapBackdropImage != null)
		{
			ConfigureBackdropRect(mapBackdropImage.rectTransform);
			mapBackdropImage.color = Color.black;
			mapBackdropImage.raycastTarget = false;
			mapBackdropImage.gameObject.SetActive(false);
			return;
		}

		Transform parent = mapViewport != null ? mapViewport : minimapImage.rectTransform.parent;
		if (parent == null)
		{
			return;
		}

		GameObject backdropObject = new GameObject("MapBackdrop", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
		backdropObject.transform.SetParent(parent, false);
		backdropObject.transform.SetAsFirstSibling();

		mapBackdropImage = backdropObject.GetComponent<Image>();
		mapBackdropImage.color = Color.black;
		mapBackdropImage.raycastTarget = false;
		ConfigureBackdropRect(backdropObject.GetComponent<RectTransform>());
		backdropObject.SetActive(false);
	}

	void ConfigureBackdropRect(RectTransform rectTransform)
	{
		if (rectTransform == null)
		{
			return;
		}

		rectTransform.anchorMin = Vector2.zero;
		rectTransform.anchorMax = Vector2.one;
		rectTransform.offsetMin = Vector2.zero;
		rectTransform.offsetMax = Vector2.zero;
		rectTransform.localScale = Vector3.one;
	}

	void SyncOpenMapTexture(bool open)
	{
		if (minimapCamera == null || minimapImage == null)
		{
			return;
		}

		if (!open)
		{
			if (_runtimeRenderTexture != null)
			{
				minimapCamera.targetTexture = _sourceRenderTexture;
				minimapImage.texture = _sourceImageTexture;
			}

			return;
		}

		RectTransform rectTransform = _mapRect != null ? _mapRect : minimapImage.rectTransform;
		float width = rectTransform.rect.width;
		float height = rectTransform.rect.height;
		if (width <= 0f || height <= 0f)
		{
			width = Screen.width;
			height = Screen.height;
		}

		float aspect = width / height;
		int sourceHeight = _sourceRenderTexture != null ? _sourceRenderTexture.height : (_sourceImageTexture != null ? _sourceImageTexture.height : 2000);
		int targetWidth = Mathf.Max(1, Mathf.RoundToInt(sourceHeight * aspect));
		int targetHeight = Mathf.Max(1, sourceHeight);

		if (_runtimeRenderTexture == null || _runtimeRenderTexture.width != targetWidth || _runtimeRenderTexture.height != targetHeight)
		{
			if (_runtimeRenderTexture != null)
			{
				_runtimeRenderTexture.Release();
				Object.Destroy(_runtimeRenderTexture);
			}

			_runtimeRenderTexture = new RenderTexture(targetWidth, targetHeight, 24)
			{
				name = "RuntimeMinimapTexture"
			};
			_runtimeRenderTexture.Create();
		}

		minimapCamera.targetTexture = _runtimeRenderTexture;
		minimapImage.texture = _runtimeRenderTexture;
	}

	void ResolveWorldCenter()
	{
		if (worldCenterOverride != null)
		{
			_worldCenter = worldCenterOverride.position;
			return;
		}

		AsteroidSpawner spawner = FindObjectOfType<AsteroidSpawner>();
		if (spawner != null)
		{
			_worldCenter = spawner.transform.position;
			return;
		}

		_worldCenter = Vector3.zero;
	}

	void OnDestroy()
	{
		if (_runtimeRenderTexture != null)
		{
			_runtimeRenderTexture.Release();
			Object.Destroy(_runtimeRenderTexture);
			_runtimeRenderTexture = null;
		}
	}
}

