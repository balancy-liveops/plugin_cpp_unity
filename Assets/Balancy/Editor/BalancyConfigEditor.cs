using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Balancy.Editor
{
    [ExecuteInEditMode]
    public class BalancyConfigEditor : EditorWindow
    {
        [MenuItem("Tools/Balancy/Config", false, -104002)]
        public static void ShowWindow()
        {
            var window = GetWindow(typeof(BalancyConfigEditor));
            window.titleContent.text = "Balancy Config";
            window.titleContent.image = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Balancy/Editor/BalancyLogo.png");
        }
        
        [MenuItem("Tools/Balancy/Open PersistentDataPath ", false, -103000)]
        public static void OpenPersistentDataPath()
        {
            var path = Application.persistentDataPath;
            EditorUtility.RevealInFinder(path);
        }

        private static readonly GUILayoutOption LAYOUT_BUTTON_LOGOUT = GUILayout.Width(30);

        private class GamesInfo
        {
            public readonly List<EditorUtils.GameInfo> Games;
            public readonly string[] GameNames;
            public string SelectedGameId;
            
            private int _selectedGame = -1;

            public GamesInfo(List<EditorUtils.GameInfo> list, string selectedGameId)
            {
                Games = list;
                SelectedGameId = selectedGameId;
                
                GameNames = new string[Games.Count];
                for (int i = 0; i < Games.Count; i++)
                {
                    GameNames[i] = Games[i].GameName;
                    if (Games[i].GameId == SelectedGameId)
                        _selectedGame = i;
                }
            }
            
            public void Render()
            {
                EditorGUI.BeginChangeCheck();
                _selectedGame = EditorGUILayout.Popup("Selected Game: ", _selectedGame, GameNames);

                if (EditorGUI.EndChangeCheck())
                    if (_selectedGame != -1)
                        SelectGameByIndex(_selectedGame);
            }

            public bool HasSelectedGame()
            {
                return _selectedGame >= 0;
            }

            private void SelectGameByIndex(int index)
            {
                SelectedGameId = Games[index].GameId;
                EditorUtils.SetSelectedGameId(SelectedGameId);
            }
        }

        private GamesInfo _gamesInfo;
        private string _errorMessage;
        private bool _downloading;
        private float _downloadingProgress;
        private string _downloadingFileName;

        private void Awake()
        {
            Debug.LogError("**==>> Awake");
            minSize = new Vector2(500, 500);
            EditorUtils.Launch();
            _userEmail = UserEmail;
        }

        private void OnDestroy()
        {
            Debug.LogError("**==>> OnDestroy");
            EditorUtils.Close();
        }

        private void OnGUI()
        {
            Render();
            EditorGUILayout.Space();
            RenderLoader();
        }
        
        public void Render()
        {
            GUILayout.BeginVertical(EditorStyles.helpBox);
            RenderAuth();
            RenderGames();
            // RenderBranches();
            GUILayout.EndVertical();
        }
        
        private void RenderLoader()
        {
            //GUI.enabled = !_downloading && AuthHelper.HasSelectedBranch() && !EditorApplication.isCompiling;
            GUI.enabled = !_downloading && _gamesInfo != null && _gamesInfo.HasSelectedGame() && !EditorApplication.isCompiling;
            GUILayout.BeginVertical(EditorStyles.helpBox);

            GUILayout.Label("Content Management");
            if (_downloading)
            {
                GUI.enabled = true;
                var rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
                EditorGUI.ProgressBar(rect, _downloadingProgress, _downloadingFileName);
                GUI.enabled = false;
            }
            else
            {
                GUILayout.BeginHorizontal();
                // if (GUILayout.Button("Generate Code"))
                //     StartCodeGeneration();

                if (GUILayout.Button("Download Data"))
                    StartDownloading();
                
                // if (GUILayout.Button("Synch Addressables"))
                //     StartSynchingAddressables();
                
                GUILayout.EndHorizontal();
            }

            GUILayout.EndVertical();
            GUI.enabled = true;
        }
        
        private void StartDownloading()
        {
            _downloading = true;
            _downloadingProgress = 0;

            EditorUtils.DownloadContent(Constants.Environment.Development, OnDownloadCompleted, OnProgressUpdate);
        }

        private void OnDownloadCompleted(bool success, string message)
        {
            if (!success)
                _errorMessage = message;
            _downloading = false;
            _needRefresh = true;
        }

        private void OnProgressUpdate(string fileName, float progress)
        {
            _downloadingFileName = fileName;
            _downloadingProgress = progress;
            _needRefresh = true;
        }

        private bool IsAuthorized => EditorUtils.GetStatus()?.IsAuthorized ?? false;
        private string UserEmail => EditorUtils.GetStatus()?.Email;
        
        private string _userEmail = "";
        private string _userPassword = "";
        
        private void RenderAuth()
        {
            if (IsAuthorized)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Balancy User: ");

                var color = GUI.color;
                GUI.color = Color.green;
                GUILayout.Label(UserEmail);
                GUI.color = color;
                
                if (GUILayout.Button(EditorGUIUtility.IconContent("Toolbar Minus"), LAYOUT_BUTTON_LOGOUT))
                    SignOut();
                
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.BeginVertical(EditorStyles.helpBox);
                GUILayout.Label("Balancy User");
                _userEmail = EditorGUILayout.TextField("Email", _userEmail);
                _userPassword = EditorGUILayout.PasswordField("Password", _userPassword);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Click to create a new Account"))
                {
                    Application.OpenURL("https://balancy.dev/auth");
                }

                GUI.enabled = !string.IsNullOrEmpty(_userEmail) && !string.IsNullOrEmpty(_userPassword);
                if (GUILayout.Button("Authorize"))
                {
                    EditorUtils.Auth(_userEmail, _userPassword, status =>
                    {
                        Debug.LogError("Repaint");
                    });
                }

                GUI.enabled = true;
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }
        }
        
        private bool _loadingGames;
        private void RenderGames()
        {
            if (!IsAuthorized)
                return;

            if (!_loadingGames)
            {
                if (_gamesInfo != null)
                {
                    _gamesInfo.Render();
                }
                else
                {
                    _loadingGames = true;
                    EditorUtils.LoadGames((games) =>
                    {
                        try
                        {
                            var selectedGame = EditorUtils.GetSelectedGameId();
                            _gamesInfo = new GamesInfo(games, selectedGame);
                            _loadingGames = false;
                            _needRefresh = true;
                        }
                        catch (Exception e)
                        {
                            Debug.LogError(e);
                        }
                    });
                }
            }
        }
        
        private void OnEnable()
        {
            EditorApplication.update += update;
        }
        
        private void OnDisable()
        {
            EditorApplication.update -= update;
        }

        private bool _needRefresh = false;
        private void update()
        {
            if (!_needRefresh)
                return;

            _needRefresh = false;
            if (!string.IsNullOrEmpty(_errorMessage))
            {
                EditorUtility.DisplayDialog("Error", _errorMessage, "Ok");
                _errorMessage = null;
            }

            Repaint();
        }
        
        private void SignOut()
        {
            EditorUtils.SignOut();
            Repaint();
        }
    }
}
