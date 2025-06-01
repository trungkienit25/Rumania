using UnityEngine;

namespace SurvivalEngine
{
    [CreateAssetMenu(fileName = "ScrollItemData", menuName = "SurvivalEngine/ScrollItemData", order = 16)]
    public class ScrollItemData : ItemData // Kế thừa từ ItemData gốc
    {
        [Header("Scroll Clue Details")]
        [Tooltip("ID duy nhất cho mảnh manh mối này, ví dụ: 'F_PIECE', 'CORAIL_PIECE'.")]
        public string pieceID; // Quan trọng để theo dõi

        // Hình ảnh của chữ cái/cuộn giấy riêng lẻ (nếu ReadPanel của bạn có thể hiển thị)
        // public Sprite individualPieceImage; 

        [Tooltip("Tiêu đề cho manh mối này khi hiển thị, ví dụ: 'Ghi Chú: Chữ F'.")]
        public string clueTitleFR;

        [Tooltip("Nội dung suy nghĩ/manh mối của riêng mảnh này (tiếng Pháp).")]
        [TextArea(4, 8)]
        public string clueNarrativeFR;

        public ScrollItemData()
        {
            // Các giá trị mặc định cho ItemData nếu cần
            title = "Cuộn Giấy Manh Mối"; // Tên chung cho các ScrollItemData
            icon = null; // Gán icon chung cho cuộn giấy nếu có
            
            // SỬA LỖI Ở ĐÂY: Dùng 'desc' thay vì 'description'
            desc = "Một mảnh manh mối quan trọng."; // Mô tả chung kế thừa từ CraftData
            
            inventory_max = 1; // Thường mỗi cuộn là duy nhất
            
            // Khởi tạo mảng actions để tránh lỗi NullReference nếu bạn cố gắng truy cập nó sau này mà chưa gán
            // Bạn sẽ gán ActionViewClue vào đây thông qua Inspector của asset ScrollItemData
            actions = new SAction[0]; 
        }
    }
}