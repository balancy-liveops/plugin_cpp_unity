using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Balancy.Dictionaries
{
    public class DataObjectsManager
    {
        private static string CACHE_PATH;
        
        [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
        internal class SharedObjectInfo
        {
            public int PixelsPerUnit;
            public int OffsetTop;
            public int OffsetBottom;
            public int OffsetRight;
            public int OffsetLeft;
            
            [MarshalAs(UnmanagedType.LPStr)] public string UnnyId;
            [MarshalAs(UnmanagedType.LPStr)] public string LocationPath;
        }

        static DataObjectsManager()
        {
            CACHE_PATH = Application.persistentDataPath + "/Balancy/Models/";
            _mainThreadInstance = UnityMainThreadDispatcher.Instance();
        }

        private static UnityMainThreadDispatcher _mainThreadInstance; 

        private class OneObjectSprite
        {
            private class CallbackInfo
            {
                public Action<UnityEngine.Sprite> Callback;
                public AsyncLoadHandler LoadHandler;
            }

            private SharedObjectInfo _spriteInfo = null;
            
            public bool Loaded = false;
            private readonly List<CallbackInfo> _callbacks = new List<CallbackInfo>();
            public Sprite Sprite;
            
            private string PathInStorage => CACHE_PATH + _spriteInfo?.LocationPath;

            public void PrepareSprite(SharedObjectInfo spriteInfo)
            {
                _spriteInfo = spriteInfo;
                if (spriteInfo != null)
                {
                    Texture2D texture = TryToLoadTextureFromResources();

                    if (texture == null)
                    {
                        var path = PathInStorage;
                        if (File.Exists(path))
                        {
                            byte[] bytes = File.ReadAllBytes(path);
                            texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                            texture.LoadImage(bytes);
                        }
                        else
                        {
                            Debug.LogError("NO FILE PATH " + path);
                            SetSprite(null);
                            return;
                        }
                    }

                    Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), 
                        new Vector2(0.5f, 0.5f), spriteInfo.PixelsPerUnit, 0, 
                        SpriteMeshType.FullRect, 
                        new Vector4(spriteInfo.OffsetLeft, spriteInfo.OffsetBottom, spriteInfo.OffsetRight, spriteInfo.OffsetTop));
                    SetSprite(sprite);
                } else 
                    SetSprite(null);
            }

            private Texture2D TryToLoadTextureFromResources()
            {
                var resourcesPath = _spriteInfo.LocationPath.Replace('/', '-');
                string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(resourcesPath);
                return Resources.Load<Texture2D>(fileNameWithoutExtension);
            }
            
            private void SetSprite(Sprite sprite)
            {
                Sprite = sprite;
                Loaded = Sprite != null;

                foreach (var info in _callbacks)
                {
                    if (info.LoadHandler.GetStatus() == AsyncLoadHandler.Status.Loading)
                    {
                        info.LoadHandler.Finish();
                        info.Callback?.Invoke(Sprite);
                    }
                }
                
                _callbacks.Clear();
            }

            public void AddCallback(AsyncLoadHandler handler, Action<UnityEngine.Sprite> callback)
            {
                var info = new CallbackInfo
                {
                    LoadHandler = handler,
                    Callback = callback
                };
                _callbacks.Add(info);
            }
        }

        private static readonly Dictionary<string, OneObjectSprite> AllSprites = new Dictionary<string, OneObjectSprite>();

        [AOT.MonoPInvokeCallback(typeof(LibraryMethods.Models.DataObjectWasCachedCallback))]
        private static void DataObjectLoaded(string id, IntPtr ptr)
        {
            try
            {
                if (ptr == IntPtr.Zero)
                {
                    Debug.LogError("Failed to load DataObject " + id);
                }
                else
                {
                    if (AllSprites.TryGetValue(id, out var oneObjectSprite))
                    {
                        var sharedObject = Marshal.PtrToStructure<SharedObjectInfo>(ptr);

                        _mainThreadInstance.Enqueue(() => { oneObjectSprite.PrepareSprite(sharedObject); });
                    }
                    else
                        Debug.Log("No request object found " + id);
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
        }
        
        public static AsyncLoadHandler GetObject(string id, Action<UnityEngine.Sprite> callback)
        {
            var handler = AsyncLoadHandler.CreateHandler();
            if (!AllSprites.TryGetValue(id, out var oneObjectSprite))
            {
                oneObjectSprite = new OneObjectSprite();
                oneObjectSprite.AddCallback(handler, callback);
                AllSprites.Add(id, oneObjectSprite);
                
                LibraryMethods.Models.balancyDataObjectLoad(id, DataObjectLoaded);
            }
            else
            {
                if (oneObjectSprite.Loaded)
                {
                    handler.Finish();
                    callback?.Invoke(oneObjectSprite.Sprite);
                }
                else
                {
                    oneObjectSprite.AddCallback(handler, callback);
                }
            }

            return handler;
        }
        
        internal static void ClearFromMemory(string id)
        {
            if (AllSprites.TryGetValue(id, out var oneObjectSprite))
            {
                if (oneObjectSprite.Sprite != null)
                {
                    Object.Destroy(oneObjectSprite.Sprite.texture);
                    Object.Destroy(oneObjectSprite.Sprite);
                }
                AllSprites.Remove(id);
            }
        }
        
        internal static void ClearFromDisk(string id)
        {
            LibraryMethods.Models.balancyDataObjectDeleteFromDisk(id);
            ClearFromMemory(id);
        }
    }
}
