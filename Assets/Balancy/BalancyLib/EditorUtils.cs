using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Balancy.Models;
using UnityEngine;

namespace Balancy.Editor
{
    public class EditorUtils
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public class ConfigStatus {
            public bool IsAuthorized;
            [MarshalAs(UnmanagedType.LPStr)]
            public string Email;
        }

        public class GameInfo
        {
            public readonly string GameName;
            public readonly string GameId;
            public readonly string PublicKey;

            public GameInfo(string name, string id, string key)
            {
                GameName = name;
                GameId = id;
                PublicKey = key;
            }
        }
        
        private static string _statusString;
        private static ConfigStatus _status;
        private static Action<ConfigStatus> _authCallback;
        private static Action<List<GameInfo>> getGamesCallback;
        private static bool _isInitialized = false;
        
        private static DownloadCompleteCallback _downloadCompleteCallback;
        private static ProgressUpdateCallback _progressUpdateCallback;

        public static void Launch()
        {
            if (_isInitialized)
                return;

            _isInitialized = true;
            LibraryMethods.General.balancySetLogCallback(LogMessage);
            UnityFileManager.Init();
            
            LibraryMethods.Editor.balancyConfigLaunch(LibraryMethods.Editor.Language.CSharp);
            UpdateStatus();
        }

        private static void UpdateStatus()
        {
            SetStatus(LibraryMethods.Editor.balancyConfigGetStatus());
        }

        private static ConfigStatus SetStatus(IntPtr pt)
        {
            _status = Marshal.PtrToStructure<ConfigStatus>(pt);
            if (_status != null)
                _statusString = _status.Email;
            
            return _status;
        }

        public static ConfigStatus GetStatus()
        {
            Launch();
            return _status;
        }

        public static void Auth(string email, string password, Action<ConfigStatus> callback)
        {
            Launch();
            _authCallback = callback;
            LibraryMethods.Editor.balancyConfigAuth(email, password, AuthDone);
        }
        
        private static void AuthDone(IntPtr pointer)
        {
            try
            {
                Debug.LogError("AuthDone!!");
                
                // var authStatus = SetStatus(pointer);
                // _authCallback?.Invoke(authStatus);
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
        }

        #region Get Games

        public static void LoadGames(Action<List<GameInfo>> callback)
        {
            getGamesCallback = callback;
            LibraryMethods.Editor.balancyConfigLoadListOfGames(GamesLoaded);
        }

        private static void GamesLoaded(IntPtr ptr, int size)
        {
            try
            {
                var result = new List<GameInfo>();
                var strs = JsonBasedObject.ReadStringArrayValues(ptr, size);

                for (int i = 0; i < size; i += 3)
                    result.Add(new GameInfo(strs[i], strs[i + 1], strs[i + 2]));

                getGamesCallback?.Invoke(result);
            }
            catch (Exception e)
            {
                Debug.LogError(":>> " + e);
            }
        }

        public static string GetSelectedGameId()
        {
            var ptr = LibraryMethods.Editor.balancyConfigGetSelectedGame();
            return Marshal.PtrToStringAnsi(ptr);
        }

        public static void SetSelectedGameId(string gameId)
        {
            LibraryMethods.Editor.balancyConfigSetSelectedGame(gameId);
        }
        
        #endregion

        public static void DownloadContent(Constants.Environment environment, DownloadCompleteCallback onReadyCallback, ProgressUpdateCallback onProgressCallback)
        {
            Launch();
            _downloadCompleteCallback = onReadyCallback;
            _progressUpdateCallback = onProgressCallback;
            LibraryMethods.Editor.balancyConfigDownloadContentToResources(environment, OnDownloadCompleted, OnProgressUpdate);
        }
        
        public static void GenerateCode(Constants.Environment environment, DownloadCompleteCallback onReadyCallback)
        {
            Launch();
            _downloadCompleteCallback = onReadyCallback;
            LibraryMethods.Editor.balancyConfigGenerateCode(environment, OnDownloadCompleted);
        }

        private static void OnDownloadCompleted(bool success, string message)
        {
            try
            {
                _downloadCompleteCallback?.Invoke(success, message);
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
        }

        private static void OnProgressUpdate(string fileName, float progress)
        {
            try
            {
                _progressUpdateCallback?.Invoke(fileName, progress);
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
        }

        public static void SignOut()
        {
            Launch();
            LibraryMethods.Editor.balancyConfigSignOut();
            UpdateStatus();
        }

        public static void Close()
        {
            LibraryMethods.Editor.balancyConfigClose();
        }
        
        private enum Level {
            Off,
            Verbose,
            Debug,
            Info,
            Warn,
            Error
        };
        
        [AOT.MonoPInvokeCallback(typeof(LibraryMethods.General.LogCallback))]
        private static void LogMessage(int level, string message)
        {
            switch ((Level)level)
            {
                case Level.Error:
                    Debug.LogError(message);
                    break;
                case Level.Warn:
                    Debug.LogWarning(message);
                    break;
                default:
                    Debug.Log(message);
                    break;
            }
        }
    }
}
