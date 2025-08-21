# 🔧 Быстрая настройка и сборка Android AAR

## Проблема:
Сборка не может найти Android SDK и Unity Editor.

## ✅ Решение:

### 1. **Запустите автоматическую настройку:**
```bash
cd /Volumes/PavelData/Projects/plugin_cpp_unity/Assets/Balancy/WebView/WVAndroidLib~
chmod +x setup_and_build.sh
./setup_and_build.sh
```

### 2. **Или настройте вручную:**
```bash
# Найдите ваш Android SDK (обычно здесь)
export ANDROID_HOME="$HOME/Library/Android/sdk"

# Найдите вашу версию Unity (замените на вашу)
export UNITY_EDITOR_PATH="/Applications/Unity/Hub/Editor/2023.2.20f1/Unity.app/Contents"

# Запустите сборку
./build_android_aar.sh
```

### 3. **Если нет Android SDK:**
1. Установите Android Studio: https://developer.android.com/studio
2. Откройте Android Studio → Tools → SDK Manager
3. Установите Android 13 (API Level 33)

### 4. **Если нет Unity Android Build Support:**
1. Откройте Unity Hub
2. Installs → [Ваша версия Unity] → Add Modules  
3. Выберите Android Build Support (IL2CPP)

## 🎯 После успешной сборки:
- Файл `balancywebview.aar` появится в `../Plugins/Android/`
- Android WebView будет поддерживать методы show/hide
- Можно тестировать `RenderViewsManager.OpenView(url, owner, true)`

## 🔍 Проверка установки:
```bash
# Проверка Android SDK
ls "$HOME/Library/Android/sdk/platforms/android-33/android.jar"

# Проверка Unity
ls "/Applications/Unity/Hub/Editor/*/Unity.app/Contents/Editor/Data/PlaybackEngines/AndroidPlayer/Variations/il2cpp/Release/Classes/classes.jar"
```
