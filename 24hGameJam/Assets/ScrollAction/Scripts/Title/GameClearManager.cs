using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameClearManager : MonoBehaviour
{
    [SerializeField] private Button _startButton;
    [SerializeField] private Button _titleButton;
    [SerializeField] private Button _endButton;
    [SerializeField] private GameObject _startPoint;
    [SerializeField] private GameObject _titlePoint;
    [SerializeField] private GameObject _endPoint;
    [SerializeField] private AudioClip _decideClip;

    private bool _isPointerOverStart;
    private bool _isPointerOverTitle;
    private bool _isPointerOverEnd;

    private void Awake()
    {
        _startPoint = ResolvePointObject(_startPoint, _startButton);
        _titlePoint = ResolvePointObject(_titlePoint, _titleButton);
        _endPoint = ResolvePointObject(_endPoint, _endButton);
        SetPointActive(_startPoint, false);
        SetPointActive(_titlePoint, false);
        SetPointActive(_endPoint, false);
    }

    private void Update()
    {
        var pointer = Pointer.current;
        if (pointer == null)
        {
            UpdateHoverState(false, false, false);
            return;
        }

        Vector2 pointerPosition = pointer.position.ReadValue();
        bool isPointerOverStart = IsPointerOverButton(_startButton, pointerPosition);
        bool isPointerOverTitle = IsPointerOverButton(_titleButton, pointerPosition);
        bool isPointerOverEnd = IsPointerOverButton(_endButton, pointerPosition);
        UpdateHoverState(isPointerOverStart, isPointerOverTitle, isPointerOverEnd);

        if (!pointer.press.wasPressedThisFrame)
        {
            return;
        }

        if (_isPointerOverStart)
        {
            StartGame();
        }
        else if (_isPointerOverTitle)
        {
            ReturnToTitle();
        }
        else if (_isPointerOverEnd)
        {
            ExitGame();
        }
    }

    private void StartGame()
    {
        Debug.Log("ゲーム開始");
        ScrollAction.DecideSoundPlayer.Play(_decideClip);
        // ゲームオーバーのリトライ・クリア後の再挑戦どちらも Shop を経由してから本編に戻す導線に統一
        SceneManager.LoadScene("Shop");
    }

    private void ReturnToTitle()
    {
        Debug.Log("タイトルに戻る");
        ScrollAction.DecideSoundPlayer.Play(_decideClip);
        SceneManager.LoadScene("Title");
    }

    private void ExitGame()
    {
        Debug.Log("ゲーム終了");
        ScrollAction.DecideSoundPlayer.Play(_decideClip);
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private static bool IsPointerOverButton(Button button, Vector2 pointerPosition)
    {
        if (button == null || !button.isActiveAndEnabled || !button.interactable)
        {
            return false;
        }

        var buttonRect = button.transform as RectTransform;
        return buttonRect != null && RectTransformUtility.RectangleContainsScreenPoint(buttonRect, pointerPosition, null);
    }

    private static GameObject ResolvePointObject(GameObject pointObject, Button button)
    {
        if (pointObject != null || button == null || button.transform.childCount == 0)
        {
            return pointObject;
        }

        return button.transform.GetChild(0).gameObject;
    }

    private static void SetPointActive(GameObject pointObject, bool isActive)
    {
        if (pointObject != null && pointObject.activeSelf != isActive)
        {
            pointObject.SetActive(isActive);
        }
    }

    private void UpdateHoverState(bool isPointerOverStart, bool isPointerOverTitle, bool isPointerOverEnd)
    {
        _isPointerOverStart = isPointerOverStart;
        _isPointerOverTitle = isPointerOverTitle;
        _isPointerOverEnd = isPointerOverEnd;
        SetPointActive(_startPoint, _isPointerOverStart);
        SetPointActive(_titlePoint, _isPointerOverTitle);
        SetPointActive(_endPoint, _isPointerOverEnd);
    }
}
