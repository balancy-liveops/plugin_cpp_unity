using System;
using System.Collections.Generic;
using UnityEngine;

namespace Balancy
{
    public class AuthTestPanel : MonoBehaviour
    {
        // ===== Inspector toggles — which auth methods to show =====
        [Header("Auth Methods")]
        public bool enableAuthByName = true;
        public bool enableAuthByEmail = true;
        public bool enableApple = true;
        public bool enableGoogle = true;
        public bool enableFacebook = true;

        [Header("Link Methods")]
        public bool enableLinkByName = true;
        public bool enableLinkByEmail = true;

        [Header("Other")]
        public bool enableGetInfo = true;
        public bool enableSignOut = true;

        // ===== State =====
        enum ViewState { SignedOut, Anonymous, Linked }

        ViewState _state = ViewState.SignedOut;
        string _userId;
        string _deviceId;
        readonly List<Network> _networks = new List<Network>();

        struct Network
        {
            public string kind;
            public string value;
        }

        // ===== Modal =====
        enum ModalType { None, AuthName, AuthEmail, LinkName, LinkEmail }

        ModalType _modal = ModalType.None;
        string _modalField1 = "";
        string _modalField2 = "";
        bool _modalForceLink = true;
        string _modalError = "";
        bool _modalErrorIsSuccess;
        bool _modalLoading;

        // ===== Toast =====
        string _toastMessage = "";
        bool _toastIsSuccess;
        float _toastEndTime;

        // ===== Show login/switch section in anonymous/linked state =====
        bool _showLoginSection;
        bool _showSwitchSection;

        // ===== Log =====
        string _log = "";
        Vector2 _logScrollPos;

        // ===== Scroll =====
        Vector2 _mainScrollPos;

        // ===== Styling =====
        GUIStyle _titleStyle;
        GUIStyle _subtitleStyle;
        GUIStyle _sectionHeaderStyle;
        GUIStyle _methodLabelStyle;
        GUIStyle _methodValueStyle;
        GUIStyle _linkedBadgeStyle;
        GUIStyle _warningStyle;
        GUIStyle _userIdStyle;
        GUIStyle _toastStyle;
        GUIStyle _modalErrorStyle;
        GUIStyle _linkStyle;
        bool _stylesInitialized;

        // ===== Colors matching the HTML theme =====
        static readonly Color ColorPrimary = new Color(0.424f, 0.361f, 0.906f);     // #6C5CE7
        static readonly Color ColorSuccess = new Color(0f, 0.722f, 0.58f);          // #00b894
        static readonly Color ColorDanger = new Color(0.906f, 0.298f, 0.235f);      // #e74c3c
        static readonly Color ColorWarning = new Color(0.71f, 0.478f, 0.082f);      // #b57a15
        static readonly Color ColorTextDim = new Color(0.7f, 0.745f, 0.765f);       // #b2bec3
        static readonly Color ColorBg = new Color(0.973f, 0.976f, 0.988f);          // #f8f9fc
        static readonly Color ColorCard = Color.white;

        void OnEnable()
        {
            Callbacks.OnSignedOut += OnSignedOut;
            RefreshState();
        }

        void OnDisable()
        {
            Callbacks.OnSignedOut -= OnSignedOut;
        }

        void OnSignedOut()
        {
            Log("OnSignedOut callback fired");
            _userId = null;
            _networks.Clear();
            _deviceId = null;
            UpdateState();
        }

        void Log(string message)
        {
            Debug.Log("[AuthPanel] " + message);
            _log += $"[{DateTime.Now:HH:mm:ss}] {message}\n";
        }

        void RefreshState()
        {
            if (!Main.IsReadyToUse)
            {
                _state = ViewState.SignedOut;
                return;
            }

            API.Auth.GetInfo(data =>
            {
                if (data.Success)
                {
                    _userId = data.UserId;
                    _networks.Clear();
                    ParseNetworks(data.NetworksJson);
                    var profile = Profiles.System;
                    _deviceId = profile?.GeneralInfo?.DeviceId;
                    Log($"GetInfo: userId={_userId} networks={data.NetworksJson}");
                }
                else
                {
                    Log($"GetInfo failed: {data.ErrorMessage}");
                }
                UpdateState();
            });
        }

