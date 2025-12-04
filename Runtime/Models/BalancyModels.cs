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
        public enum ObjectType
        {
            Unknown = 0,
            Sprite = 1 << 0,
            Asset = 1 << 2,
            View = 1 << 4
        }
        
        private string id;
        private ObjectType type;
        public string Id => id;

        public override void InitData()
        {
            base.InitData();
            id = GetStringParam("id");
            type = (ObjectType)GetIntParam("type");
        }
        
        public AsyncLoadHandler LoadSprite(Action<UnityEngine.Sprite> callback)
        {
            switch (type)
            {
                case ObjectType.Unknown:
                case ObjectType.Sprite:
                    return DataObjectsManager.GetObject(Id, callback);
                case ObjectType.Asset:
                {
                    // if (OnLoadAssetAsSprite != null)
                    //     return OnLoadAssetAsSprite?.Invoke(Name, callback);
                    Debug.LogError($"Addressables plugin wasn't found. Please add it to the project. {Id}");
                    break;
                }
            }

            return null;
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
        
        public AsyncLoadHandler LoadAsset(Action<UnityEngine.Object> callback)
        {
            switch (type)
            {
                case ObjectType.Unknown:
                case ObjectType.Sprite:
                    Debug.LogError($"You are trying to load sprite as an Object. Please use LoadSprite instead. {Id}");
                    return null;
                case ObjectType.Asset:
                    // if (OnLoadAssetAsObject != null)
                    //     return OnLoadAssetAsObject?.Invoke(Name, callback);
                    Debug.LogError($"Addressables plugin wasn't found. Please add it to the project. {Id}");
                    break;
            }

            return null;
        }

        public void Preload()
        {
            switch (type)
            {
                case ObjectType.Unknown:
                case ObjectType.Sprite:
                    //DataObjectsManager.PreloadObject(Id);
                    Debug.LogError("Not implemented yet. PreloadObject for sprite " + Id);
                    break;
                case ObjectType.Asset:
                    LoadAsset(asset =>
                    {
                        if (asset == null)
                            Debug.LogError($"Failed to preload asset {Id}");
                        else
                            Debug.Log("Preloaded asset " + Id);
                    });
                    break;
                case ObjectType.View:
                {
                    Balancy.Dictionaries.DataObjectsManager.GetObjectView(id, url =>
                    {
                    });
                    break;
                }
            }
        }

        public void OpenView(Action onShown, JsonBasedObject owner = null)
        {
            if (type != ObjectType.View)
            {
                Debug.LogError("You are trying to open view for object that is not a view. " + Id);
                return;
            }

            Balancy.Dictionaries.DataObjectsManager.GetObjectView(id, url =>
            {
                Balancy.RenderViewsManager.OpenLocalView(url, owner);
                onShown?.Invoke();
            });
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