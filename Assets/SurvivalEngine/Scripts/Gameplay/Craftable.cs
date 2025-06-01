﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NetcodePlus;

namespace SurvivalEngine
{
    /// <summary>
    /// Base class for Items, Constructions, Characters, Plants
    /// </summary>

    [RequireComponent(typeof(Selectable))]
    [RequireComponent(typeof(UniqueID))]
    public abstract class Craftable : SObject
    {
        private Selectable cselect;
        private Destructible cdestruct;
        private Buildable cbuildable;

        protected override void Awake()
        {
            base.Awake(); 
            cselect = GetComponent<Selectable>();
            cdestruct = GetComponent<Destructible>();
            cbuildable = GetComponent<Buildable>();
        }

        //Get the data based on which type of object it is
        public new CraftData GetData()
        {
            if (this is Item)
                return ((Item)this).data;
            if (this is Plant)
                return ((Plant)this).data;
            if (this is Construction)
                return ((Construction)this).data;
            if (this is Character)
                return ((Character)this).data;
            return null;
        }

        public Selectable Selectable { get { return cselect; } }
        public Destructible Destructible { get { return cdestruct; } } //Can be null
        public Buildable Buildable { get { return cbuildable; } }    //Can be null

        //--- Static functions for easy access

        public static GameObject Create(CraftData data, Vector3 pos)
        {
            if (data == null)
                return null;

            if (!TheNetwork.Get().IsServer)
                return null;

            if (data is ItemData)
            {
                ItemData item = (ItemData)data;
                Item obj = Item.Create(item, pos, 1);
                return obj.gameObject;
            }

            if (data is PlantData)
            {
                PlantData item = (PlantData)data;
                Plant obj = Plant.Create(item, pos, -1);
                return obj.gameObject;
            }

            if (data is ConstructionData)
            {
                ConstructionData item = (ConstructionData)data;
                Construction obj = Construction.Create(item, pos);
                return obj.gameObject;
            }

            if (data is CharacterData)
            {
                CharacterData item = (CharacterData)data;
                Character obj = Character.Create(item, pos);
                return obj.gameObject;
            }

            return null;
        }
    }
}
