// File: Scripts/Actions/ActionRead.cs
using UnityEngine;
using System.Collections.Generic; // Cần cho List<string>

namespace SurvivalEngine
{
    // Quan trọng: 
    // 1. Nếu bạn đã tạo một Action Asset từ script ActionRead.cs cũ (ví dụ, tên asset là "ActionReadClues.asset"),
    //    thì menuName ở đây phải khớp với menuName đã dùng để tạo asset đó.
    // 2. Hoặc, bạn có thể đổi menuName này thành một tên mới (ví dụ "ReadClueAction") rồi tạo một Action Asset MỚI
    //    từ menu "SurvivalEngine/Actions/ReadClueAction", sau đó gán asset MỚI này vào các ItemData manh mối.
    [CreateAssetMenu(fileName = "ActionReadClues", menuName = "SurvivalEngine/Actions/ReadClues", order = 52)] 
    public class ActionRead : SAction 
    {
        [Header("CÀI ĐẶT THEO DÕI MANH MỐI (Cho Asset ActionRead này)")]
        [Tooltip("QUAN TRỌNG: Kéo GroupData Asset (ví dụ: 'clue_scroll_group') mà bạn đã tạo và gán cho các ItemData manh mối vào đây.")]
        public GroupData clueItemGroupForTracking; 

        [Tooltip("QUAN TRỌNG: Điền các ID của ItemData manh mối CẦN ĐỌC để hoàn thành (ví dụ: manhmoi_f, manhmoi_i,...). Phải khớp với ID trong ItemData của từng manh mối.")]
        public List<string> requiredClueItemIDsForCompletion = new List<string>();

        [Header("NỘI DUNG MANH MỐI CUỐI CÙNG (Hiển thị bằng ActionRead này)")]
        [Tooltip("Tiêu đề cho manh mối tổng hợp khi tất cả các mảnh đã được đọc.")]
        public string finalClueTitleFR = "Manh Mối Đã Hoàn Tất";

        [Tooltip("Nội dung cho manh mối tổng hợp khi tất cả các mảnh đã được đọc.")]
        [TextArea(5, 10)]
        public string finalClueNarrativeFR = "Tất cả các mảnh ghép đã được tìm thấy! Đại dương đang gặp nguy hiểm bởi vì...";

        // Một key duy nhất để đánh dấu bộ manh mối này đã được giải (để không hiện lại manh mối cuối)
        [Tooltip("ID duy nhất cho bộ puzzle này, ví dụ 'ocean_mystery_puzzle'. Nếu để trống, manh mối cuối có thể hiện lại mỗi khi đọc mảnh thứ 5.")]
        public string puzzleCompletionID = "ocean_mystery_puzzle";


        // --- PHƯƠNG THỨC CHO INVENTORY ITEM (Khi "Đọc Manh Mối" từ túi đồ) ---
        public override void DoAction(PlayerCharacter character, InventorySlot slot)
        {
            ItemData item = slot.GetItem(); // Lấy ItemData từ slot
            if (item != null && character.IsSelf()) // IsSelf() để UI chỉ hiện cho người chơi local
            {
                // 1. Hiển thị thông tin của ItemData hiện tại bằng ReadPanel (sử dụng Title và Desc của ItemData)
                // ReadPanel.Get(0) nếu panel_id của ReadPanel bạn muốn dùng là 0
                ReadPanel.Get().ShowPanel(item.title, item.desc);

                // 2. Logic theo dõi manh mối
                if (this.clueItemGroupForTracking != null && item.HasGroup(this.clueItemGroupForTracking))
                {
                    PlayerData playerData = PlayerData.Get(character.PlayerID); 
                    if (playerData != null)
                    {
                        string saveDataKey = "read_clue_" + item.id; // Ví dụ: "read_clue_manhmoi_f"
                        
                        bool alreadyMarkedAsRead = playerData.unlocked_ids.ContainsKey(saveDataKey) && playerData.unlocked_ids[saveDataKey] == 1;

                        if (!alreadyMarkedAsRead) 
                        {
                            playerData.unlocked_ids[saveDataKey] = 1; 
                            Debug.Log("Manh mối được đánh dấu đã đọc: " + item.id + " (Key: " + saveDataKey + ")");
                            
                            // Đồng bộ PlayerData nếu là game online và có cơ chế.
                            // Nếu là single player, thay đổi sẽ được lưu khi game save.
                            // PlayerCharacterAttribute pca = character.GetComponent<PlayerCharacterAttribute>();
                            // if (pca != null && TheNetwork.Get() != null && TheNetwork.Get().IsOnline()) 
                            // {
                            //     pca.RefreshPlayer(); 
                            // }
                        } 
                        
                        CheckAllCluesCollected(playerData, character); 
                    }
                }
            }
        }

