using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NetcodePlus;
using Unity.Netcode;

namespace SurvivalEngine {

    [RequireComponent(typeof(Selectable))]
    public class ShopNPC : SNetworkBehaviour
    {
        public string title;

        [Header("Buy")]
        public ItemData[] items; //Buy Items

        [Header("Sell")]
        public GroupData sell_group; //Sell Items, if null, can sell anything

        private Selectable selectable;
        private SNetworkActions actions;

        protected override void Awake()
        {
            base.Awake();
            selectable = GetComponent<Selectable>();
        }

        protected override void OnSpawn()
        {
            base.OnSpawn();
            actions = new SNetworkActions(this);
            actions.RegisterSerializable(ActionType.Buy, DoBuyItem);
            actions.RegisterSerializable(ActionType.Sell, DoSellItem);
            actions.IgnoreAuthority(ActionType.Buy); //Any client can buy
            actions.IgnoreAuthority(ActionType.Sell); //Any client can sell
        }

        protected override void OnDespawn()
        {
            base.OnDespawn();
            actions.Clear();
        }

        public void OpenShop()
        {
            PlayerCharacter character = PlayerCharacter.GetNearest(transform.position);
            if (character != null && character.IsSelf())
                OpenShop(character);
        }

        public void OpenShop(PlayerCharacter player)
        {
            if (player.IsSelf())
            {
                ShopPanel.Get().ShowShop(player, this);
            }
        }

        public void BuyItem(PlayerCharacter player, ItemData item)
        {
            ShopTradeData trade = new ShopTradeData(player.player_id, item);
            actions?.Trigger(ActionType.Buy, trade); // DoBuyItem()
        }

        public void SellItem(PlayerCharacter player, ItemData item)
        {
            ShopTradeData trade = new ShopTradeData(player.player_id, item);
            actions?.Trigger(ActionType.Sell, trade); // DoSellItem()
        }

        private void DoBuyItem(SerializedData sdata)
        {
            ShopTradeData trade = sdata.Get<ShopTradeData>();
            if (trade != null)
            {
                PlayerCharacter player = PlayerCharacter.Get(trade.player_id);
                ItemData item = trade.item.Get<ItemData>();
                if (player != null && item != null)
                {
                    if (player.SaveData.gold >= item.buy_cost)
                    {
                        player.SaveData.gold -= item.buy_cost;
                        player.Inventory.GainItem(item, 1);

                        if(player.IsSelf())
                            ShopPanel.Get()?.AfterTrade();
                    }
                }
            }
        }

        private void DoSellItem(SerializedData sdata)
        {
            ShopTradeData trade = sdata.Get<ShopTradeData>();
            if (trade != null)
            {
                PlayerCharacter player = PlayerCharacter.Get(trade.player_id);
                ItemData item = trade.item.Get<ItemData>();
                if (player != null && item != null)
                {
                    if (player.InventoryData.HasItem(item.id, 1) && item.sell_cost > 0 && CanSell(item))
                    {
                        player.SaveData.gold += item.sell_cost;
                        player.InventoryData.RemoveItem(item.id, 1);
                        player.Inventory.Refresh(player.InventoryData);

                        if (player.IsSelf())
                            ShopPanel.Get()?.AfterTrade();
                    }
                }
            }
        }

        public bool CanSell(ItemData item)
        {
            return sell_group == null || item.HasGroup(sell_group);
        }
    }

    public class ShopTradeData : INetworkSerializable
    {
        public int player_id;
        public CraftDataRef item;

        public ShopTradeData() { }
        public ShopTradeData(int p, CraftData d) { player_id = p; item = new CraftDataRef(d); }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref player_id);
            serializer.SerializeNetworkSerializable(ref item);
        }
    }

}
