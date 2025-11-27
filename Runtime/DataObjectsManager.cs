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
        private static string RESOURCES_PATH;

        enum Status
        {
            None = 0,
            Loading = 1,
            Loaded = 2
        }
        
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

        internal static void Init(string cachePath, string resourcesPath)
        {
            CACHE_PATH = Path.Combine(cachePath, "Balancy/Models/");
            RESOURCES_PATH = resourcesPath;
            _mainThreadInstance = UnityMainThreadDispatcher.Instance();
        }

        private static UnityMainThreadDispatcher _mainThreadInstance; 

        private abstract class OneObjectBase
        {
            protected SharedObjectInfo _objectInfo = null;
            public Status Status = Status.None;
            
            protected string PathInStorage => CACHE_PATH + _objectInfo?.LocationPath;
            protected string PathInResources => RESOURCES_PATH + _objectInfo?.LocationPath;

            public void ProcessLoadedObject(SharedObjectInfo objectInfo)
            {
                _objectInfo = objectInfo;
                if (objectInfo != null)
                {
                    OnObjectLoaded();
                }
                else
                {
                    OnObjectLoadFailed();
                }
            }

            protected abstract void OnObjectLoaded();
            protected abstract void OnObjectLoadFailed();
        }

        private class OneObjectSprite : OneObjectBase
        {
            private class CallbackInfo
            {
                public Action<UnityEngine.Sprite> Callback;
                public AsyncLoadHandler LoadHandler;
            }

            private readonly List<CallbackInfo> _callbacks = new List<CallbackInfo>();
            public Sprite Sprite;

            protected override void OnObjectLoaded()
            {
                Texture2D texture = TryToLoadTextureFromResources();

                if (texture == null)
                {
                    var path = PathInStorage;
                    if (!File.Exists(path))
                    {
                        path = PathInResources;
                        if (!File.Exists(path))
                        {
                            if (File.Exists(PathInResources))
                                Debug.LogError("NO FILE PATH " + path);
                            SetSprite(null);
                            return;
                        }
                    }
                    byte[] bytes = File.ReadAllBytes(path);
                    texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                    texture.LoadImage(bytes);
                }

                Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), 
                    new Vector2(0.5f, 0.5f), _objectInfo.PixelsPerUnit, 0, 
                    SpriteMeshType.FullRect, 
                    new Vector4(_objectInfo.OffsetLeft, _objectInfo.OffsetBottom, _objectInfo.OffsetRight, _objectInfo.OffsetTop));
                SetSprite(sprite);
            }

            protected override void OnObjectLoadFailed()
            {
                SetSprite(null);
            }

            private Texture2D TryToLoadTextureFromResources()
            {
                var resourcesPath = _objectInfo.LocationPath.Replace('/', '-');
                string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(resourcesPath);
                return Resources.Load<Texture2D>(fileNameWithoutExtension);
            }
            
            private void SetSprite(Sprite sprite)
            {
                Sprite = sprite;
                Status = Sprite != null ? Status.Loaded : Status.None;

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

        private class OneObjectPath : OneObjectBase
        {
            private class CallbackInfo
            {
                public Action<string> Callback;
                public AsyncLoadHandler LoadHandler;
            }

            private readonly List<CallbackInfo> _callbacks = new List<CallbackInfo>();
            public string FilePath;

            protected override void OnObjectLoaded()
            {
                if (File.Exists(PathInStorage))
                    SetPath(PathInStorage);
                else
                {
                    if (File.Exists(PathInResources))
                        SetPath(PathInResources);
                    else
                        SetPath(null);
                }
            }

            protected override void OnObjectLoadFailed()
            {
                SetPath(null);
            }

            private void SetPath(string path)
            {
                FilePath = path;
                Status = !string.IsNullOrEmpty(FilePath) ? Status.Loaded : Status.None;

                foreach (var info in _callbacks)
                {
                    if (info.LoadHandler.GetStatus() == AsyncLoadHandler.Status.Loading)
                    {
                        info.LoadHandler.Finish();
                        info.Callback?.Invoke(FilePath);
                    }
                }
                
                _callbacks.Clear();
            }

            public void AddCallback(AsyncLoadHandler handler, Action<string> callback)
            {
                var info = new CallbackInfo
                {
                    LoadHandler = handler,
                    Callback = callback
                };
                _callbacks.Add(info);
            }
        }
        
        private class OneObjectView
        {
            private class CallbackInfo
            {
                public Action<string> Callback;
                public AsyncLoadHandler LoadHandler;
            }

            public bool Loaded = false;
            private readonly List<CallbackInfo> _callbacks = new List<CallbackInfo>();
            private string Path;

            public string PathInStorage => Path;

            public void SetPath(string path)
            {
                Path = path;
                Loaded = true;

                foreach (var info in _callbacks)
                {
                    if (info.LoadHandler.GetStatus() == AsyncLoadHandler.Status.Loading)
                    {
                        info.LoadHandler.Finish();
                        info.Callback?.Invoke(PathInStorage);
                    }
                }
                
                _callbacks.Clear();
            }

            public void AddCallback(AsyncLoadHandler handler, Action<string> callback)
            {
                var info = new CallbackInfo
                {
                    LoadHandler = handler,
                    Callback = callback
                };
                _callbacks.Add(info);
            }
        }

        private static readonly Dictionary<string, OneObjectBase> AllObjects = new Dictionary<string, OneObjectBase>();
        private static readonly Dictionary<string, OneObjectView> AllViews = new Dictionary<string, OneObjectView>();
        
        [AOT.MonoPInvokeCallback(typeof(LibraryMethods.Models.DataObjectWasCachedCallback))]
        private static void DataObjectLoaded(string id, IntPtr ptr)
        {
            try
            {
                if (ptr == IntPtr.Zero)
                {
                    if (AllObjects.TryGetValue(id, out var oneObject))
                    {
                        _mainThreadInstance.Enqueue(() => { oneObject.ProcessLoadedObject(null); });
                    } else
                        Debug.Log("No request object found " + id);
                }
                else
                {
                    if (AllObjects.TryGetValue(id, out var oneObject))
                    {
                        var sharedObject = Marshal.PtrToStructure<SharedObjectInfo>(ptr);
                        _mainThreadInstance.Enqueue(() => { oneObject.ProcessLoadedObject(sharedObject); });
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
        
        public static AsyncLoadHandler GetSprite(string id, Action<UnityEngine.Sprite> callback)
        {
            var handler = AsyncLoadHandler.CreateHandler();
            if (!AllObjects.TryGetValue(id, out var oneObject))
            {
                var oneObjectSprite = new OneObjectSprite();
                oneObjectSprite.AddCallback(handler, callback);
                AllObjects.Add(id, oneObjectSprite);
                oneObject = oneObjectSprite;
            }
            else
            {
                if (oneObject is OneObjectSprite sprite)
                {
                    if (sprite.Status == Status.Loaded)
                    {
                        handler.Finish();
                        callback?.Invoke(sprite.Sprite);
                    }
                    else
                    {
                        sprite.AddCallback(handler, callback);
                    }
                }
                else
                {
                    Debug.LogError($"Object {id} is not a sprite type");
                    handler.Finish();
                    callback?.Invoke(null);
                    return handler;
                }
            }

            if (oneObject.Status == Status.None && !handler.IsFinished())
            {
                oneObject.Status = Status.Loading;
                LibraryMethods.Models.balancyDataObjectLoad(id, DataObjectLoaded);
            }

            return handler;
        }

        public static AsyncLoadHandler GetObject(string id, Action<string> callback)
        {
            var handler = AsyncLoadHandler.CreateHandler();
            if (!AllObjects.TryGetValue(id, out var oneObject))
            {
                var oneObjectPath = new OneObjectPath();
                oneObjectPath.AddCallback(handler, callback);
                AllObjects.Add(id, oneObjectPath);
                oneObject = oneObjectPath;
            }
            else
            {
                if (oneObject is OneObjectPath pathObject)
                {
                    if (pathObject.Status == Status.Loaded)
                    {
                        handler.Finish();
                        callback?.Invoke(pathObject.FilePath);
                    }
                    else
                    {
                        pathObject.AddCallback(handler, callback);
                    }
                }
                else
                {
                    Debug.LogError($"Object {id} is not a path type");
                    handler.Finish();
                    callback?.Invoke(null);
                    return handler;
                }
            }

            if (oneObject.Status == Status.None && !handler.IsFinished())
            {
                oneObject.Status = Status.Loading;
                LibraryMethods.Models.balancyDataObjectLoad(id, DataObjectLoaded);
            }

            return handler;
        }
        
        [AOT.MonoPInvokeCallback(typeof(LibraryMethods.Models.DataObjectWasCachedCallback))]
        private static void DataObjectViewLoaded(string id, string path)
        {
            if (AllViews.TryGetValue(id, out var oneObjectView))
            {
                if (string.IsNullOrEmpty(path))
                    AllViews.Remove(id);
                _mainThreadInstance.Enqueue(() => { oneObjectView.SetPath(path); });
            }
            else
                Debug.Log("No request object view found " + id);
        }
        
        public static AsyncLoadHandler GetObjectView(string id, Action<string> callback)
        {
            var handler = AsyncLoadHandler.CreateHandler();
            if (!AllViews.TryGetValue(id, out var oneObjectView))
            {
                oneObjectView = new OneObjectView();
                oneObjectView.AddCallback(handler, callback);
                AllViews.Add(id, oneObjectView);
                
                LibraryMethods.Models.balancyDataObjectViewPreload(id, DataObjectViewLoaded);
            }
            else
            {
                if (oneObjectView.Loaded)
                {
                    handler.Finish();
                    callback?.Invoke(oneObjectView.PathInStorage);
                }
                else
                {
                    oneObjectView.AddCallback(handler, callback);
                }
            }

            return handler;
        }
        
        internal static void ClearFromMemory(string id)
        {
            if (AllObjects.TryGetValue(id, out var oneObject))
            {
                if (oneObject is OneObjectSprite sprite && sprite.Sprite != null)
                {
                    Object.Destroy(sprite.Sprite.texture);
                    Object.Destroy(sprite.Sprite);
                }
                AllObjects.Remove(id);
            }
        }
        
        internal static void ClearFromDisk(string id)
        {
            LibraryMethods.Models.balancyDataObjectDeleteFromDisk(id);
            ClearFromMemory(id);
        }
    }
}
