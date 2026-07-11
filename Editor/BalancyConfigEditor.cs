using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
                
                // Sort branches alphabetically by BranchName
                SelectedGameBranches.Sort((a, b) => string.Compare(a.BranchName, b.BranchName, StringComparison.OrdinalIgnoreCase));
                
                _selectedBranchOrder = -1;
                BranchNames = new string[SelectedGameBranches.Count];
                for (int i = 0; i < SelectedGameBranches.Count; i++)
                {
                    BranchNames[i] = SelectedGameBranches[i].BranchName;
                    if (SelectedGameBranches[i].BranchId == selectedBranch)
                        _selectedBranchOrder = i;
                } 
                SelectedBranchId = selectedBranch;
            }

            public bool HasBranches => GetBranches?.Count > 0;
            
            public List<EditorUtils.BranchInfo> GetBranches => SelectedGameBranches;

            public GamesInfo(List<EditorUtils.GameInfo> list, string selectedGameId)
            {
                Games = list;
                SelectedGameId = selectedGameId;
                
                // Sort games alphabetically by GameName
                Games.Sort((a, b) => string.Compare(a.GameName, b.GameName, StringComparison.OrdinalIgnoreCase));
                
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
            
            public bool Render()
            {
                EditorGUI.BeginChangeCheck();
                _selectedGame = EditorGUILayout.Popup("Selected Game: ", _selectedGame, GameNames);

                if (EditorGUI.EndChangeCheck())
                    if (_selectedGame != -1)
                    {
                        SelectGameByIndex(_selectedGame);
                        return true;
                    }

                return false;
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
            GUI.enabled = !_downloading && _gamesInfo != null && _gamesInfo.HasSelectedGame() && HasValidBranchSelected() && !EditorApplication.isCompiling;
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

                if (GUILayout.Button("Sync Addressables"))
                    StartSynchingAddressables();

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

        private void StartSynchingAddressables()
        {
            if (_gamesInfo == null || !_gamesInfo.HasSelectedGame() || !HasValidBranchSelected())
            {
                EditorUtility.DisplayDialog("Error", "Please select a game and branch first", "OK");
                return;
            }

            var gameInfo = _gamesInfo.GetSelectedGameInfo();
            string gameId = gameInfo?.GameId;

            // Get selected branch info
            string branchName = "";
            int branchId = -1;
            if (_gamesInfo.HasBranches && _gamesInfo.GetBranches != null)
            {
                foreach (var branch in _gamesInfo.GetBranches)
                {
                    if (branch.BranchId == _gamesInfo.SelectedBranchId)
                    {
                        branchName = branch.BranchName;
                        branchId = branch.BranchId;
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(branchName) || branchId < 0)
            {
                EditorUtility.DisplayDialog("Error", "Invalid branch selection", "OK");
                return;
            }

            // Create a minimal EditorAuth wrapper
            var editorAuth = new Balancy_EditorAuth();

            _downloading = true;
            _downloadingProgress = 0;
            _downloadingFileName = "Initializing addressables sync...";

            // Trigger the addressables sync event
            Balancy_Editor.TriggerSynchAddressables(
                editorAuth,
                gameId,
                "", // token - not used in new implementation
                branchId,
                branchName,
                OnAddressablesProgress,
                OnAddressablesStart,
                OnAddressablesComplete
            );
        }

        private void OnAddressablesStart()
        {
            _downloadingFileName = "Starting addressables sync...";
            _downloadingProgress = 0.1f;
            _needRefresh = true;
        }

        private void OnAddressablesProgress(string fileName, float progress)
        {
            _downloadingFileName = fileName;
            _downloadingProgress = progress;
            _needRefresh = true;
        }

        private void OnAddressablesComplete(string message)
        {
            _downloading = false;
            _downloadingFileName = "";

            if (string.IsNullOrEmpty(message))
            {
                EditorUtility.DisplayDialog("Success", "Addressables synchronized successfully!", "OK");
            }
            else
            {
                _errorMessage = message;
            }

            _needRefresh = true;
        }

        private void OnDownloadCompleted(bool success, string message)
        {
            if (!success)
                _errorMessage = message;
            else
            {
                // PrepareSprites();
                CreateBalancyFilesManifest();
            }
            _downloading = false;
            _needRefresh = true;
        }

        private void CreateBalancyFilesManifest()
        {
            try
            {
                string balancyPath = Path.Combine(Application.streamingAssetsPath, "Balancy");
                
                if (!Directory.Exists(balancyPath))
                {
                    Debug.LogWarning("Balancy folder not found in StreamingAssets, skipping manifest creation.");
                    return;
                }

                string manifestPath = Path.Combine(balancyPath, "balancy_files_manifest.txt");
                
                var files = Directory.GetFiles(balancyPath, "*", SearchOption.AllDirectories)
                    .Where(f => !f.EndsWith(".meta") && !Path.GetFileName(f).StartsWith("."))
                    .Select(f => "./" + f.Substring(balancyPath.Length + 1).Replace('\\', '/'))
                    .OrderBy(f => f)
                    .ToList();

                File.WriteAllLines(manifestPath, files);
                
                Debug.Log($"Created Balancy files manifest with {files.Count} files at: {manifestPath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to create Balancy files manifest: {e.Message}");
            }
        }

        private void PrepareSprites()
        {
            string resourcesPath = Application.dataPath + "/Balancy/Resources/";

            if (Directory.Exists(resourcesPath))
            {
                // Get all image files from file system
                string[] imageExtensions = { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.tga" };
                var imageFiles = new List<string>();

                foreach (string extension in imageExtensions)
                {
                    imageFiles.AddRange(Directory.GetFiles(resourcesPath, extension, SearchOption.AllDirectories));
                }

                // Import each file
                foreach (string filePath in imageFiles)
                {
                    // Convert absolute path to relative Unity path
                    string relativePath = "Assets" + filePath.Substring(Application.dataPath.Length);
                    relativePath = relativePath.Replace('\\', '/'); // Ensure forward slashes

                    // Import the asset
                    AssetDatabase.ImportAsset(relativePath, ImportAssetOptions.ForceUpdate);

                    // Configure texture import settings
                    TextureImporter textureImporter = AssetImporter.GetAtPath(relativePath) as TextureImporter;
                    if (textureImporter != null)
                    {
                        textureImporter.textureType = TextureImporterType.Sprite;
                        textureImporter.mipmapEnabled = false;
                        textureImporter.isReadable = true; // Essential for base64!
                        textureImporter.textureCompression =
                            TextureImporterCompression.Uncompressed; // Avoid compression issues

                        // Re-import with new settings
                        AssetDatabase.ImportAsset(relativePath, ImportAssetOptions.ForceUpdate);
                    }
                }

                // Final refresh
                AssetDatabase.Refresh();
            }
        }

        private void OnProgressUpdate(string fileName, float progress)
        {
            _downloadingFileName = fileName;
            _downloadingProgress = progress;
            _needRefresh = true;
        }

        private bool IsAuthorized => EditorUtils.GetStatus()?.IsAuthorized ?? false;
        private string UserEmail => EditorUtils.GetStatus()?.Email;
        
        private bool HasValidBranchSelected()
        {
            if (_gamesInfo == null || !_gamesInfo.HasBranches || _gamesInfo.GetBranches == null || _gamesInfo.GetBranches.Count == 0)
                return false;
                
            // Check if we can find a branch with the selected branch ID
            foreach (var branch in _gamesInfo.GetBranches)
            {
                if (branch.BranchId == _gamesInfo.SelectedBranchId)
                {
                    return true;
                }
            }
            return false;
        }
        
        private string _userEmail = "";
        private string _userPassword = "";
        private string _authErrorMessage = "";
        private bool _showAuthError = false;
        private bool _isAuthenticating = false;
        
        private void PerformAuthorization()
        {
            _isAuthenticating = true;
            _showAuthError = false;
            _authErrorMessage = "";
            
            EditorUtils.Auth(_userEmail, _userPassword, status =>
            {
                _isAuthenticating = false;
                
                if (!status.IsAuthorized)
                {
                    _authErrorMessage = "Failed to auth, no connection or wrong password";
                    _showAuthError = true;
                }
                else
                {
                    // Clear any existing error message on successful authentication
                    _showAuthError = false;
                    _authErrorMessage = "";
                }
                m_EditorDispatcher?.Enqueue(Repaint);
            });
        }
        
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
                
                // Disable UI during authentication
                GUI.enabled = !_isAuthenticating;
                
                // Track changes to email and password to clear error message
                EditorGUI.BeginChangeCheck();
                
                // Set keyboard control names for better focus handling
                GUI.SetNextControlName("EmailField");
                _userEmail = EditorGUILayout.TextField("Email", _userEmail);
                
                GUI.SetNextControlName("PasswordField");
                _userPassword = EditorGUILayout.PasswordField("Password", _userPassword);
                
                if (EditorGUI.EndChangeCheck())
                {
                    // Clear error message when user starts typing
                    _showAuthError = false;
                    _authErrorMessage = "";
                }
                
                // Show authentication error if it exists
                if (_showAuthError && !string.IsNullOrEmpty(_authErrorMessage))
                {
                    var originalColor = GUI.color;
                    GUI.color = Color.red;
                    EditorGUILayout.HelpBox(_authErrorMessage, MessageType.Error);
                    GUI.color = originalColor;
                }
                
                // Show loading animation during authentication
                if (_isAuthenticating)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    
                    var originalColor = GUI.color;
                    GUI.color = Color.yellow;
                    
                    // Simple loading animation using dots
                    string loadingText = "Authenticating";
                    int dotCount = Mathf.FloorToInt(Time.realtimeSinceStartup * 2) % 4;
                    for (int i = 0; i < dotCount; i++)
                        loadingText += ".";
                    
                    EditorGUILayout.LabelField(loadingText, EditorStyles.boldLabel);
                    GUI.color = originalColor;
                    
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                }
                
                GUILayout.BeginHorizontal();
                
                // Disable "Create Account" button during authentication
                GUI.enabled = !_isAuthenticating;
                if (GUILayout.Button("Click to create a new Account"))
                {
                    Application.OpenURL("https://balancy.dev/auth");
                }

                // Only enable Authorize button if fields are filled and not authenticating
                GUI.enabled = !_isAuthenticating && !string.IsNullOrEmpty(_userEmail) && !string.IsNullOrEmpty(_userPassword);
                
                string buttonText = _isAuthenticating ? "Authorizing..." : "Authorize";
                
                // Make the Authorize button the default button (responds to Enter)
                bool isDefaultButton = !_isAuthenticating && !string.IsNullOrEmpty(_userEmail) && !string.IsNullOrEmpty(_userPassword);
                
                if (isDefaultButton)
                {
                    // Check for Enter key globally when button can be activated
                    if (Event.current.type == EventType.KeyDown && 
                        (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter))
                    {
                        Event.current.Use();
                        PerformAuthorization();
                        GUIUtility.ExitGUI();
                    }
                }
                
                if (GUILayout.Button(buttonText))
                {
                    PerformAuthorization();
                }

                GUI.enabled = true;
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }
        }
        
        private bool _loadingGames;
        private bool _loadGamesFailed;
        private double _loadGamesStartTime;
        private const double LoadGamesTimeout = 15; // seconds

        private void RenderGames()
        {
            if (!IsAuthorized)
                return;

            if (_loadGamesFailed)
            {
                EditorGUILayout.HelpBox(
                    "Failed to load the list of games. Check your connection, retry, or sign out and log in again.",
                    MessageType.Warning);
                if (GUILayout.Button("Retry"))
                    _loadGamesFailed = false;
                return;
            }

            if (_gamesInfo == null)
            {
                // Keep the drawn controls identical between the Layout and Repaint
                // events: start the actual loading outside of OnGUI via delayCall.
                EditorGUILayout.LabelField("Loading games...");

                if (!_loadingGames)
                {
                    _loadingGames = true;
                    _loadGamesStartTime = EditorApplication.timeSinceStartup;
                    EditorApplication.delayCall += StartLoadingGames;
                }
                else if (Event.current.type == EventType.Repaint &&
                         EditorApplication.timeSinceStartup - _loadGamesStartTime > LoadGamesTimeout)
                {
                    // The native side never called us back (no connection, or the auth
                    // token is broken) — unstick the UI instead of loading forever.
                    // Only flip the state on Repaint, so the controls drawn in this
                    // pass stay consistent with its earlier Layout event.
                    _loadingGames = false;
                    _loadGamesFailed = true;
                    m_EditorDispatcher?.Enqueue(Repaint);
                }

                return;
            }

            if (_gamesInfo.Render())
            {
                m_EditorDispatcher?.Enqueue(Repaint);
                _needRefresh = true;
            }
        }

        private void StartLoadingGames()
        {
            EditorUtils.LoadGames((games) =>
            {
                try
                {
                    var selectedGame = EditorUtils.GetSelectedGameId();
                    _gamesInfo = new GamesInfo(games, selectedGame);
                    _loadingGames = false;
                    _needRefresh = true;
                    m_EditorDispatcher?.Enqueue(Repaint);
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                }
            });
        }
        
        private bool _loadingBranches;
        private void RenderBranches()
        {
            if (!IsAuthorized)
                return;

            if (!_loadingGames && !_loadingBranches && _gamesInfo != null && _gamesInfo.HasSelectedGame())
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
                            m_EditorDispatcher?.Enqueue(Repaint);
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
            
            // Check if a valid branch is selected
            if (string.IsNullOrEmpty(branchName))
            {
                GUILayout.Space(10);
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                
                var originalColor = GUI.color;
                GUI.color = Color.yellow;
                EditorGUILayout.HelpBox("Please select a branch", MessageType.Warning);
                GUI.color = originalColor;
                
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(10);
                return;
            }

            GUILayout.Space(10);
            GUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Project Setup", EditorStyles.boldLabel);
            GUILayout.Space(5);
            
            // Balancy Launcher Status
            RenderLauncherStatus(gameId, publicKey, branchName, gameName);
            
            GUILayout.Space(5);
            
            // Balancy UI Status
            RenderBalancyUIStatus();
            
            GUILayout.EndVertical();
            GUILayout.Space(10);
        }
        
        private void RenderLauncherStatus(string gameId, string publicKey, string branchName, string gameName)
        {
            BalancyLauncher launcher = UnityEngine.Object.FindAnyObjectByType<BalancyLauncher>();
            
            GUILayout.BeginHorizontal();
            
            // Status icon and label
            if (launcher != null)
            {
                // Launcher exists - show checkmark
                var originalColor = GUI.color;
                GUI.color = Color.green;
                GUILayout.Label("✓", EditorStyles.boldLabel, GUILayout.Width(20));
                GUI.color = originalColor;
                
                GUILayout.Label("Launcher:", GUILayout.Width(70));
                
                // Show current branch connection
                // Get current branch using SerializedObject to access private field
                SerializedObject serializedLauncher = new SerializedObject(launcher);
                SerializedProperty apiGameIdProperty = serializedLauncher.FindProperty("apiGameId");
                SerializedProperty apiPublicKeyProperty = serializedLauncher.FindProperty("apiPublicKey");
                SerializedProperty branchProperty = serializedLauncher.FindProperty("branchName");
                
                string currentGameId = apiGameIdProperty?.stringValue ?? "";
                string currentPublicKey = apiPublicKeyProperty?.stringValue ?? "";

                if (currentGameId == gameId && currentPublicKey == publicKey)
                {
                    string currentBranch = branchProperty?.stringValue ?? "";
                    if (string.IsNullOrEmpty(currentBranch))
                    {
                        GUI.color = Color.yellow;
                        GUILayout.Label("Autodetect branch", EditorStyles.label);
                        GUI.color = originalColor;
                    }
                    else if (currentBranch == branchName)
                    {
                        GUI.color = Color.green;
                        GUILayout.Label($"Connects to '{currentBranch}'", EditorStyles.label);
                        GUI.color = originalColor;
                    }
                    else
                    {
                        GUI.color = Color.yellow;
                        GUILayout.Label($"Connects to '{currentBranch}' (different branch)", EditorStyles.label);
                        GUI.color = originalColor;
                    }

                    GUILayout.FlexibleSpace();

                    // Update button if branch is different or not set
                    if (currentBranch != branchName)
                    {
                        if (GUILayout.Button($"Force to '{branchName}'", GUILayout.Width(120)))
                        {
                            Undo.RecordObject(launcher, "Update BalancyLauncher");
                            launcher.SetGameId(gameId);
                            launcher.SetPublicKey(publicKey);
                            launcher.SetBranchName(branchName);
                            EditorUtility.SetDirty(launcher);
                            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                        }
                    }
                }
                else
                {
                    if (currentGameId != gameId)
                    {
                        GUI.color = Color.red;
                        GUILayout.Label("Different Game ID set!", EditorStyles.label);
                        GUI.color = originalColor;
                    }
                    else if (currentPublicKey != publicKey)
                    {
                        GUI.color = Color.red;
                        GUILayout.Label("Different Public Key set!", EditorStyles.label);
                        GUI.color = originalColor;
                    }
                    
                    if (GUILayout.Button("Apply Selected Game", GUILayout.Width(150)))
                    {
                        Undo.RecordObject(launcher, "Update BalancyLauncher");
                        launcher.SetGameId(gameId);
                        launcher.SetPublicKey(publicKey);
                        EditorUtility.SetDirty(launcher);
                        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                    }
                }

                if (GUILayout.Button("Select", GUILayout.Width(60)))
                {
                    Selection.activeGameObject = launcher.gameObject;
                    EditorGUIUtility.PingObject(launcher);
                }
            }
            else
            {
                // No launcher - show add option
                var originalColor = GUI.color;
                GUI.color = Color.gray;
                GUILayout.Label("○", EditorStyles.boldLabel, GUILayout.Width(20));
                GUI.color = originalColor;
                
                GUILayout.Label("Launcher:", GUILayout.Width(70));
                
                GUI.color = Color.gray;
                GUILayout.Label("Not found in scene", EditorStyles.label);
                GUI.color = originalColor;
                
                GUILayout.FlexibleSpace();
                
                if (GUILayout.Button("Add", GUILayout.Width(60)))
                {
                    // Create a new GameObject with BalancyLauncher component
                    GameObject newObject = new GameObject("BalancyLauncher");
                    BalancyLauncher newLauncher = newObject.AddComponent<BalancyLauncher>();
                    
                    // Set properties
                    newLauncher.SetGameId(gameId);
                    newLauncher.SetPublicKey(publicKey);
                    // newLauncher.SetBranchName(branchName);
                    
                    // Select the new GameObject in the hierarchy
                    Selection.activeGameObject = newObject;
                    
                    // Mark the scene as dirty to ensure changes are saved
                    EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                }
            }
            
            GUILayout.EndHorizontal();
        }
        
        private void RenderBalancyUIStatus()
        {
            GameObject existingUI = GameObject.Find("BalancyUI");
            
            GUILayout.BeginHorizontal();
            
            // Status icon and label
            if (existingUI != null)
            {
                // UI exists - show checkmark
                var originalColor = GUI.color;
                GUI.color = Color.green;
                GUILayout.Label("✓", EditorStyles.boldLabel, GUILayout.Width(20));
                GUI.color = originalColor;
                
                GUILayout.Label("Balancy UI:", GUILayout.Width(70));
                
                GUI.color = Color.green;
                GUILayout.Label("Present in scene", EditorStyles.label);
                GUI.color = originalColor;
                
                GUILayout.FlexibleSpace();
                
                // Optional: Add "Select" button to highlight the UI in hierarchy
                if (GUILayout.Button("Select", GUILayout.Width(60)))
                {
                    Selection.activeGameObject = existingUI;
                    EditorGUIUtility.PingObject(existingUI);
                }
            }
            else
            {
                // No UI - show add option
                var originalColor = GUI.color;
                GUI.color = Color.gray;
                GUILayout.Label("○", EditorStyles.boldLabel, GUILayout.Width(20));
                GUI.color = originalColor;
                
                GUILayout.Label("Balancy UI:", GUILayout.Width(70));
                
                GUI.color = Color.gray;
                GUILayout.Label("Not found in scene", EditorStyles.label);
                GUI.color = originalColor;
                
                GUILayout.FlexibleSpace();
                
                if (GUILayout.Button("Add", GUILayout.Width(60)))
                {
                    var prefab = PrefabFinder.FindPrefabByName("BalancyUI");
                    if (prefab)
                    {
                        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                        Selection.activeGameObject = instance;
                        EditorGUIUtility.PingObject(instance);
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Prefab Not Found",
                            "BalancyUI prefab could not be found. Please ensure it's imported correctly.",
                            "OK");
                    }
                }
            }
            
            GUILayout.EndHorizontal();
        }
        
        private void OnEnable()
        {
            EditorApplication.update += update;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }
        
        private void OnDisable()
        {
            EditorApplication.update -= update;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }
        
        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                CloseAllWindowsAndCleanup();
            }
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
            
            // Refresh during authentication to animate the loading dots
            if (_isAuthenticating)
            {
                Repaint();
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
            _gamesInfo = null;
            _loadingGames = false;
            _loadGamesFailed = false;
            _loadingBranches = false;
            Repaint();
        }
    }
}
