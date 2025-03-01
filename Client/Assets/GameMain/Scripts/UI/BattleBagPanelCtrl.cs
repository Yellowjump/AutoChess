using System.Collections.Generic;
using System.Linq;
using DataTable;
using GameFramework.Event;
using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;
using SelfEventArg;
using SkillSystem;
using TMPro;
using UnityEngine.Pool;

public class BattleBagPanelCtrl : UIFormLogic
{
    public enum BagPanelShowState
    {
        /// <summary>
        /// 合成
        /// </summary>
        Craft,
        /// <summary>
        /// 装备
        /// </summary>
        HeroEquip,
    }

    private BagPanelShowState _curShowState;
    [SerializeField]
    private Button _btnContinue;
    [SerializeField]
    private Button _btnCraft;
    [SerializeField]
    private BattleBagItem _craftItem;
    [SerializeField] private Transform _craftParent;
    [SerializeField] private Transform _willCraftParent;
    [SerializeField] private TextMeshProUGUI _craftItemName;
    [SerializeField] private TextMeshProUGUI _craftItemDesc;
    [SerializeField] private TextMeshProUGUI _craftItemRarity;
    [SerializeField] private Transform _joinCraftItemParent;
    [SerializeField]
    private BattleBagItem _bagItemTemp;
    [SerializeField] private Transform _bagItemParent;
    
   
    private ObjectPool<BattleBagItem> _itemPool;
    
    [SerializeField]
    private Transform _releaseItemPa;
    private List<BattleBagItem> _curShowBagItemList = new();
    private List<BattleBagItem> _joinCraftItemList = new();
    
    
    //------------heroEquip--------------
    [SerializeField] private Transform _heroEquipParent;
    [SerializeField] private Transform _heroEquipItemParent;
    [SerializeField] private BattleBagHeroToggleItem _bagHeroToggleItemTemp;
    [SerializeField] private Transform _bagHeroToggleItemParent;
    private ObjectPool<BattleBagHeroToggleItem> _heroToggleItemPool;
    private List<BattleBagHeroToggleItem> _curHeroToggleItemList = new();
    private List<BattleBagItem> _curHeroEquipItemList = new();

    private int _curShowHeroUID = -1;
    //------------itemTip--------------
    [SerializeField] private Transform _itemTipParent;
    [SerializeField] private TextMeshProUGUI _itemTipName;
    [SerializeField] private TextMeshProUGUI _itemTipRarity;
    [SerializeField] private TextMeshProUGUI _itemTipCast;
    [SerializeField] private TextMeshProUGUI _itemTipType;
    [SerializeField] private TextMeshProUGUI _itemTipCDNum;
    [SerializeField] private TextMeshProUGUI _itemTipDec;
    [SerializeField] private TextMeshProUGUI _itemTipAtkDistance;
    private const int _tipPosOffsetX = 250;
    private const int _tipPosOffsetY = 150;
    public override void OnInit(object userData)
    {
        base.OnInit(userData);
        _btnContinue.onClick.AddListener(OnClickContinueBtn);
        _btnCraft.onClick.AddListener(OnClickCraftBtn);
        _itemPool ??= new ObjectPool<BattleBagItem>(() =>
        {
            GameObject ob = Instantiate(_bagItemTemp.gameObject, _releaseItemPa);
            BattleBagItem ri = ob.GetComponent<BattleBagItem>();
            if (ri != null)
            {
                ri.OnClickPointCallback = OnClickItem;
                ri.OnPointEnterCallback = OnPointItemEnter;
                ri.OnPointExitCallback = OnPointItemExit;
                ri.Init();
            }
            return ri;
        }, (item) => {item.gameObject.SetActive(false);}, (item) => {item.gameObject.SetActive(false);item.OnRelease();item.transform.SetParent(_releaseItemPa);},
            (item) => { Destroy(item.gameObject);
                Destroy(item);
            });
        _craftItem.Init();
        _craftItem.CurItemType = BattleBagItem.ItemType.CraftResult;
        _craftItem.OnClickPointCallback = OnClickItem;
        
        //---------英雄装备----------
        
        _heroToggleItemPool ??= new ObjectPool<BattleBagHeroToggleItem>(() =>
        {
            GameObject ob = Instantiate(_bagHeroToggleItemTemp.gameObject, _releaseItemPa);
            ob.SetActive(true);
            BattleBagHeroToggleItem ri = ob.GetComponent<BattleBagHeroToggleItem>();
            if (ri != null)
            {
                ri.OnClickPointCallback = OnClickHeroToggleItem;
                ri.Init();
            }
            return ri;
        }, (item) => {item.transform.SetParent(_bagHeroToggleItemParent);}, (item) => {;item.transform.SetParent(_releaseItemPa);}, (item) => { Destroy(item.gameObject); });
    }

