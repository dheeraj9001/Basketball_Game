using UnityEngine;

public sealed class HoopScoreTrigger : MonoBehaviour
{
    BasketballGameController game;
    public void Initialize(BasketballGameController controller) { game = controller; }
    void OnTriggerEnter(Collider other)
    {
        if (other.transform == game.basketball) game.ScoreBall();
    }
}
