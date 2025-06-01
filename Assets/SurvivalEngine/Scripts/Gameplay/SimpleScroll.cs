using UnityEngine;

public class SimpleScroll : MonoBehaviour
{
    [Header("Thông tin cuộn giấy")]
    [Tooltip("ID của cuộn giấy này, có thể chứa số. Dùng để nhận diện.")]
    public string scrollID;

    [Tooltip("Chữ cái, số, hoặc từ khóa ngắn gọn hiển thị.")]
    public string displayLetterOrNumber;

    [Tooltip("Nội dung manh mối chi tiết bằng tiếng Pháp.")]
    [TextArea(5, 10)]
    public string clueMessageFR;

    private bool hasBeenGrabbed = false; // Để đảm bảo chỉ kích hoạt 1 lần nếu cần

    // Phương thức này sẽ được gọi khi GameObject có Collider và bị click chuột
    // Hoặc bạn có thể gọi nó từ một hệ thống "grab" khác nếu có
    void OnMouseDown() 
    {
        HandleGrab();
    }

    // Bạn có thể gọi hàm này từ một script quản lý "grab" khác
    public void HandleGrab()
    {
        if (hasBeenGrabbed) // Nếu bạn chỉ muốn nó kích hoạt 1 lần
        {
            // Tùy chọn: hiển thị lại nếu đã grab rồi
            Debug.Log("Cuộn giấy '" + scrollID + "' đã được xem trước đó. Hiển thị lại thông tin.");
            // return; 
        }

        Debug.Log("Đã grab cuộn giấy ID: " + scrollID + ", Chữ: " + displayLetterOrNumber);
        hasBeenGrabbed = true;

        // Gọi SimpleMessagePanel để hiển thị thông tin
        SimpleMessagePanel messagePanel = SimpleMessagePanel.Get();
        if (messagePanel != null)
        {
            messagePanel.Show(scrollID, displayLetterOrNumber, clueMessageFR);
        }
        else
        {
            Debug.LogError("SimpleMessagePanel instance không tìm thấy trong scene!");
        }

        // Tùy chọn: Hủy GameObject cuộn giấy sau khi nhặt và đọc
        // Destroy(gameObject, 0.5f); // Hủy sau 0.5 giây để người chơi kịp thấy hiệu ứng (nếu có)
    }
}