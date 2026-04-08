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

namespace Balancy
{
    /// <summary>
    /// A lightweight reference to a Visual Scripting script in the CMS.
    /// Holds the script's CMS ID and provides methods to launch and stop it.
    /// </summary>
    public class ScriptRef
    {
        /// <summary>
        /// The script's CMS ID (unnyId).
        /// </summary>
        public readonly string ScriptId;

        /// <summary>
        /// Whether this reference points to a valid script (i.e., has a non-empty ID).
        /// </summary>
        public bool HasValue => !string.IsNullOrEmpty(ScriptId);

        public ScriptRef(string scriptId)
        {
            ScriptId = scriptId;
        }

        /// <summary>
        /// Launch this script.
        /// </summary>
        /// <param name="launcherId">Optional identifier for who launched the script. Accessible inside the script via GetLauncherId().</param>
        /// <param name="inputJson">Optional JSON string with input parameters keyed by port name. Example: {"score":100,"mode":"hard"}</param>
        /// <param name="onComplete">Optional callback invoked when the script finishes. Receives (exitPortName, outputsJson).</param>
        /// <returns>The instance ID of the running script, or empty string on failure.</returns>
        public string Launch(string launcherId = null, string inputJson = null, Action<string, string> onComplete = null)
        {
            if (!HasValue)
                return "";
            return API.VisualScripting.RunScriptById(ScriptId, launcherId, inputJson, onComplete);
        }

        /// <summary>
        /// Stop a running instance of this script.
        /// </summary>
        /// <param name="instanceId">The instance ID returned from Launch().</param>
        /// <returns>True if the script was found and stopped.</returns>
        public static bool Stop(string instanceId)
        {
            return API.VisualScripting.StopScript(instanceId);
        }

        public override string ToString()
        {
            return ScriptId ?? "";
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
        private string name;
        private ObjectType type;
        public string Id => id;
        public string Name => name;

        public override void InitData()
        {
            base.InitData();
            id = GetStringParam("id");
            name = GetStringParam("name");
            type = (ObjectType)GetIntParam("type");
        }
        
        public static Func<string, Action<UnityEngine.Sprite>, AsyncLoadHandler> OnLoadAssetAsSprite = null;
        public static Func<string, Action<UnityEngine.Object>, AsyncLoadHandler> OnLoadAssetAsObject = null;
        
        public AsyncLoadHandler LoadSprite(Action<UnityEngine.Sprite> callback)
        {
            switch (type)
            {
                case ObjectType.Unknown:
                case ObjectType.Sprite:
                    return DataObjectsManager.GetSprite(Id, callback);
                case ObjectType.Asset:
                {
                    if (OnLoadAssetAsSprite != null)
                        return OnLoadAssetAsSprite?.Invoke(Name, callback);
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
                    if (OnLoadAssetAsObject != null)
                        return OnLoadAssetAsObject?.Invoke(Name, callback);
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
        
        public void LoadFile(Action<string> callback)
        {
            Balancy.Dictionaries.DataObjectsManager.GetObject(Id, callback);
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

namespace Balancy.Models.Core
{
    public class Vector2 : JsonBasedObject
    {
        private float x;
        private float y;
        public float X => x;
        public float Y => y;

        public override void InitData()
        {
            base.InitData();
            x = GetFloatParam("x");
            y = GetFloatParam("y");
        }

#if !BALANCY_SERVER
        public UnityEngine.Vector2 Value => new UnityEngine.Vector2(X, Y);
#endif

        public override string ToString() => $"({X},{Y})";
    }

    public class Vector3 : JsonBasedObject
    {
        private float x;
        private float y;
        private float z;
        public float X => x;
        public float Y => y;
        public float Z => z;

        public override void InitData()
        {
            base.InitData();
            x = GetFloatParam("x");
            y = GetFloatParam("y");
            z = GetFloatParam("z");
        }

#if !BALANCY_SERVER
        public UnityEngine.Vector3 Value => new UnityEngine.Vector3(X, Y, Z);
#endif

        public override string ToString() => $"({X},{Y},{Z})";
    }

    public class Vector4 : JsonBasedObject
    {
        private float x;
        private float y;
        private float z;
        private float w;
        public float X => x;
        public float Y => y;
        public float Z => z;
        public float W => w;

        public override void InitData()
        {
            base.InitData();
            x = GetFloatParam("x");
            y = GetFloatParam("y");
            z = GetFloatParam("z");
            w = GetFloatParam("w");
        }

#if !BALANCY_SERVER
        public UnityEngine.Vector4 Value => new UnityEngine.Vector4(X, Y, Z, W);
#endif

        public override string ToString() => $"({X},{Y},{Z},{W})";
    }

    public class Vector2Int : JsonBasedObject
    {
        private int x;
        private int y;
        public int X => x;
        public int Y => y;

        public override void InitData()
        {
            base.InitData();
            x = GetIntParam("x");
            y = GetIntParam("y");
        }

#if !BALANCY_SERVER
        public UnityEngine.Vector2Int Value => new UnityEngine.Vector2Int(X, Y);
#endif

        public override string ToString() => $"({X},{Y})";
    }

    public class Vector3Int : JsonBasedObject
    {
        private int x;
        private int y;
        private int z;
        public int X => x;
        public int Y => y;
        public int Z => z;

        public override void InitData()
        {
            base.InitData();
            x = GetIntParam("x");
            y = GetIntParam("y");
            z = GetIntParam("z");
        }

#if !BALANCY_SERVER
        public UnityEngine.Vector3Int Value => new UnityEngine.Vector3Int(X, Y, Z);
#endif

        public override string ToString() => $"({X},{Y},{Z})";
    }

    public class RangeInt : JsonBasedObject
    {
        private int min;
        private int max;
        public int Min => min;
        public int Max => max;

        public override void InitData()
        {
            base.InitData();
            min = GetIntParam("min");
            max = GetIntParam("max");
        }

#if !BALANCY_SERVER
        public int GetRandom() => UnityEngine.Random.Range(Min, Max + 1);
#endif

        public override string ToString() => $"[{Min}, {Max}]";
    }

    public class RangeFloat : JsonBasedObject
    {
        private float min;
        private float max;
        public float Min => min;
        public float Max => max;

        public override void InitData()
        {
            base.InitData();
            min = GetFloatParam("min");
            max = GetFloatParam("max");
        }

#if !BALANCY_SERVER
        public float GetRandom() => UnityEngine.Random.Range(Min, Max);
#endif

        public override string ToString() => $"[{Min}, {Max}]";
    }
}