using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class TitleManager : MonoBehaviour
{
	[SerializeField] private Button _startButton;
	[SerializeField] private Button _endButton;
	[SerializeField] private GameObject _startPoint;
	[SerializeField] private GameObject _endPoint;
	[SerializeField] private AudioClip _decideClip;

	private bool _isPointerOverStart;
	private bool _isPointerOverEnd;

	private void Awake()
	{
		_startPoint = ResolvePointObject(_startPoint, _startButton);
		_endPoint = ResolvePointObject(_endPoint, _endButton);
		SetPointActive(_startPoint, false);
		SetPointActive(_endPoint, false);
	}

	private void Update()
	{
		var pointer = Pointer.current;
		if (pointer == null)
		{
			UpdateHoverState(false, false);
			return;
		}

		Vector2 pointerPosition = pointer.position.ReadValue();
		bool isPointerOverStart = IsPointerOverButton(_startButton, pointerPosition);
		bool isPointerOverEnd = IsPointerOverButton(_endButton, pointerPosition);
		UpdateHoverState(isPointerOverStart, isPointerOverEnd);

		if (!pointer.press.wasPressedThisFrame)
		{
			return;
		}

		if (_isPointerOverStart)
		{
			StartGame();
		}
		else if (_isPointerOverEnd)
		{
			ExitGame();
		}
	}

	private void StartGame()
	{
		// ゲーム開始の処理をここに追加
		Debug.Log("ゲーム開始");
		ScrollAction.DecideSoundPlayer.Play(_decideClip);
		// Shop で装備を整えてから ScrollAction 本編へ進ませるため、開始導線は Shop に統一する
		UnityEngine.SceneManagement.SceneManager.LoadScene("Shop");
	}

	private void ExitGame()
	{
		// ゲーム終了の処理をここに追加
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

		RectTransform targetRect = button.targetGraphic != null
			? button.targetGraphic.rectTransform
			: button.transform as RectTransform;

		return targetRect != null && RectTransformUtility.RectangleContainsScreenPoint(targetRect, pointerPosition, null);
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

	private void UpdateHoverState(bool isPointerOverStart, bool isPointerOverEnd)
	{
		_isPointerOverStart = isPointerOverStart;
		_isPointerOverEnd = isPointerOverEnd;
		SetPointActive(_startPoint, _isPointerOverStart);
		SetPointActive(_endPoint, _isPointerOverEnd);
	}
}