        void ParseNetworks(string json)
        {
            _networks.Clear();
            if (string.IsNullOrEmpty(json)) return;

            // Simple JSON array parser: [{"kind":"x","value":"y"}, ...]
            try
            {
                json = json.Trim();
                if (!json.StartsWith("[")) return;
                json = json.Substring(1, json.Length - 2); // strip [ ]
                if (string.IsNullOrEmpty(json.Trim())) return;

                // Split by },{ but handle nested objects
                int depth = 0;
                int start = 0;
                for (int i = 0; i < json.Length; i++)
                {
                    if (json[i] == '{') depth++;
                    else if (json[i] == '}') depth--;
                    else if (json[i] == ',' && depth == 0)
                    {
                        ParseSingleNetwork(json.Substring(start, i - start));
                        start = i + 1;
                    }
                }
                ParseSingleNetwork(json.Substring(start));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AuthPanel] Failed to parse networks: {e.Message}");
            }
        }

        void ParseSingleNetwork(string obj)
        {
            obj = obj.Trim();
            if (!obj.StartsWith("{")) return;

            string kind = ExtractJsonValue(obj, "kind");
            string value = ExtractJsonValue(obj, "value");
            if (!string.IsNullOrEmpty(kind))
                _networks.Add(new Network { kind = kind, value = value ?? "" });
        }

        static string ExtractJsonValue(string json, string key)
        {
            string search = "\"" + key + "\"";
            int idx = json.IndexOf(search, StringComparison.Ordinal);
            if (idx < 0) return null;
            idx = json.IndexOf(':', idx + search.Length);
            if (idx < 0) return null;
            idx++;
            while (idx < json.Length && json[idx] == ' ') idx++;
            if (idx >= json.Length) return null;
            if (json[idx] == '"')
            {
                int end = json.IndexOf('"', idx + 1);
                if (end < 0) return null;
                return json.Substring(idx + 1, end - idx - 1);
            }
            return null;
        }

        void UpdateState()
        {
            if (string.IsNullOrEmpty(_userId))
                _state = ViewState.SignedOut;
            else if (_networks.Count == 0)
                _state = ViewState.Anonymous;
            else
                _state = ViewState.Linked;
        }

        Network? GetNetwork(string kind)
        {
            foreach (var n in _networks)
                if (n.kind == kind) return n;
            return null;
        }

        bool HasAnyAuthMethod()
        {
            return enableApple || enableGoogle || enableFacebook
                || enableAuthByName || enableAuthByEmail;
        }

        // ===== Styles =====