    public override void OnOpen(object userData)
    {
        base.OnOpen(userData);
        _itemTipParent.gameObject.SetActive(false);
        GameEntry.Event.Subscribe(BagPanelCheckToCraftEventArgs.EventId,OnCheckToCraft);
        GameEntry.Event.Subscribe(BagPanelCheckToEquipEventArgs.EventId,OnCheckToEquip);
        ShowBagItem();
        if (userData != null)
        {
            if ((BagPanelShowState)userData == BagPanelShowState.HeroEquip)
            {
                _curShowState = BagPanelShowState.HeroEquip;
                _craftParent.gameObject.SetActive(false);
                _heroEquipParent.gameObject.SetActive(true);
                ShowHeroEquip();
            }
            else if ((BagPanelShowState)userData == BagPanelShowState.Craft)
            {
                _curShowState = BagPanelShowState.Craft;
                _craftParent.gameObject.SetActive(true);
                _heroEquipParent.gameObject.SetActive(false);
                InitWillCraftItem();
            }
        }
    }

    private void InitWillCraftItem()
    {
        _btnCraft.interactable = false;
        _craftItem.ItemID = 0;
        _willCraftParent.gameObject.SetActive(false);
    }
    
    private void ShowBagItem()
    {
        var bagDic = SelfDataManager.Instance.ItemBag;
        
        foreach (var keyValue in bagDic.OrderBy((ky)=>ky.Key))
        {
            var oneItem = _itemPool.Get();
            oneItem.transform.SetParent(_bagItemParent);
            oneItem.ItemID =keyValue.Key;
            oneItem.itemNum = keyValue.Value;
            oneItem.CurItemType = BattleBagItem.ItemType.Bag;
            oneItem.Fresh();
            _curShowBagItemList.Add(oneItem);
        }
    }
    
    public override void OnClose(bool isShutdown, object userData)
    {
        base.OnClose(isShutdown, userData);
        GameEntry.Event.Unsubscribe(BagPanelCheckToCraftEventArgs.EventId,OnCheckToCraft);
        GameEntry.Event.Unsubscribe(BagPanelCheckToEquipEventArgs.EventId,OnCheckToEquip);
        ClearShowBagItem();
        ClearAllJoinCraftItem();
        ClearCurHeroEquipItem();
        ClearHeroToggleItem();
        _curShowHeroUID = -1;
    }

    private void ClearShowBagItem()
    {
        foreach (var item in _curShowBagItemList)
        {
            _itemPool?.Release(item);
        }
        _curShowBagItemList.Clear();
    }
    private void ClearAllJoinCraftItem()
    {
        
        foreach (var item in _joinCraftItemList)
        {
            _itemPool?.Release(item);
        }
        _joinCraftItemList.Clear();
    }

    private void ClearCurHeroEquipItem()
    {
        foreach (var item in _curHeroEquipItemList)
        {
            _itemPool?.Release(item);
        }
        _curHeroEquipItemList.Clear();
    }

