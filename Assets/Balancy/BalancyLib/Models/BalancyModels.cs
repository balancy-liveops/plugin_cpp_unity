using System;
using Balancy.Dictionaries;
using UnityEngine;

namespace Balancy.Localization
{
    public class LocalizedString
    {
        public readonly string Key;

        public string Value
        {
            get
            {
#if BALANCY_SERVER
                return Key;
#else
                if (string.IsNullOrEmpty(Key))
                    return Key;
                
                return Balancy.API.Localization.GetLocalizedValue(Key);
#endif
            }
        }

        public bool HasValue => !string.IsNullOrEmpty(Key);

        public LocalizedString(string localizedKey)
        {
            Key = localizedKey;
        }

        public override string ToString()
        {
            return Value;
        }
    }
}

namespace Balancy.Models
{
    public class UnnyColor
    {
        public readonly string Value;

        public UnnyColor(string v)
        {
            Value = v;
        }
    }
    
    public class UnnyDate : JsonBasedObject
    {
        internal static DateTime EPOCH_START = new DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc);
        
        private int time;
        public int Time => time;

        public override void InitData()
        {
            base.InitData();
            time = GetIntParam("value");
        }

        private DateTime? dateTimeUtc;
        public DateTime DateTimeUtc
        {
            get
            {
                if (!dateTimeUtc.HasValue)
                    dateTimeUtc = EPOCH_START.AddSeconds(Time);

                return dateTimeUtc.Value;
            }
        }
        
        private DateTime? dateTimeGame;
        public DateTime DateTimeGame
        {
            get
            {
                if (!dateTimeGame.HasValue)
                {
                    var status = API.GetStatus();
                    if (status == null)
                        return EPOCH_START;
                    dateTimeGame = EPOCH_START.AddSeconds(status.GameTime - status.ServerTime + Time);
                }

                return dateTimeGame.Value;
            }
        }
    }
    
    public class UnnyObject : JsonBasedObject
    {
        private string id;
        public string Id => id;

        public override void InitData()
        {
            base.InitData();
            id = GetStringParam("id");
        }
        
        public AsyncLoadHandler LoadSprite(Action<UnityEngine.Sprite> callback)
        {
            return DataObjectsManager.GetObject(Id, callback);
        }
        
        /// <summary>
        /// Clears sprite from memory cache and destroys it
        /// </summary>
        public void ClearFromMemory()
        {
            DataObjectsManager.ClearFromMemory(Id);
        }
        
        /// <summary>
        /// Removes sprite from disk cache
        /// </summary>
        public void ClearFromDisk()
        {
            DataObjectsManager.ClearFromDisk(Id);
        }
    }
    
    public class UnnyAsset : JsonBasedObject
    {
        private string name;
        public string Name => name;

        public override void InitData()
        {
            base.InitData();
            name = GetStringParam("name");
        }
    }
    
    public class UnnyProduct : JsonBasedObject
    {
        private string productId;
        public string ProductId => productId;
        
        private float price;
        public float Price => price;
        
        public override void InitData()
        {
            base.InitData();
            productId = GetStringParam("productId");
            price = GetFloatParam("price");
        }
    }
}