        // --- PHƯƠNG THỨC CHO SELECTABLE (Khi tương tác với đối tượng trong thế giới) ---
        // Thường thì người chơi sẽ nhặt item (ActionTake) rồi mới đọc từ inventory.
        // Nhưng nếu bạn muốn có action "Đọc" trực tiếp từ item trên mặt đất, logic sẽ tương tự.
        public override void DoAction(PlayerCharacter character, Selectable select)
        {
            // Logic gốc cho ReadObject component (nếu có)
            ReadObject readComp = select.GetComponent<ReadObject>(); 
            if (readComp != null && character.IsSelf()) 
            {
                ReadPanel.Get().ShowPanel(readComp.title, readComp.text);
                return; 
            }

            // Logic cho Item component (nếu Selectable là một Item có thể đọc được)
            Item itemComp = select.GetComponent<Item>(); 
            if (itemComp != null && itemComp.data != null && character.IsSelf()) 
            {
                ItemData item = itemComp.data;

                // Nếu bạn muốn nhân vật di chuyển tới trước khi đọc:
                // (Yêu cầu PlayerCharacter.cs có hàm MoveToInteract)
                /*
                float interactionRange = select.use_range + character.interact_range;
                character.MoveToInteract(select.transform.position, interactionRange, () => {
                    if (select.IsInUseRange(character)) { // Kiểm tra lại khi đã đến nơi
                        ProcessClueItemReading(character, item);
                    }
                });
                */
                // Hoặc nếu không cần di chuyển, chỉ đọc nếu đã trong tầm:
                if (select.IsInUseRange(character))
                {
                    ProcessClueItemReading(character, item);
                }
                else
                {
                    // Có thể hiện thông báo "Cần đến gần hơn"
                }
            }
        }
        
        // Hàm helper để xử lý logic đọc item manh mối (tránh lặp code)
        private void ProcessClueItemReading(PlayerCharacter character, ItemData item)
        {
            ReadPanel.Get().ShowPanel(item.title, item.desc);

            if (this.clueItemGroupForTracking != null && item.HasGroup(this.clueItemGroupForTracking))
            {
                PlayerData playerData = PlayerData.Get(character.PlayerID);
                if (playerData != null)
                {
                    string saveDataKey = "read_clue_" + item.id; 
                    bool alreadyMarkedAsRead = playerData.unlocked_ids.ContainsKey(saveDataKey) && playerData.unlocked_ids[saveDataKey] == 1;
                    if (!alreadyMarkedAsRead)
                    {
                        playerData.unlocked_ids[saveDataKey] = 1; 
                        Debug.Log("Manh mối (từ world/inventory) được đánh dấu đã đọc: " + item.id);
                        // PlayerCharacterAttribute pca = character.GetComponent<PlayerCharacterAttribute>();
                        // if (pca != null && TheNetwork.Get() != null && TheNetwork.Get().IsOnline()) pca.RefreshPlayer();
                    }
                    CheckAllCluesCollected(playerData, character);
                }
            }
        }
        