    private void ClearHeroToggleItem()
    {
        foreach (var item in _curHeroToggleItemList)
        {
            _heroToggleItemPool?.Release(item);
        }
        _curHeroToggleItemList.Clear();
    }
    private void OnClickItem(BattleBagItem battleBagItem)
    {
        if (_curShowState == BagPanelShowState.Craft)
        {
            if (battleBagItem.CurItemType == BattleBagItem.ItemType.Bag)
            {
                OnClickBagItem(battleBagItem);
            }
            else if (battleBagItem.CurItemType == BattleBagItem.ItemType.InJoinCraft)
            {
                OnClickJoinCraftItem(battleBagItem);
            }
            OnJoinCraftItemChanged();
        }
        else if (_curShowState == BagPanelShowState.HeroEquip)
        {
            if (battleBagItem.CurItemType == BattleBagItem.ItemType.Bag)
            {
                OnClickBagItemWhenHeroEquip(battleBagItem);
            }
            else if (battleBagItem.CurItemType == BattleBagItem.ItemType.HeroEquip)
            {
                OnClickHeroEquipItem(battleBagItem);
            }
        }
    }

    private void OnPointItemEnter(BattleBagItem battleBagItem)
    {
        if (battleBagItem.CurItemType == BattleBagItem.ItemType.CraftResult)
        {
            return;
        }
        _itemTipParent.gameObject.SetActive(true);
        var xOffset = battleBagItem.transform.position.x > 0 ? -_tipPosOffsetX : _tipPosOffsetX;
        var yOffset = battleBagItem.transform.position.y > 0 ? -_tipPosOffsetY : _tipPosOffsetY;
        _itemTipParent.position = battleBagItem.transform.position + new Vector3(xOffset,yOffset,0);
        var itemTable = GameEntry.DataTable.GetDataTable<DRItem>("Item");
        if (!itemTable.HasDataRow(battleBagItem.ItemID))
        {
            Log.Error($"Item Table not Contain {battleBagItem.ItemID}");
            return;
        }

        _itemTipName.text = GameEntry.Localization.GetString(itemTable[battleBagItem.ItemID].Name);
        _itemTipDec.text = itemTable[battleBagItem.ItemID].Decs;
        _itemTipRarity.color = ConstValue.RarityColorList[itemTable[battleBagItem.ItemID].Rarity];
        _itemTipRarity.text = ConstValue.RarityNameList[itemTable[battleBagItem.ItemID].Rarity];
        var skillTable = GameEntry.DataTable.GetDataTable<DRSkill>("Skill");
        if (skillTable.HasDataRow(itemTable[battleBagItem.ItemID].SkillID))
        {
            var skillData = skillTable[itemTable[battleBagItem.ItemID].SkillID];
            if (skillData.SkillType == (int)SkillType.NormalSkill)
            {
                _itemTipAtkDistance.text = skillData.SkillRange.ToString();
            }
            _itemTipAtkDistance.gameObject.SetActive(skillData.SkillType == (int)SkillType.NormalSkill);
            _itemTipCast.gameObject.SetActive(skillData.SkillType != (int)SkillType.PassiveSkill);
            string castOrGet = string.Empty;
            if (skillData.CastPower == 0)
            {
                castOrGet = "无消耗";
            }
            else if (skillData.CastPower > 0)
            {
                castOrGet = $"消耗{skillData.CastPower}法力值";
            }
            else
            {
                castOrGet = $"获得{-skillData.CastPower}法力值";
            }
            _itemTipCast.text = castOrGet;
            
        }
        
        
    }

    private void OnPointItemExit(BattleBagItem battleBagItem)
    {
        if (battleBagItem.CurItemType == BattleBagItem.ItemType.CraftResult)
        {
            return;
        }
        _itemTipParent.gameObject.SetActive(false);
    }


