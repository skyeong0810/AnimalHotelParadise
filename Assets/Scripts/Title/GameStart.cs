using UnityEngine;
using UnityEngine.SceneManagement;

public class GameStart : MonoBehaviour
{
    public void GameStartButton()
    {
        SceneManager.LoadScene("CounterScene");
    }
}