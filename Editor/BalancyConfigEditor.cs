using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Balancy.Editor
{
    [ExecuteInEditMode]
    public class BalancyConfigEditor : EditorWindow
    {
        // Static method to reset the cleanup flag
        public static void ResetCleanupFlag()
        {
            _alreadyCleanedUp = false;
        }
        
        // Static method to close all open Balancy Config windows and clean up resources
        // This can be called from anywhere, including before play mode starts
        private static bool _alreadyCleanedUp = false;
        public static void CloseAllWindowsAndCleanup()
        {
            // Only perform cleanup once to avoid multiple cleanup operations
            if (_alreadyCleanedUp)
                return;
                
            _alreadyCleanedUp = true;
            
            // Find all open BalancyConfigEditor windows
            BalancyConfigEditor[] windows = Resources.FindObjectsOfTypeAll<BalancyConfigEditor>();
            
            // Close each window and perform cleanup
            foreach (var window in windows)
            {
                if (window != null)
                {
                    try
                    {
                        // Ensure we don't call OnDestroy (which would call EditorUtils.Close() again)
                        window.skipOnDestroyCleanup = true;
                        
                        // Close the window
                        window.Close();
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"Error closing Balancy Config window: {e.Message}");
                    }
                }
            }
            
            try
            {
                // Perform any additional cleanup needed
                EditorUtils.Close();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error during EditorUtils.Close(): {e.Message}");
            }
        }
        
        [MenuItem("Tools/Balancy/Config", false, -104002)]
        public static void ShowWindow()
        {
            // Don't allow opening the window if in play mode
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Cannot open Balancy Config window while in Play mode. Stop the game first.");
                return;
            }
            
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

        private EditorDispatcher m_EditorDispatcher;

        private class GamesInfo
        {
            public readonly List<EditorUtils.GameInfo> Games;
            public readonly string[] GameNames;
            public string SelectedGameId;
            
            private int _selectedGame = -1;
            private int _selectedBranchOrder = -1;
            
            private List<EditorUtils.BranchInfo> SelectedGameBranches;
            public string[] BranchNames;
            public int SelectedBranchId;

            public void SetBranches(List<EditorUtils.BranchInfo> branches, int selectedBranch)
            {
                SelectedGameBranches = branches;
                
                BranchNames = new string[SelectedGameBranches.Count];
                for (int i = 0; i < SelectedGameBranches.Count; i++)
                {
                    BranchNames[i] = SelectedGameBranches[i].BranchName;
                    if (SelectedGameBranches[i].BranchId == selectedBranch)
                        _selectedBranchOrder = i;
                }
            }

            public bool HasBranches => GetBranches?.Count > 0;
            
            public List<EditorUtils.BranchInfo> GetBranches => SelectedGameBranches;

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

            public EditorUtils.GameInfo GetSelectedGameInfo()
            {
                for (int i = 0; i < Games.Count; i++)
                {
                    if (Games[i].GameId == SelectedGameId)
                        return Games[i];
                }

                return null;
            }
            
            public void Render()
            {
                EditorGUI.BeginChangeCheck();
                _selectedGame = EditorGUILayout.Popup("Selected Game: ", _selectedGame, GameNames);

                if (EditorGUI.EndChangeCheck())
                    if (_selectedGame != -1)
                        SelectGameByIndex(_selectedGame);
            }
            
            public void RenderBranches()
            {
                EditorGUI.BeginChangeCheck();
                _selectedBranchOrder = EditorGUILayout.Popup("Selected Branch: ", _selectedBranchOrder, BranchNames);

                if (EditorGUI.EndChangeCheck())
                    if (_selectedBranchOrder != -1)
                        SelectBranchByIndex(_selectedBranchOrder);
            }

            public bool HasSelectedGame()
            {
                return _selectedGame >= 0;
            }

            private void SelectGameByIndex(int index)
            {
                SelectedGameId = Games[index].GameId;
                EditorUtils.SetSelectedGameId(SelectedGameId);
                SelectedGameBranches = null;
            }
            
            private void SelectBranchByIndex(int index)
            {
                SelectedBranchId = SelectedGameBranches[index].BranchId;
                EditorUtils.SetSelectedBranchId(SelectedBranchId);
            }
        }

        private GamesInfo _gamesInfo;
        private string _errorMessage;
        private bool _downloading;
        private float _downloadingProgress;
        private string _downloadingFileName;
        
        // Flag to control whether OnDestroy should clean up resources
        private bool skipOnDestroyCleanup = false;

        private void Awake()
        {
            // Prevent opening the window if in play mode
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Cannot open Balancy Config window while in Play mode. Window will be closed.");
                EditorApplication.delayCall += Close;
                return;
            }
            
            minSize = new Vector2(500, 500);
            EditorUtils.Launch();
            _userEmail = UserEmail;
            
            m_EditorDispatcher = new EditorDispatcher();
            m_EditorDispatcher.StartEditorDispatcher();
        }

        private void OnDestroy()
        {
            // Skip cleanup if we're already handling it elsewhere (e.g., when entering Play mode)
            if (!skipOnDestroyCleanup)
            {
                EditorUtils.Close();
            }
            
            m_EditorDispatcher?.StopEditorDispatcher();
        }

        private void OnGUI()
        {
            // Close immediately if in play mode
            if (EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Balancy Config cannot be used in Play mode.", MessageType.Warning);
                EditorGUILayout.Space();
                if (GUILayout.Button("Close Window"))
                {
                    Close();
                }
                return;
            }
            
            Render();
            EditorGUILayout.Space();
            RenderLoader();
        }
        
        public void Render()
        {
            GUILayout.BeginVertical(EditorStyles.helpBox);
            RenderAuth();
            RenderGames();
            RenderBranches();
            RenderActions();
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
                if (GUILayout.Button("Generate Code"))
                    StartCodeGeneration();

                if (GUILayout.Button("Download Data"))
                    StartDownloading();
                
                // if (GUILayout.Button("Synch Addressables"))
                //     StartSynchingAddressables();
                
                GUILayout.EndHorizontal();
            }

            GUILayout.EndVertical();
            GUI.enabled = true;
        }
        
        private void StartCodeGeneration()
        {
            _downloading = true;
            _downloadingProgress = 0.5f;

            EditorUtils.GenerateCode(OnDownloadCompleted);
        }
        
        private void StartDownloading()
        {
            _downloading = true;
            _downloadingProgress = 0;

            EditorUtils.DownloadContent(OnDownloadCompleted, OnProgressUpdate);
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
                        m_EditorDispatcher.Enqueue(Repaint);
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
        
        private bool _loadingBranches;
        private void RenderBranches()
        {
            if (!IsAuthorized)
                return;

            if (!_loadingGames && !_loadingBranches && _gamesInfo != null)
            {
                if (_gamesInfo.HasBranches)
                {
                    _gamesInfo.RenderBranches();
                }
                else
                {
                    _loadingBranches = true;
                    EditorUtils.LoadBranches((branches) =>
                    {
                        try
                        {
                            var selectedBranch = EditorUtils.GetSelectedBranchId();
                            _gamesInfo.SetBranches(branches, selectedBranch);
                            _loadingBranches = false;
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

        private void RenderActions()
        {
            if (!IsAuthorized || _gamesInfo == null || !_gamesInfo.HasSelectedGame() || _downloading || EditorApplication.isCompiling)
                return;
                
            // Find BalancyLauncher in the current scene
            BalancyLauncher launcher = UnityEngine.Object.FindAnyObjectByType<BalancyLauncher>();
            
            GUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            var gameInfo = _gamesInfo.GetSelectedGameInfo();
            string gameName = gameInfo?.GameName;
            string gameId = gameInfo?.GameId;
            string publicKey = gameInfo?.PublicKey;
            
            // Get the branch name if branches are loaded
            string branchName = "";
            if (_gamesInfo.HasBranches && _gamesInfo.GetBranches != null && _gamesInfo.GetBranches.Count > 0)
            {
                // Find the branch with the selected branch ID
                foreach (var branch in _gamesInfo.GetBranches)
                {
                    if (branch.BranchId == _gamesInfo.SelectedBranchId)
                    {
                        branchName = branch.BranchName;
                        break;
                    }
                }
            }
                
            if (launcher != null)
            {
                // BalancyLauncher exists in the scene
                if (GUILayout.Button($"Sync with \"{gameName}\" branch \"{branchName}\""))
                {
                    // Update existing BalancyLauncher properties
                    Undo.RecordObject(launcher, "Update BalancyLauncher");
                    launcher.SetGameId(gameId);
                    launcher.SetPublicKey(publicKey);
                    launcher.SetBranchName(branchName);
                    EditorUtility.SetDirty(launcher);
                    EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                }

                GUI.enabled = true;
            }
            else
            {
                // No BalancyLauncher in the scene
                if (GUILayout.Button($"Create BalancyLauncher for \"{gameName}\""))
                {
                    // Create a new GameObject with BalancyLauncher component
                    GameObject newObject = new GameObject("BalancyLauncher");
                    BalancyLauncher newLauncher = newObject.AddComponent<BalancyLauncher>();
                    
                    // Set properties
                    newLauncher.SetGameId(gameId);
                    newLauncher.SetPublicKey(publicKey);
                    newLauncher.SetBranchName("");
                    
                    // Select the new GameObject in the hierarchy
                    Selection.activeGameObject = newObject;
                    
                    // Mark the scene as dirty to ensure changes are saved
                    EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                }
                
                GUI.enabled = false;
            }
            
            if (GUILayout.Button($"Add Balancy UI"))
            {
                var existing = GameObject.Find("BalancyUI");
                if (existing != null)
                {
                    EditorUtility.DisplayDialog("Balancy UI Exists",
                        "An instance of 'BalancyUI' is already present in the scene.",
                        "OK");
                }
                else
                {
                    var prefab = PrefabFinder.FindPrefabByName("BalancyUI");
                    if (prefab)
                        PrefabUtility.InstantiatePrefab(prefab);
                }
            }
            
            GUI.enabled = true;
            
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(10);
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
            // Check if we entered play mode and close the window if we did
            if (EditorApplication.isPlaying && this != null)
            {
                // Use the static method that properly cleans up all resources
                CloseAllWindowsAndCleanup();
                return;
            }
            
            if (!_needRefresh)
                return;

            _needRefresh = false;
            AssetDatabase.Refresh();
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
