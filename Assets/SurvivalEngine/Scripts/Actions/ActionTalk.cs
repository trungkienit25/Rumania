// File: Scripts/Actions/ActionTalk.cs
using UnityEngine;

namespace SurvivalEngine // QUAN TRỌNG: Phải cùng namespace với SAction
{
    // Dòng này quyết định sự xuất hiện của action trong menu Create
    [CreateAssetMenu(fileName = "ActionTalk", menuName = "SurvivalEngine/Actions/Talk", order = 85)]
    public class ActionTalk : SAction // Kế thừa từ SAction
    {
        [Header("Dialogue Content")]
        [Tooltip("Tiêu đề của hộp thoại, ví dụ: tên NPC.")]
        public string dialogue_title;

        [Tooltip("Nội dung lời thoại hoặc manh mối sẽ được hiển thị trong ReadPanel.")]
        [TextArea(5, 10)]
        public string dialogue_text;

        // Hàm này sẽ được gọi khi người chơi chọn action "Nói Chuyện" với một Selectable
        public override void DoAction(PlayerCharacter character, Selectable select)
        {
            if (character != null && character.IsSelf() && select != null)
            {
                // Nếu bạn muốn nhân vật di chuyển đến gần NPC trước khi nói chuyện:
                // (Điều này yêu cầu bạn đã thêm hàm MoveToInteract vào PlayerCharacter.cs)
                /*
                float interactionRange = select.use_range + character.interact_range;
                character.MoveToInteract(select.transform.position, interactionRange, () => {
                    if (select.IsInUseRange(character))
                    {
                        character.transform.LookAt(select.transform.position); // Quay mặt về phía NPC
                        ReadPanel.Get().ShowPanel(this.dialogue_title, this.dialogue_text);
                    }
                });
                */

                // Nếu không cần di chuyển, chỉ nói chuyện khi đã ở trong tầm:
                if (select.IsInUseRange(character))
                {
                    character.transform.LookAt(select.transform.position); // Quay mặt về phía NPC
                    ReadPanel.Get().ShowPanel(this.dialogue_title, this.dialogue_text);
                }
                else
                {
                    Debug.Log("Cần đến gần hơn để nói chuyện.");
                    // Có thể thêm một thông báo nhỏ cho người chơi ở đây nếu muốn
                }
            }
        }

        // Hàm này kiểm tra xem có thể thực hiện action "Nói Chuyện" không
        public override bool CanDoAction(PlayerCharacter character, Selectable select)
        {
            // Luôn cho phép nói chuyện nếu Selectable có action này và đang không có panel nào khác hiển thị
            return select != null && !TheUI.Get().IsFullPanelOpened();
        }

        // Các phương thức này không dùng cho action này, để trống
        public override void DoAction(PlayerCharacter character, InventorySlot slot) { }
        public override bool CanDoAction(PlayerCharacter character, InventorySlot slot) { return false; }
    }
}