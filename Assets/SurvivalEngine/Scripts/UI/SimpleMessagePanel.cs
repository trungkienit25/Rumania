using UnityEngine;
using UnityEngine.UI; // Cần cho UI elements

public class SimpleMessagePanel : MonoBehaviour
{
    public GameObject panelRoot; // Kéo GameObject ScrollMessagePanel_UI vào đây
    public Text idText;      // Kéo ID_Text vào đây
    public Text letterText;  // Kéo Letter_Text vào đây
    public Text clueText;    // Kéo Clue_Text vào đây
    public Button closeButton; 

    private static SimpleMessagePanel instance;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject); // Đảm bảo chỉ có 1 instance
            return;
        }

        if (panelRoot == null)
            panelRoot = gameObject; // Nếu không gán, tự lấy GameObject chứa script

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(Hide);
        }
        HideInternal(); // Ẩn khi bắt đầu
    }

    public static SimpleMessagePanel Get()
    {
        return instance;
    }

    public void Show(string scrollID, string letterOrNumber, string clue)
    {
        if (idText != null)
            idText.text = "ID Manh Mối: " + scrollID; // Hoặc bạn có thể bỏ qua nếu không cần hiển thị ID

        if (letterText != null)
            letterText.text = "Chữ/Số: " + letterOrNumber;

        if (clueText != null)
            clueText.text = clue;

        if (panelRoot != null)
            panelRoot.SetActive(true);
    }

    public void Hide()
    {
        HideInternal();
    }

    private void HideInternal()
    {
         if (panelRoot != null)
            panelRoot.SetActive(false);
    }
}