        // ---- HÀM KIỂM TRA MANH MỐI ----
        private void CheckAllCluesCollected(PlayerData playerData, PlayerCharacter character)
        {
            if (playerData == null || this.requiredClueItemIDsForCompletion == null || this.requiredClueItemIDsForCompletion.Count == 0) 
            {
                return;
            }

            // Kiểm tra xem manh mối cuối đã được hiển thị cho bộ này chưa
            string finalClueShownKey = "final_clue_shown_" + this.puzzleCompletionID; // Sử dụng puzzleCompletionID
            if (!string.IsNullOrEmpty(this.puzzleCompletionID) && playerData.unlocked_ids.ContainsKey(finalClueShownKey) && playerData.unlocked_ids[finalClueShownKey] == 1)
            {
                Debug.Log("Manh mối tổng hợp cho puzzle '" + this.puzzleCompletionID + "' đã được hiển thị trước đó.");
                return; 
            }

            int collectedCount = 0;
            foreach (string scrollIdKeyInRequiredList in this.requiredClueItemIDsForCompletion)
            {
                if (string.IsNullOrEmpty(scrollIdKeyInRequiredList)) continue; 

                string saveDataKeyToCheck = "read_clue_" + scrollIdKeyInRequiredList;
                if (playerData.unlocked_ids.ContainsKey(saveDataKeyToCheck) 
                    && playerData.unlocked_ids[saveDataKeyToCheck] == 1)
                {
                    collectedCount++;
                }
            }
            
            Debug.Log("Đã đọc " + collectedCount + "/" + this.requiredClueItemIDsForCompletion.Count + " manh mối yêu cầu cho puzzle: " + this.puzzleCompletionID);

            if (collectedCount >= this.requiredClueItemIDsForCompletion.Count)
            {
                Debug.Log("Tất cả manh mối đã được đọc! Hiển thị manh mối tổng hợp.");
                ReadPanel.Get().ShowPanel(this.finalClueTitleFR, this.finalClueNarrativeFR);
                
                if (!string.IsNullOrEmpty(this.puzzleCompletionID))
                {
                    playerData.unlocked_ids[finalClueShownKey] = 1; // Đánh dấu manh mối cuối đã hiển thị
                    // PlayerCharacterAttribute pca = character.GetComponent<PlayerCharacterAttribute>();
                    // if (pca != null && TheNetwork.Get() != null && TheNetwork.Get().IsOnline()) pca.RefreshPlayer();
                }
            }
        }
        // --------------------------------------------------------

        public override bool CanDoAction(PlayerCharacter character, InventorySlot slot)
        {
            ItemData item = slot.GetItem();
            if (item == null) return false;

            if (!string.IsNullOrEmpty(this.puzzleCompletionID) && this.clueItemGroupForTracking != null && item.HasGroup(this.clueItemGroupForTracking))
            {
               PlayerData playerData = PlayerData.Get(character.PlayerID);
               string finalClueKey = "final_clue_shown_" + this.puzzleCompletionID;
               if(playerData != null && playerData.unlocked_ids.ContainsKey(finalClueKey) && playerData.unlocked_ids[finalClueKey] == 1)
               {
                   // Nếu manh mối cuối đã hiện, và bạn không muốn action "Đọc" xuất hiện nữa cho các mảnh của puzzle đó:
                   // return false; 
                   // Hoặc, bạn có thể muốn cho phép đọc lại nhưng không kích hoạt CheckAllCluesCollected nữa (đã xử lý)
               }
            }
            return true; 
        }

        public override bool CanDoAction(PlayerCharacter character, Selectable select)
        {
             ReadObject read = select.GetComponent<ReadObject>();
             if (read != null) return true; 

             Item itemComp = select.GetComponent<Item>();
             if (itemComp != null && itemComp.data != null && this.clueItemGroupForTracking != null && itemComp.data.HasGroup(this.clueItemGroupForTracking))
             {
                 // (Tùy chọn) Logic tương tự CanDoAction cho InventorySlot nếu muốn ẩn action sau khi đọc xong hết
                 return true; 
             }
             return false; 
        }
    }
}