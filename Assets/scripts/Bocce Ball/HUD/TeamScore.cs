using UnityEngine;
using System.Collections;

public class TeamScore : MonoBehaviour
{
    public void SetColor(Color color)
    {
        GetComponent<GUIText>().color = color;
    }

    public void SetScore(int score)
    {
        GetComponent<GUIText>().text = score.ToString();
    }
}