    private void OnClickBagItem(BattleBagItem battleBagItem)
    {
        var itemID = battleBagItem.ItemID;
        var itemBagNum = battleBagItem.itemNum;
        var joinItem = _joinCraftItemList.FindLast((item) => item.ItemID == itemID);
        if (joinItem == null)
        {
            joinItem = _itemPool.Get();
            joinItem.CurItemType = BattleBagItem.ItemType.InJoinCraft;
            joinItem.transform.SetParent(_joinCraftItemParent);
            joinItem.ItemID =itemID;
            joinItem.itemNum = 1;
            joinItem.Fresh();
            _joinCraftItemList.Add(joinItem);
        }
        else
        {
            joinItem.itemNum++;
            joinItem.FreshNum();
        }

        if (itemBagNum <= 1)
        {
            _curShowBagItemList.Remove(battleBagItem);
            _itemPool.Release(battleBagItem);
        }
        else
        {
            battleBagItem.itemNum--;
            battleBagItem.FreshNum();
        }
    }
    private void OnClickJoinCraftItem(BattleBagItem craftItem)
    {
        var itemID = craftItem.ItemID;
        
        if(craftItem.itemNum<=1)
        {
            _joinCraftItemList.Remove(craftItem);
            _itemPool.Release(craftItem);
        }
        else
        {
            craftItem.itemNum--;
            craftItem.FreshNum();
        }
        var bagItem = _curShowBagItemList.FindLast((item) => item.ItemID == itemID);
        if (bagItem == null)
        {
            bagItem = _itemPool.Get();
            bagItem.CurItemType = BattleBagItem.ItemType.Bag;
            bagItem.transform.SetParent(_bagItemParent);
            bagItem.ItemID =itemID;
            bagItem.itemNum = 1;
            bagItem.Fresh();
            _curShowBagItemList.Add(bagItem);
        }
        else
        {
            bagItem.itemNum++;
            bagItem.FreshNum();
        }
    }

    private void OnJoinCraftItemChanged()
    {
        var itemTable = GameEntry.DataTable.GetDataTable<DRItem>("Item");
        int matchID = -1;
        foreach (var oneItem in itemTable)
        {
            if (oneItem.CraftList.Count != 0)
            {
                if (MeetCraftList(oneItem.CraftList))
                {
                    matchID = oneItem.Id;
                }
            }
        }

        if (matchID != -1)
        {
            if (matchID != _craftItem.ItemID)
            {
                _craftItem.ItemID = matchID;
                _craftItemName.text = GameEntry.Localization.GetString(itemTable[matchID].Name);
                _craftItemDesc.text = itemTable[matchID].Decs;
                _craftItemRarity.color = ConstValue.RarityColorList[itemTable[matchID].Rarity];
                _craftItemRarity.text = ConstValue.RarityNameList[itemTable[matchID].Rarity];
                _craftItem.Fresh();
                _willCraftParent.gameObject.SetActive(true);
                _btnCraft.interactable = true;
            }
        }
        else
        {
            _craftItem.ItemID = 0;
            _willCraftParent.gameObject.SetActive(false);
            _btnCraft.interactable = false;
        }
        
    }

    private bool MeetCraftList(List<(int, int)> needItem)
    {
        if (_joinCraftItemList == null || needItem == null || needItem.Count==0|| _joinCraftItemList.Count != needItem.Count)
        {
            return false;
        }

        foreach (var idAndNum in needItem)
        {
            if (_joinCraftItemList.Exists((item) => item.ItemID == idAndNum.Item1 && item.itemNum == idAndNum.Item2) == false)
            {
                return false;
            }
        }
        return true;
    }
    private void OnClickContinueBtn()
    {
        GameEntry.UI.CloseUIForm(UIForm);
    }
    private void OnClickCraftBtn()
    {
        if (_craftItem != null && _craftItem.ItemID != 0)
        {
            if (SelfDataManager.Instance.TryCraftItem(_craftItem.ItemID))
            {
                _craftItem.ItemID = 0;
                _willCraftParent.gameObject.SetActive(false);
                //打开 tip 界面通知获取物品（要不要做呢）
                
                //刷新界面
                FreshBagItem();
            }
        }
    }

    
    public void FreshBagItem()
    {
        ClearShowBagItem();
        ClearAllJoinCraftItem();
        ShowBagItem();
    }
    
    public void OnCheckToCraft(object sender,GameEventArgs e)
    {
        BagPanelCheckToCraftEventArgs ne = (BagPanelCheckToCraftEventArgs)e;
        if (ne == null)
        {
            return;
        }

        if (_curShowState == BagPanelShowState.Craft)
        {
            return;
        }

        _curShowState = BagPanelShowState.Craft;
        _heroEquipParent.gameObject.SetActive(false);
        _craftParent.gameObject.SetActive(true);
        InitWillCraftItem();
    }

