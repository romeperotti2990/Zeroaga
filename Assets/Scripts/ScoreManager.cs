using UnityEngine;
using UnityEngine.UI;

public class ScoreManager : MonoBehaviour
{
    static ScoreManager _instance;

    public static ScoreManager Instance
    {
        get
        {
            if (_instance != null)
            {
                return _instance;
            }

            _instance = FindObjectOfType<ScoreManager>();
            if (_instance != null)
            {
                return _instance;
            }

            GameObject created = new GameObject("ScoreManager");
            _instance = created.AddComponent<ScoreManager>();
            return _instance;
        }
    }

    public int CurrentPoints { get; private set; }
    public int CollectedPickups { get; private set; }

    [Header("UI")]
    public Text scoreText;
    public bool createTextIfMissing = true;
    public Vector2 textOffset = new Vector2(18f, -18f);
    public int fontSize = 32;

    Canvas _canvas;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        ResolveUI();
        RefreshUI();
    }

    void OnEnable()
    {
        ResolveUI();
        RefreshUI();
    }

    public void AddPoints(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        CurrentPoints += amount;
        CollectedPickups += 1;
        RefreshUI();
    }

    void ResolveUI()
    {
        if (_canvas == null)
        {
            _canvas = GetComponentInParent<Canvas>();
            if (_canvas == null)
            {
                _canvas = FindObjectOfType<Canvas>();
            }
        }

        if (scoreText == null)
        {
            scoreText = GetComponentInChildren<Text>(true);
        }

        if (scoreText == null && createTextIfMissing && _canvas != null)
        {
            scoreText = CreateTextLabel(_canvas.transform);
        }

        if (scoreText != null)
        {
            RectTransform rectTransform = scoreText.rectTransform;
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(0f, 1f);
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.anchoredPosition = textOffset;
            rectTransform.sizeDelta = new Vector2(500f, 80f);
            scoreText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            scoreText.fontSize = fontSize;
            scoreText.alignment = TextAnchor.UpperLeft;
            scoreText.color = Color.white;
            scoreText.raycastTarget = false;
            scoreText.horizontalOverflow = HorizontalWrapMode.Overflow;
            scoreText.verticalOverflow = VerticalWrapMode.Overflow;
            scoreText.enabled = true;
        }
    }

    Text CreateTextLabel(Transform parent)
    {
        GameObject textObject = new GameObject("ScoreText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObject.transform.SetParent(parent, false);

        Text label = textObject.GetComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = fontSize;
        label.alignment = TextAnchor.UpperLeft;
        label.color = Color.white;
        label.raycastTarget = false;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        return label;
    }

    void RefreshUI()
    {
        ResolveUI();

        if (scoreText != null)
        {
            scoreText.text = $"Score: {CurrentPoints}\nPickups: {CollectedPickups}";
        }
    }
}
