using UnityEngine;
using UnityEngine.UI;

/// <summary>Connects the existing Canvas controls to the basketball game.</summary>
public sealed class BasketballCanvasUI : MonoBehaviour
{
    public GameObject topHUD;
    public Button grabButton;
    public Button shootButton;
    public Text scoreText;
    public Text statusText;
    public Image powerFill;

    BasketballGameController game;

    public void Initialize(BasketballGameController controller)
    {
        game = controller;
        topHUD.SetActive(true);
        grabButton.onClick.AddListener(game.GrabOrAim);
    }

    public void Refresh(int score, int attempts, string status, bool charging, float charge)
    {
        scoreText.text = $"SCORE  {score}     SHOTS  {attempts}";
        statusText.text = status;
        powerFill.fillAmount = charging ? Mathf.Clamp01(charge) : 0f;
    }

    public void BeginShotCharge() => game.BeginCharge();
    public void ReleaseShotCharge() => game.ReleaseShot();
}
