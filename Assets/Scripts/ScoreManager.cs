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

            GameObject existing = GameObject.Find("ScoreManager");
            if (existing != null)
            {
                _instance = existing.GetComponent<ScoreManager>();
                if (_instance != null)
                {
                    return _instance;
                }
            }

            GameObject created = new GameObject("ScoreManager");
            _instance = created.AddComponent<ScoreManager>();
            DontDestroyOnLoad(created);
            return _instance;
        }
    }

    public int CurrentPoints { get; private set; }

    Canvas _canvas;
    Text _scoreText;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureUI();
        RefreshUI();
    }

    public void AddPoints(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        CurrentPoints += amount;
        RefreshUI();
    }

    void EnsureUI()
    {
        if (_scoreText != null)
        {
            return;
        }

        _canvas = FindObjectOfType<Canvas>();
        if (_canvas == null)
        {
            GameObject canvasObject = new GameObject("ScoreCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvas = canvasObject.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            DontDestroyOnLoad(canvasObject);
        }

        GameObject textObject = new GameObject("ScoreText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObject.transform.SetParent(_canvas.transform, false);

        RectTransform rectTransform = textObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(0f, 1f);
        rectTransform.pivot = new Vector2(0f, 1f);
        rectTransform.anchoredPosition = new Vector2(18f, -18f);
        rectTransform.sizeDelta = new Vector2(360f, 60f);

        _scoreText = textObject.GetComponent<Text>();
        _scoreText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        _scoreText.fontSize = 32;
        _scoreText.alignment = TextAnchor.UpperLeft;
        _scoreText.color = Color.white;
        _scoreText.raycastTarget = false;
    }

    void RefreshUI()
    {
        EnsureUI();
        if (_scoreText != null)
        {
            _scoreText.text = $"Points: {CurrentPoints}";
        }
    }
}