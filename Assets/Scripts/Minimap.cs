using UnityEngine;
using UnityEngine.UI;

// Minimal Minimap: shows a UI icon on a RawImage render texture and orients it to the player.
public class Minimap : MonoBehaviour
{
	[Header("References")]
	public Camera minimapCamera;
	public RawImage minimapImage;

	[Header("Icon")]
	public Sprite playerIconSprite;
	public Vector2 iconSize = new Vector2(24, 24);

	[Header("Player")]
	public Transform player;

	RectTransform _iconContainer;
	RectTransform _playerIconRT;
	Image _playerIconImage;

	void Start()
	{
		if (minimapImage == null || minimapCamera == null)
		{
			enabled = false;
			return;
		}

		// use the RawImage as parent for icons
		_iconContainer = minimapImage.rectTransform;

		// create UI icon
		if (_playerIconRT == null)
		{
			var go = new GameObject("PlayerIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
			go.transform.SetParent(_iconContainer, false);
			_playerIconRT = go.GetComponent<RectTransform>();
			_playerIconImage = go.GetComponent<Image>();
			_playerIconImage.sprite = playerIconSprite ? playerIconSprite : Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
			_playerIconImage.color = Color.white;
			_playerIconImage.preserveAspect = true;
			_playerIconImage.raycastTarget = false;
		}

		_playerIconRT.anchorMin = _playerIconRT.anchorMax = new Vector2(0.5f, 0.5f);
		_playerIconRT.pivot = new Vector2(0.5f, 0.5f);
		_playerIconRT.sizeDelta = iconSize;
	}

	void LateUpdate()
	{
		if (player == null || minimapCamera == null || minimapImage == null || _playerIconRT == null)
			return;

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
}

