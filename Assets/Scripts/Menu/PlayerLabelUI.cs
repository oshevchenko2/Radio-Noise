using TMPro;
using UnityEngine;

public class PlayerLabelUI : MonoBehaviour
{
    public TMP_Text nameText;

    public void SetName(string playerName, bool isReady = false)
    {
        nameText.text = playerName + (isReady ? " (Ready)" : " (Not Ready)");
    }

    public void MarkReady()
    {
        if (nameText != null)
        {
            nameText.text += " (Ready)";
        }
    }
}
