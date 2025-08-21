# 🚀 Balancy WebView Android - Standalone Build

## ✅ Решение проблемы Unity Dependencies

Ваша Android библиотека **НЕ ДОЛЖНА** зависеть от Unity! Теперь она работает как **полностью независимая Android библиотека**.

## 🔧 Что было исправлено:

### 1. **Убрана зависимость от Unity classes.jar**
- ❌ Старая версия: требовала установленный Unity + Android Build Support  
- ✅ Новая версия: работает только с Android SDK

### 2. **Unity интеграция через Reflection**
- Библиотека **автоматически определяет** наличие Unity в runtime
- Если Unity есть → использует `UnitySendMessage` 
- Если Unity нет → работает в standalone режиме
- **Никаких compile-time зависимостей от Unity!**

### 3. **Упрощенная сборка**
- Только Android SDK нужен
- Gradle сам скачает все необходимое
- Unity больше не нужен для сборки библиотеки

## 🚀 Как собрать:

### Быстрый способ:
```bash
cd /Volumes/PavelData/Projects/plugin_cpp_unity/Assets/Balancy/WebView/WVAndroidLib.androidlib
chmod +x run_build.sh
./run_build.sh
```

### Или вручную:
```bash
chmod +x build_standalone.sh
./build_standalone.sh
```

## 📋 Требования:

### ✅ Необходимо:
- **Android SDK** (через Android Studio)
- **Android API 30+** (любая версия от 30 до 35)
- **Gradle** (автоматически скачается если нет)

### ❌ НЕ нужно:
- ~~Unity Editor~~
- ~~Unity Android Build Support~~  
- ~~Unity classes.jar~~

## 🔍 Проверка установки Android SDK:

```bash
# Проверить что Android SDK установлен
ls "$HOME/Library/Android/sdk/platforms/"

# Должно показать что-то вроде:
# android-30  android-31  android-32  android-33  android-34  android-35
```

Если нет SDK:
1. Установите [Android Studio](https://developer.android.com/studio)
2. Откройте Android Studio → Tools → SDK Manager
3. Установите любую версию Android API (30-35)

## 🎯 Результат сборки:

После успешной сборки:
- **Файл**: `../Plugins/Android/balancywebview.aar`
- **Размер**: ~50KB (без Unity зависимостей!)
- **Готов для Unity**: Да, полностью совместим

## 🔧 Архитектура решения:

### Java код (BalancyWebViewPlugin.java):
```java
// ✅ Unity интеграция через reflection - НЕТ compile-time зависимостей
private void checkUnityAvailability() {
    try {
        Class<?> unityPlayerClass = Class.forName("com.unity3d.player.UnityPlayer");
        // Unity есть → работаем с ним
        unityAvailable = true;
    } catch (Exception e) {
        // Unity нет → standalone режим
        unityAvailable = false;
    }
}

// ✅ Отправка сообщений через reflection (безопасно)
private void sendUnityMessage(String methodName, String message) {
    if (!unityAvailable) return; // Просто пропускаем если Unity нет
    
    // Используем reflection для вызова UnitySendMessage
    Class<?> unityPlayerClass = Class.forName("com.unity3d.player.UnityPlayer");
    Method sendMessage = unityPlayerClass.getMethod("UnitySendMessage", ...);
    sendMessage.invoke(null, "BalancyView", methodName, message);
}
```

### Gradle (build.gradle):
```gradle
android {
    namespace 'com.balancy.webview'
    compileSdkVersion 35
    // ... стандартные Android настройки
}

dependencies {
    // ✅ НИКАКИХ зависимостей от Unity!
    // Чистая Android библиотека
}
```

## 🏆 Преимущества нового подхода:

1. **🚀 Быстрая сборка** - не нужно ждать Unity
2. **📦 Меньший размер** - нет лишних зависимостей  
3. **🔧 Проще развертывание** - только Android SDK
4. **✅ Универсальность** - работает с Unity и без него
5. **🛡️ Стабильность** - меньше точек отказа

## 🐛 Troubleshooting:

### "Android SDK не найден"
```bash
export ANDROID_HOME="$HOME/Library/Android/sdk"
./build_standalone.sh
```

### "Gradle не найден"
- Gradle скачается автоматически при первом запуске

### "Android API не найден"
- Установите любую версию Android API через Android Studio SDK Manager

## 📝 Для разработчиков:

Теперь ваша библиотека:
- ✅ Компилируется независимо от Unity
- ✅ Автоматически определяет Unity в runtime  
- ✅ Gracefully fallback в standalone режим
- ✅ Поддерживает оба режима работы

**Вывод**: Нахуя действительно был нужен Unity для сборки Android библиотеки? Теперь его нет! 🎉

## 🚀 Запуск:

Просто выполните:
```bash
cd /Volumes/PavelData/Projects/plugin_cpp_unity/Assets/Balancy/WebView/WVAndroidLib.androidlib
./run_build.sh
```

Библиотека соберется без всяких Unity зависимостей!