    #region ------------------------------英雄装备--------------------------------------
    private void ShowHeroEquip()
    {
        //刷新角色toggle（实际是button）
        var heroList = GameEntry.HeroManager.QiziCSList;
        foreach (var oneHero in heroList.OrderBy((a)=>a.HeroUID))
        {
            var oneHeroItem = _heroToggleItemPool.Get();
            oneHeroItem.HeroUID = oneHero.HeroUID;
            oneHeroItem.HeroName = oneHero.HeroUID.ToString();
            oneHeroItem.Fresh();
            _curHeroToggleItemList.Add(oneHeroItem);
        }
        //显示第一个角色的当前装备
        if (_curHeroToggleItemList != null)
        {
            var firstHeroToggle = _curHeroToggleItemList.First();
            OnClickHeroToggleItem(firstHeroToggle); 
        }
    }
    private void OnClickBagItemWhenHeroEquip(BattleBagItem battleBagItem)
    {
        var itemID = battleBagItem.ItemID;
        var itemBagNum = battleBagItem.itemNum;
        var success = SelfDataManager.Instance.TryEquipItem(_curShowHeroUID, itemID);
        if (!success)
        {
            return;
        }
        var newEquipItem = _itemPool.Get();
        newEquipItem.CurItemType = BattleBagItem.ItemType.HeroEquip;
        newEquipItem.transform.SetParent(_heroEquipItemParent);
        newEquipItem.ItemID =itemID;
        newEquipItem.itemNum = 1;
        newEquipItem.Fresh();
        _curHeroEquipItemList.Add(newEquipItem);

        if (itemBagNum <= 1)
        {
            _curShowBagItemList.Remove(battleBagItem);
            _itemPool.Release(battleBagItem);
        }
        else
        {
            battleBagItem.itemNum--;
            battleBagItem.FreshNum();
        }
    }
    private void OnClickHeroEquipItem(BattleBagItem battleBagItem)
    {
        
        var itemID = battleBagItem.ItemID;
        var equipIndex = _curHeroEquipItemList.IndexOf(battleBagItem);
        var success = SelfDataManager.Instance.TryRemoveEquip(_curShowHeroUID, itemID,equipIndex);
        if (!success)
        {
            return;
        }
        _curHeroEquipItemList.Remove(battleBagItem);
        _itemPool.Release(battleBagItem);
        var bagItem = _curShowBagItemList.FindLast((item) => item.ItemID == itemID);
        if (bagItem == null)
        {
            bagItem = _itemPool.Get();
            bagItem.CurItemType = BattleBagItem.ItemType.Bag;
            bagItem.transform.SetParent(_bagItemParent);
            bagItem.ItemID =itemID;
            bagItem.itemNum = 1;
            bagItem.Fresh();
            _curShowBagItemList.Add(bagItem);
        }
        else
        {
            bagItem.itemNum++;
            bagItem.FreshNum();
        }
    }
    private void OnClickHeroToggleItem(BattleBagHeroToggleItem battleBagHeroToggleItem)
    {
        var heroUID = battleBagHeroToggleItem.HeroUID;
        if (_curShowHeroUID == heroUID)
        {
            return;
        }
        _curShowHeroUID = heroUID;
        ClearCurHeroEquipItem();
        var entity = GameEntry.HeroManager.GetEntityByUID(heroUID);
        if (entity != null)
        {
            foreach (var itemID in entity.EquipItemList)
            {
                var oneItem = _itemPool.Get();
                oneItem.transform.SetParent(_heroEquipItemParent);
                oneItem.ItemID =itemID;
                oneItem.itemNum = 1;
                oneItem.CurItemType = BattleBagItem.ItemType.HeroEquip;
                oneItem.Fresh();
                _curHeroEquipItemList.Add(oneItem);
            }

            
        }
    }
    public void OnCheckToEquip(object sender,GameEventArgs e)
    {
        BagPanelCheckToEquipEventArgs ne = (BagPanelCheckToEquipEventArgs)e;
        if (ne == null)
        {
            return;
        }
        if (_curShowState == BagPanelShowState.HeroEquip)
        {
            return;
        }
        _curShowState = BagPanelShowState.HeroEquip;
        _heroEquipParent.gameObject.SetActive(true);
        _craftParent.gameObject.SetActive(false);
        FreshBagItem();
        ClearHeroToggleItem();
        ShowHeroEquip();
    }
    #endregion
}