        void InitStyles()
        {
            if (_stylesInitialized) return;
            _stylesInitialized = true;

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter
            };
            _subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.39f, 0.43f, 0.45f) }
            };
            _sectionHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11, fontStyle = FontStyle.Bold,
                normal = { textColor = ColorTextDim }
            };
            _methodLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14, fontStyle = FontStyle.Bold
            };
            _methodValueStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                normal = { textColor = new Color(0.39f, 0.43f, 0.45f) }
            };
            _linkedBadgeStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11, fontStyle = FontStyle.Bold,
                normal = { textColor = ColorSuccess },
                alignment = TextAnchor.MiddleRight
            };
            _warningStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 13, alignment = TextAnchor.MiddleLeft, wordWrap = true,
                normal = { textColor = ColorWarning },
                padding = new RectOffset(10, 10, 8, 8)
            };
            _userIdStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12, wordWrap = true,
                normal = { textColor = new Color(0.39f, 0.43f, 0.45f) },
                padding = new RectOffset(8, 8, 6, 6)
            };
            _toastStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(16, 16, 8, 8)
            };
            _modalErrorStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 13, alignment = TextAnchor.MiddleLeft, wordWrap = true,
                padding = new RectOffset(10, 10, 8, 8)
            };
            _linkStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = ColorPrimary },
                fontStyle = FontStyle.Bold
            };
        }

        // ===== Main OnGUI =====

        void OnGUI()
        {
            InitStyles();

            float panelW = Mathf.Min(480, Screen.width - 40);
            float panelX = (Screen.width - panelW) / 2f;
            float panelY = 20;

            GUI.skin.label.richText = true;
            GUI.skin.button.fontSize = 14;
            GUI.skin.textField.fontSize = 14;
            GUI.skin.toggle.fontSize = 13;

            // Modal overlay — draw on top and block all input behind it
            if (_modal != ModalType.None)
            {
                DrawModal();
                return;
            }

            // SDK status
            if (!Main.IsReadyToUse)
            {
                GUI.Label(new Rect(panelX, panelY, panelW, 30), "<color=red>SDK Not Ready</color>");
                panelY += 35;
                DrawSettingsToggles(panelX, ref panelY, panelW);
                DrawLog(panelX, panelW);
                return;
            }

            // Settings toggles at top
            DrawSettingsToggles(panelX, ref panelY, panelW);

            // Header
            GUI.Label(new Rect(panelX, panelY, panelW, 30), "Account", _titleStyle);
            panelY += 30;

            string subtitle = _state == ViewState.SignedOut ? "Sign in to continue"
                : _state == ViewState.Anonymous ? "Secure your progress"
                : "Manage your account";
            GUI.Label(new Rect(panelX, panelY, panelW, 20), subtitle, _subtitleStyle);
            panelY += 28;

            // Scrollable main content
            float scrollH = Screen.height - panelY - 180;
            float contentH = EstimateContentHeight(panelW);

            _mainScrollPos = GUI.BeginScrollView(
                new Rect(panelX, panelY, panelW, scrollH),
                _mainScrollPos,
                new Rect(0, 0, panelW - 20, contentH)
            );

            float sy = 0;

            switch (_state)
            {
                case ViewState.SignedOut:
                    DrawSignedOut(ref sy, panelW - 20);
                    break;
                case ViewState.Anonymous:
                    DrawAnonymous(ref sy, panelW - 20);
                    break;
                case ViewState.Linked:
                    DrawLinked(ref sy, panelW - 20);
                    break;
            }

            GUI.EndScrollView();

            // Toast
            DrawToast(panelX, panelW);

            // Log
            DrawLog(panelX, panelW);
        }

        void DrawSettingsToggles(float x, ref float y, float w)
        {
            GUI.Label(new Rect(x, y, w, 18), "SETTINGS", _sectionHeaderStyle ?? GUI.skin.label);
            y += 20;

            float toggleW = w / 3f;
            float toggleH = 20;

            enableAuthByName = GUI.Toggle(new Rect(x, y, toggleW, toggleH), enableAuthByName, " Name Auth");
            enableAuthByEmail = GUI.Toggle(new Rect(x + toggleW, y, toggleW, toggleH), enableAuthByEmail, " Email Auth");
            enableApple = GUI.Toggle(new Rect(x + toggleW * 2, y, toggleW, toggleH), enableApple, " Apple");
            y += toggleH + 2;

            enableGoogle = GUI.Toggle(new Rect(x, y, toggleW, toggleH), enableGoogle, " Google");
            enableFacebook = GUI.Toggle(new Rect(x + toggleW, y, toggleW, toggleH), enableFacebook, " Facebook");
            enableGetInfo = GUI.Toggle(new Rect(x + toggleW * 2, y, toggleW, toggleH), enableGetInfo, " Get Info");
            y += toggleH + 2;

            enableLinkByName = GUI.Toggle(new Rect(x, y, toggleW, toggleH), enableLinkByName, " Name Link");
            enableLinkByEmail = GUI.Toggle(new Rect(x + toggleW, y, toggleW, toggleH), enableLinkByEmail, " Email Link");
            enableSignOut = GUI.Toggle(new Rect(x + toggleW * 2, y, toggleW, toggleH), enableSignOut, " Sign Out");
            y += toggleH + 4;

            // Refresh button
            if (GUI.Button(new Rect(x, y, 120, 25), "Refresh State"))
                RefreshState();
            y += 32;
        }

        // ===== State A: Signed Out =====

        void DrawSignedOut(ref float sy, float w)
        {
            // Continue as Guest
            if (GUI.Button(new Rect(0, sy, w, 40), "Continue as Guest"))
                DoContinueAsGuest();
            sy += 48;

            // Divider
            GUI.Label(new Rect(0, sy, w, 20), "--- or sign in ---", _subtitleStyle ?? GUI.skin.label);
            sy += 28;

            // Auth methods table
            DrawMethodsTable(ref sy, w, "auth");
        }

        // ===== State B: Anonymous =====

        void DrawAnonymous(ref float sy, float w)
        {
            // Warning banner
            GUI.Box(new Rect(0, sy, w, 50), "");
            GUI.Label(new Rect(8, sy + 4, w - 16, 44),
                "<color=#8a6d1b><b>Your progress is not saved</b>\nLink an account to keep your progress safe across devices.</color>");
            sy += 58;

            // Link methods table
            DrawMethodsTable(ref sy, w, "link");

            // "Already have an account? Log in"
            if (HasAnyAuthMethod())
            {
                sy += 8;
                if (GUI.Button(new Rect(0, sy, w, 25),
                    _showLoginSection ? "Hide login options" : "Already have an account? Log in",
                    _linkStyle ?? GUI.skin.label))
                {
                    _showLoginSection = !_showLoginSection;
                }
                sy += 28;

                if (_showLoginSection)
                    DrawMethodsTable(ref sy, w, "auth");
            }
        }

        // ===== State C: Linked =====

        void DrawLinked(ref float sy, float w)
        {
            // Account info
            if (enableGetInfo)
            {
                GUI.Box(new Rect(0, sy, w, 60), "");
                GUI.Label(new Rect(8, sy + 6, w - 16, 18),
                    "<color=#00b894><b>Account linked</b></color>");
                GUI.Label(new Rect(8, sy + 28, w - 16, 24), _userId ?? "", _userIdStyle ?? GUI.skin.label);
                sy += 66;
            }

            // Manage methods table
            DrawMethodsTable(ref sy, w, "manage");

            // "Have another account? Switch"
            if (HasAnyAuthMethod())
            {
                sy += 8;
                if (GUI.Button(new Rect(0, sy, w, 25),
                    _showSwitchSection ? "Hide switch options" : "Have another account? Switch",
                    _linkStyle ?? GUI.skin.label))
                {
                    _showSwitchSection = !_showSwitchSection;
                }
                sy += 28;

                if (_showSwitchSection)
                    DrawMethodsTable(ref sy, w, "auth");
            }

            // Sign Out
            if (enableSignOut)
            {
                sy += 8;
                var oldColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(1f, 0.85f, 0.85f);
                if (GUI.Button(new Rect(0, sy, w, 40), "Sign Out"))
                    DoSignOut();
                GUI.backgroundColor = oldColor;
                sy += 48;
            }
        }

        // ===== Methods Table =====

        void DrawMethodsTable(ref float sy, float w, string mode)
        {
            // Header
            string title = mode == "link" ? "LINK YOUR ACCOUNT"
                : mode == "auth" ? "SIGN IN WITH"
                : "LINKED ACCOUNTS";

            GUI.Label(new Rect(0, sy, w, 18), title, _sectionHeaderStyle ?? GUI.skin.label);
            sy += 22;

            // Methods
            DrawMethodRow(ref sy, w, mode, "device", "Device", false);
            DrawMethodRow(ref sy, w, mode, "apple", "Apple", true);
            DrawMethodRow(ref sy, w, mode, "google", "Google", true);
            DrawMethodRow(ref sy, w, mode, "facebook", "Facebook", true);
            DrawMethodRow(ref sy, w, mode, "name", "Username", false);
            DrawMethodRow(ref sy, w, mode, "email", "Email", false);

            sy += 6;
        }

        bool IsMethodVisible(string key, string mode)
        {
            if (key == "device")
                return mode == "link" || mode == "manage";
            if (key == "apple") return enableApple;
            if (key == "google") return enableGoogle;
            if (key == "facebook") return enableFacebook;
            if (key == "name")
                return mode == "auth" ? enableAuthByName : enableLinkByName;
            if (key == "email")
                return mode == "auth" ? enableAuthByEmail : enableLinkByEmail;
            return false;
        }

        void DrawMethodRow(ref float sy, float w, string mode, string key, string label, bool isProvider)
        {
            if (!IsMethodVisible(key, mode)) return;

            float rowH = 36;
            float labelW = 100;
            float valueW = w - labelW - 160;
            float btnW = 70;
            float unlinkW = 60;

            var network = GetNetwork(key);
            bool isLinked = network.HasValue;
            if (key == "device" && !string.IsNullOrEmpty(_userId))
                isLinked = true;

            // Background
            GUI.Box(new Rect(0, sy, w, rowH), "");

            // Label
            GUI.Label(new Rect(8, sy + 2, labelW, rowH - 4), label, _methodLabelStyle ?? GUI.skin.label);

            // Value
            string valueText;
            if (isLinked && network.HasValue)
                valueText = network.Value.value;
            else if (isLinked && key == "device")
                valueText = _deviceId ?? _userId ?? "";
            else
                valueText = "Not linked";

            Color origColor = GUI.contentColor;
            if (!isLinked) GUI.contentColor = ColorTextDim;
            GUI.Label(new Rect(labelW + 8, sy + 2, valueW, rowH - 4), valueText, _methodValueStyle ?? GUI.skin.label);
            GUI.contentColor = origColor;

            // Action buttons
            float btnX = w - btnW - unlinkW - 16;

            if (mode == "auth")
            {
                if (key != "device")
                {
                    if (GUI.Button(new Rect(btnX, sy + 4, btnW + unlinkW + 8, rowH - 8), "Sign in"))
                    {
                        if (isProvider)
                            DoAuthByProvider(key);
                        else if (key == "name")
                            OpenModal(ModalType.AuthName);
                        else if (key == "email")
                            OpenModal(ModalType.AuthEmail);
                    }
                }
            }
            else if (mode == "link")
            {
                if (key == "device")
                {
                    GUI.Label(new Rect(btnX, sy + 2, btnW + unlinkW + 8, rowH - 4),
                        "Linked", _linkedBadgeStyle ?? GUI.skin.label);
                }
                else
                {
                    if (GUI.Button(new Rect(btnX, sy + 4, btnW + unlinkW + 8, rowH - 8), "Link"))
                    {
                        if (isProvider)
                            DoLinkByProvider(key);
                        else if (key == "name")
                            OpenModal(ModalType.LinkName);
                        else if (key == "email")
                            OpenModal(ModalType.LinkEmail);
                    }
                }
            }
            else if (mode == "manage")
            {
                if (isLinked)
                {
                    GUI.Label(new Rect(btnX, sy + 2, btnW, rowH - 4),
                        "Linked", _linkedBadgeStyle ?? GUI.skin.label);

                    bool canUnlink = key != "device" && network.HasValue;
                    if (canUnlink)
                    {
                        if (GUI.Button(new Rect(btnX + btnW + 4, sy + 6, unlinkW, rowH - 12), "Unlink"))
                            DoUnlink(key, network.Value.value);
                    }
                }
                else
                {
                    if (GUI.Button(new Rect(btnX, sy + 4, btnW + unlinkW + 8, rowH - 8), "Link"))
                    {
                        if (isProvider)
                            DoLinkByProvider(key);
                        else if (key == "name")
                            OpenModal(ModalType.LinkName);
                        else if (key == "email")
                            OpenModal(ModalType.LinkEmail);
                    }
                }
            }

            sy += rowH + 2;
        }

        // ===== Modal =====

        void OpenModal(ModalType type)
        {
            _modal = type;
            _modalField1 = "";
            _modalField2 = "";
            _modalForceLink = true;
            _modalError = "";
            _modalLoading = false;
        }

        void CloseModal()
        {
            _modal = ModalType.None;
            _modalError = "";
            _modalLoading = false;
        }

        void DrawModal()
        {
            // Dim overlay
            GUI.color = new Color(0, 0, 0, 0.5f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float modalW = Mathf.Min(400, Screen.width - 60);
            float modalH = _modal == ModalType.LinkName || _modal == ModalType.LinkEmail ? 310 : 270;
            float mx = (Screen.width - modalW) / 2f;
            float my = (Screen.height - modalH) / 2f;

            // Background
            GUI.Box(new Rect(mx, my, modalW, modalH), "");

            float cy = my + 16;
            float cw = modalW - 32;
            float cx = mx + 16;

            // Title
            string title, field1Label, field1Placeholder, submitLabel;
            bool isLink = _modal == ModalType.LinkName || _modal == ModalType.LinkEmail;
            bool isName = _modal == ModalType.AuthName || _modal == ModalType.LinkName;

            if (_modal == ModalType.AuthName)
            {
                title = _state == ViewState.Anonymous ? "Log In with Username" : "Sign In with Username";
                field1Label = "Username";
                field1Placeholder = "Enter username";
                submitLabel = "Log In";
            }
            else if (_modal == ModalType.AuthEmail)
            {
                title = _state == ViewState.Anonymous ? "Log In with Email" : "Sign In with Email";
                field1Label = "Email";
                field1Placeholder = "Enter email";
                submitLabel = "Log In";
            }
            else if (_modal == ModalType.LinkName)
            {
                title = "Link Username";
                field1Label = "Username";
                field1Placeholder = "Choose username";
                submitLabel = "Link Username";
            }
            else
            {
                title = "Link Email";
                field1Label = "Email";
                field1Placeholder = "Enter email";
                submitLabel = "Link Email";
            }

            GUI.Label(new Rect(cx, cy, cw, 24), $"<b><size=18>{title}</size></b>");
            cy += 28;

            // Warning for auth in anonymous mode
            if (!isLink && _state == ViewState.Anonymous)
            {
                Color oldBg = GUI.backgroundColor;
                GUI.backgroundColor = new Color(1f, 0.92f, 0.7f);
                GUI.Box(new Rect(cx, cy, cw, 26), "Your current progress will be replaced.", _warningStyle ?? GUI.skin.box);
                GUI.backgroundColor = oldBg;
                cy += 32;
                modalH += 32;
            }

            // Field 1
            GUI.Label(new Rect(cx, cy, cw, 18), field1Label.ToUpperInvariant(), _sectionHeaderStyle ?? GUI.skin.label);
            cy += 20;
            _modalField1 = GUI.TextField(new Rect(cx, cy, cw, 28), _modalField1);
            cy += 34;

            // Field 2: Password
            GUI.Label(new Rect(cx, cy, cw, 18), "PASSWORD", _sectionHeaderStyle ?? GUI.skin.label);
            cy += 20;
            _modalField2 = GUI.PasswordField(new Rect(cx, cy, cw, 28), _modalField2, '*');
            cy += 34;

            // Force link toggle
            if (isLink)
            {
                _modalForceLink = GUI.Toggle(new Rect(cx, cy, cw, 22), _modalForceLink, " Force link (override existing)");
                cy += 28;
            }

            // Error/success message
            if (!string.IsNullOrEmpty(_modalError))
            {
                Color oldBg = GUI.backgroundColor;
                GUI.backgroundColor = _modalErrorIsSuccess ? new Color(0.7f, 1f, 0.85f) : new Color(1f, 0.85f, 0.85f);
                GUI.Box(new Rect(cx, cy, cw, 28), _modalError, _modalErrorStyle ?? GUI.skin.box);
                GUI.backgroundColor = oldBg;
                cy += 34;
            }

            // Buttons
            float btnH = 36;
            float halfW = (cw - 10) / 2f;

            GUI.enabled = !_modalLoading;
            if (GUI.Button(new Rect(cx, cy, halfW, btnH), _modalLoading ? "Loading..." : submitLabel))
                SubmitModal();
            GUI.enabled = true;

            if (GUI.Button(new Rect(cx + halfW + 10, cy, halfW, btnH), "Cancel"))
                CloseModal();
        }

        void SubmitModal()
        {
            switch (_modal)
            {
                case ModalType.AuthName:
                    DoAuthByName(_modalField1, _modalField2);
                    break;
                case ModalType.AuthEmail:
                    DoAuthByEmail(_modalField1, _modalField2);
                    break;
                case ModalType.LinkName:
                    DoLinkByName(_modalField1, _modalField2, _modalForceLink);
                    break;
                case ModalType.LinkEmail:
                    DoLinkByEmail(_modalField1, _modalField2, _modalForceLink);
                    break;
            }
        }

        // ===== Auth/Link operations =====

        void DoAuthByName(string name, string password)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(password))
            {
                _modalError = "Please enter username and password";
                _modalErrorIsSuccess = false;
                return;
            }

            _modalLoading = true;
            _modalError = "";
            Log($"Auth.WithNameAndPassword({name}, ***)");

            API.Auth.WithNameAndPassword(name, password, data =>
            {
                _modalLoading = false;
                if (data.Success)
                {
                    _modalError = "Signed in as " + data.UserId;
                    _modalErrorIsSuccess = true;
                    Log($"Auth Name: success userId={data.UserId}");
                    CloseModal();
                    RefreshState();
                    ShowToast(true, "Signed in with Username");
                }
                else
                {
                    _modalError = data.ErrorMessage ?? "Auth failed";
                    _modalErrorIsSuccess = false;
                    Log($"Auth Name: failed error={data.ErrorMessage}");
                }
            });
        }

        void DoAuthByEmail(string email, string password)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                _modalError = "Please enter email and password";
                _modalErrorIsSuccess = false;
                return;
            }

            _modalLoading = true;
            _modalError = "";
            Log($"Auth.WithEmailAndPassword({email}, ***)");

            API.Auth.WithEmailAndPassword(email, password, data =>
            {
                _modalLoading = false;
                if (data.Success)
                {
                    _modalError = "Signed in as " + data.UserId;
                    _modalErrorIsSuccess = true;
                    Log($"Auth Email: success userId={data.UserId}");
                    CloseModal();
                    RefreshState();
                    ShowToast(true, "Signed in with Email");
                }
                else
                {
                    _modalError = data.ErrorMessage ?? "Auth failed";
                    _modalErrorIsSuccess = false;
                    Log($"Auth Email: failed error={data.ErrorMessage}");
                }
            });
        }

        void DoLinkByName(string name, string password, bool forceLink)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(password))
            {
                _modalError = "Please enter username and password";
                _modalErrorIsSuccess = false;
                return;
            }

            _modalLoading = true;
            _modalError = "";
            Log($"Link.WithNameAndPassword({name}, ***, forceLink={forceLink})");

            API.Link.WithNameAndPassword(name, password, forceLink, data =>
            {
                _modalLoading = false;
                if (data.Success)
                {
                    _modalError = "Username linked successfully";
                    _modalErrorIsSuccess = true;
                    Log($"Link Name: success userId={data.UserId}");
                    CloseModal();
                    RefreshState();
                    ShowToast(true, "Username linked");
                }
                else
                {
                    _modalError = data.ErrorMessage ?? "Link failed";
                    _modalErrorIsSuccess = false;
                    Log($"Link Name: failed error={data.ErrorMessage}");
                }
            });
        }

        void DoLinkByEmail(string email, string password, bool forceLink)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                _modalError = "Please enter email and password";
                _modalErrorIsSuccess = false;
                return;
            }

            _modalLoading = true;
            _modalError = "";
            Log($"Link.WithEmailAndPassword({email}, ***, forceLink={forceLink})");

            API.Link.WithEmailAndPassword(email, password, forceLink, data =>
            {
                _modalLoading = false;
                if (data.Success)
                {
                    _modalError = "Email linked successfully";
                    _modalErrorIsSuccess = true;
                    Log($"Link Email: success userId={data.UserId}");
                    CloseModal();
                    RefreshState();
                    ShowToast(true, "Email linked");
                }
                else
                {
                    _modalError = data.ErrorMessage ?? "Link failed";
                    _modalErrorIsSuccess = false;
                    Log($"Link Email: failed error={data.ErrorMessage}");
                }
            });
        }

        void DoAuthByProvider(string provider)
        {
            Log($"Auth.With{Capitalize(provider)}() — provider auth requires native OAuth token");
            ShowToast(false, $"{Capitalize(provider)} auth requires native OAuth — provide userId+token via API.Auth.With{Capitalize(provider)}()");
        }

        void DoLinkByProvider(string provider)
        {
            Log($"Link.With{Capitalize(provider)}() — provider link requires native OAuth token");
            ShowToast(false, $"{Capitalize(provider)} link requires native OAuth — provide userId+token via API.Link.With{Capitalize(provider)}()");
        }

        void DoUnlink(string kind, string value)
        {
            Log($"Unlink({kind}, {value})");
            if (kind == "name")
            {
                API.Auth.UnlinkName(value, data =>
                {
                    Log($"UnlinkName: success={data.Success} error={data.ErrorMessage}");
                    if (data.Success)
                    {
                        ShowToast(true, "Unlinked successfully");
                        RefreshState();
                    }
                    else
                        ShowToast(false, data.ErrorMessage ?? "Unlink failed");
                });
            }
            else if (kind == "email")
            {
                API.Auth.UnlinkEmail(value, data =>
                {
                    Log($"UnlinkEmail: success={data.Success} error={data.ErrorMessage}");
                    if (data.Success)
                    {
                        ShowToast(true, "Unlinked successfully");
                        RefreshState();
                    }
                    else
                        ShowToast(false, data.ErrorMessage ?? "Unlink failed");
                });
            }
            else
            {
                // Provider unlink — not yet available via public API, log for now
                Log($"Provider unlink for '{kind}' is not yet exposed in the Unity API");
                ShowToast(false, $"Provider unlink for '{kind}' not yet available");
            }
        }

        void DoSignOut()
        {
            Log("Auth.SignOut()");
            API.Auth.SignOut(data =>
            {
                Log($"SignOut: success={data.Success} error={data.ErrorMessage}");
                if (data.Success)
                {
                    _userId = null;
                    _networks.Clear();
                    _deviceId = null;
                    UpdateState();
                    ShowToast(true, "Signed out successfully");
                }
                else
                    ShowToast(false, data.ErrorMessage ?? "Sign out failed");
            });
        }

        void DoContinueAsGuest()
        {
            Log("Continuing as guest (re-init with device auth)");
            // Guest = re-auth with device ID. The SDK auto-auths as guest on init,
            // so we just refresh state to pick up the current userId.
            RefreshState();
            ShowToast(true, "Continuing as guest");
        }

        // ===== Toast =====

        void ShowToast(bool success, string message)
        {
            _toastMessage = message;
            _toastIsSuccess = success;
            _toastEndTime = Time.realtimeSinceStartup + 3f;
            Log($"[Toast] {(success ? "OK" : "ERR")}: {message}");
        }

        void DrawToast(float panelX, float panelW)
        {
            if (string.IsNullOrEmpty(_toastMessage)) return;
            if (Time.realtimeSinceStartup > _toastEndTime)
            {
                _toastMessage = "";
                return;
            }

            float toastW = Mathf.Min(panelW, 400);
            float toastX = (Screen.width - toastW) / 2f;
            float toastY = Screen.height - 200;

            Color oldBg = GUI.backgroundColor;
            GUI.backgroundColor = _toastIsSuccess ? ColorSuccess : ColorDanger;
            Color oldContent = GUI.contentColor;
            GUI.contentColor = Color.white;
            GUI.Box(new Rect(toastX, toastY, toastW, 30), _toastMessage, _toastStyle ?? GUI.skin.box);
            GUI.contentColor = oldContent;
            GUI.backgroundColor = oldBg;
        }

        // ===== Log =====

        void DrawLog(float panelX, float panelW)
        {
            float logY = Screen.height - 160;
            GUI.Label(new Rect(panelX, logY, 100, 20), "<b>Log:</b>");
            if (GUI.Button(new Rect(panelX + 110, logY, 60, 20), "Clear"))
                _log = "";
            logY += 24;

            float logH = 130;
            float contentH = Mathf.Max(logH, _log.Split('\n').Length * 16);
            _logScrollPos = GUI.BeginScrollView(
                new Rect(panelX, logY, panelW, logH),
                _logScrollPos,
                new Rect(0, 0, panelW - 20, contentH)
            );

            var logStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true };
            GUI.Label(new Rect(0, 0, panelW - 20, contentH), _log, logStyle);

            GUI.EndScrollView();

            if (_log.Length > 0)
                _logScrollPos.y = float.MaxValue;
        }

        // ===== Helpers =====

        float EstimateContentHeight(float w)
        {
            // Rough estimate to size the scroll content
            float h = 200; // base
            int methodCount = 0;
            if (enableApple) methodCount++;
            if (enableGoogle) methodCount++;
            if (enableFacebook) methodCount++;
            if (enableAuthByName || enableLinkByName) methodCount++;
            if (enableAuthByEmail || enableLinkByEmail) methodCount++;
            methodCount++; // device
            h += methodCount * 40;

            if (_state == ViewState.Anonymous && _showLoginSection)
                h += methodCount * 40 + 50;
            if (_state == ViewState.Linked && _showSwitchSection)
                h += methodCount * 40 + 50;
            if (_state == ViewState.Linked && enableGetInfo) h += 70;
            if (_state == ViewState.Linked && enableSignOut) h += 50;

            return Mathf.Max(h, 400);
        }

        static string Capitalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return char.ToUpper(s[0]) + s.Substring(1);
        }
    }
}
