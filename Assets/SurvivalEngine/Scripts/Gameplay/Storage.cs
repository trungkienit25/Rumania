// File: Scripts/Gameplay/Storage.cs (PHIÊN BẢN HOÀN CHỈNH)
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SurvivalEngine
{
    [RequireComponent(typeof(Selectable))]
    [RequireComponent(typeof(UniqueID))]
    public class Storage : MonoBehaviour
    {
        [Header("Cài Đặt Rương Cơ Bản")]
        public int storage_size = 10;
        public SData[] starting_items;

        [Header("--- CÀI ĐẶT HỘP PUZZLE (Chỉ bật cho rương giải đố) ---")]
        [Tooltip("BẬT ô này nếu đây là một Hộp Giải Đố. TẮT ô này cho tất cả các rương đồ thông thường khác.")]
        public bool is_puzzle_box = false;

        [Tooltip("Điền các ID của ItemData cuộn giấy theo đúng thứ tự giải đố.")]
        public string[] correct_solution_ids = new string[5];

        [Header("Nội Dung Thông Báo Thành Công")]
        [Tooltip("Tiêu đề của thông báo khi giải đúng.")]
        public string success_title;

        [Tooltip("Nội dung của thông báo khi giải đúng.")]
        [TextArea(5, 10)]
        public string success_narrative;
        
        [Tooltip("ID duy nhất cho puzzle này để game nhớ là đã giải rồi, ví dụ: 'ocean_puzzle_1'.")]
        public string puzzle_completion_id;

        // ---- Các biến nội bộ, không cần thay đổi ----
        private UniqueID unique_id;
        private bool was_panel_open = false; // Biến để theo dõi trạng thái của UI Panel
        private static List<Storage> storage_list = new List<Storage>();

        void Awake()
        {
            storage_list.Add(this);
            unique_id = GetComponent<UniqueID>();
            
            if (is_puzzle_box && string.IsNullOrEmpty(puzzle_completion_id) && unique_id != null)
            {
                puzzle_completion_id = "puzzle_solved_" + unique_id.unique_id;
            }
        }

        private void OnDestroy()
        {
            storage_list.Remove(this);
        }

        private void Start()
        {
            // Logic tạo inventory khi bắt đầu, giữ nguyên
            if (!string.IsNullOrEmpty(unique_id.unique_id))
            {
                if (!InventoryData.Exists(unique_id.unique_id))
                {
                    InventoryData invdata = InventoryData.Get(InventoryType.Storage, unique_id.unique_id, storage_size);
                    // Logic thêm starting_items giữ nguyên...
                }
            }
        }

        void Update()
        {
            if (TheGame.Get().IsPaused())
                return;

            // ---- LOGIC MỚI: THEO DÕI UI PANEL ----
            // Logic này chỉ chạy nếu đây là một Puzzle Box
            if (!is_puzzle_box)
                return;

            // Tìm StoragePanel đang hiển thị inventory của Hộp Puzzle này
            StoragePanel panel = null;
            foreach (StoragePanel p in StoragePanel.GetAll())
            {
                if (p.IsVisible() && p.GetStorageUID() == GetUID())
                {
                    panel = p;
                    break;
                }
            }

            bool is_panel_open = panel != null;

            // Phát hiện thời điểm panel VỪA được đóng lại
            if (was_panel_open && !is_panel_open)
            {
                CheckSolution(); 
            }

            // Cập nhật trạng thái cho frame tiếp theo
            was_panel_open = is_panel_open;
            // ---- HẾT LOGIC MỚI ----
        }

        // Hàm mở rương giữ nguyên, nó sẽ mở StoragePanel có sẵn
        public void OpenStorage(PlayerCharacter player)
        {
            if (!string.IsNullOrEmpty(unique_id.unique_id))
            {
                StoragePanel spanel = StoragePanel.Get(player.PlayerID);
                spanel?.ShowStorage(player, unique_id.unique_id, storage_size);
            }
        }
        
        // ---- HÀM KIỂM TRA PUZZLE (MỚI) ----
        private void CheckSolution()
        {
            InventoryData inventory = InventoryData.Get(InventoryType.Storage, GetUID());
            if (inventory == null) return;
            
            PlayerData pdata = PlayerData.GetSelf();
            if(pdata != null && !string.IsNullOrEmpty(puzzle_completion_id) && pdata.unlocked_ids.ContainsKey(puzzle_completion_id))
            {
                Debug.Log("Puzzle Box '" + GetUID() + "' đã được giải trước đó.");
                return; 
            }

            Debug.Log("Đang kiểm tra puzzle cho Storage ID: " + GetUID());
            
            bool is_correct = IsSolutionCorrect(inventory);

            if (is_correct)
            {
                Debug.Log("GIẢI ĐỐ THÀNH CÔNG!");
                
                if (pdata != null && !string.IsNullOrEmpty(puzzle_completion_id)) 
                {
                    pdata.unlocked_ids[puzzle_completion_id] = 1; // Đánh dấu puzzle đã được giải
                }

                // HIỂN THỊ HỘP THOẠI THÔNG BÁO THÀNH CÔNG (tận dụng ReadPanel có sẵn)
                ReadPanel.Get().ShowPanel(success_title, success_narrative);
            }
            else
            {
                Debug.Log("GIẢI ĐỐ THẤT BẠI! Thứ tự chưa đúng.");
            }
        }

        // Vòng lặp kiểm tra thứ tự và ID
        private bool IsSolutionCorrect(InventoryData inventory)
        {
            if (correct_solution_ids == null || inventory.items.Count < correct_solution_ids.Length) 
                return false; 

            for (int i = 0; i < correct_solution_ids.Length; i++)
            {
                if (!inventory.items.ContainsKey(i) || inventory.items[i].item_id != correct_solution_ids[i])
                    return false; 
            }
            return true;
        }
        // --------------------------------------------------------
        
        public string GetUID() { return unique_id.unique_id; }
        
        public static Storage Get(string uid)
        {
            if (!string.IsNullOrEmpty(uid))
            {
                foreach (Storage storage in storage_list)
                {
                    if (storage.unique_id != null && storage.unique_id.unique_id == uid)
                        return storage;
                }
            }
            return null;
        }

        public static List<Storage> GetAll() { return storage_list; }
    }
}