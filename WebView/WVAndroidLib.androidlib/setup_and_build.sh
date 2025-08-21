#!/bin/bash

# 🔧 Скрипт для установки переменных среды и сборки Android AAR

echo "🔧 Настройка переменных среды для Android сборки..."

# Поиск Android SDK
POSSIBLE_ANDROID_PATHS=(
    "$HOME/Library/Android/sdk"
    "$HOME/Android/Sdk" 
    "/usr/local/android-sdk"
    "/opt/android-sdk"
)

echo "🔍 Поиск Android SDK..."
for path in "${POSSIBLE_ANDROID_PATHS[@]}"; do
    if [ -d "$path" ]; then
        export ANDROID_HOME="$path"
        echo "✅ Найден Android SDK: $ANDROID_HOME"
        break
    fi
done

if [ -z "$ANDROID_HOME" ]; then
    echo "❌ Android SDK не найден!"
    echo "📥 Установите Android SDK через Android Studio или скачайте command line tools"
    echo "🌐 https://developer.android.com/studio"
    exit 1
fi

# Поиск Unity Editor
POSSIBLE_UNITY_PATHS=(
    "/Applications/Unity/Hub/Editor/*/Unity.app/Contents"
    "/Applications/Unity/Hub/Editor/2024.*/Unity.app/Contents"
    "/Applications/Unity/Hub/Editor/2023.*/Unity.app/Contents"
    "/Applications/Unity/Hub/Editor/2022.*/Unity.app/Contents"
    "/Applications/Unity/Hub/Editor/2021.*/Unity.app/Contents"
    "/Applications/Unity.app/Contents"
    "/opt/Unity/Editor/Unity.app/Contents"
    "$HOME/Unity/Hub/Editor/*/Unity.app/Contents"
)

echo "🔍 Поиск Unity Editor..."
# Если UNITY_EDITOR_PATH уже задан, используем его
if [ ! -z "$UNITY_EDITOR_PATH" ]; then
    echo "✅ Используется заданный путь: $UNITY_EDITOR_PATH"
else
    # Автоматический поиск
    for pattern in "${POSSIBLE_UNITY_PATHS[@]}"; do
        # Используем glob для поиска по паттерну
        for path in $pattern; do
            if [ -d "$path" ]; then
                # Проверяем наличие classes.jar для Android Build Support
                # Поддержка Unity 6+ и старых версий
                classes_jar_unity6="${path}/PlaybackEngines/AndroidPlayer/Variations/il2cpp/Release/Classes/classes.jar"
                classes_jar_old="${path}/Editor/Data/PlaybackEngines/AndroidPlayer/Variations/il2cpp/Release/Classes/classes.jar"
                
                if [ -f "$classes_jar_unity6" ]; then
                    export UNITY_EDITOR_PATH="$path"
                    echo "✅ Найден Unity Editor с Android Build Support (Unity 6+): $UNITY_EDITOR_PATH"
                    break 2  # Выходим из обоих циклов
                elif [ -f "$classes_jar_old" ]; then
                    export UNITY_EDITOR_PATH="$path"
                    echo "✅ Найден Unity Editor с Android Build Support (старая версия): $UNITY_EDITOR_PATH"
                    break 2  # Выходим из обоих циклов
                else
                    echo "⚠️  Найден Unity без Android Build Support: $path"
                fi
            fi
        done
    done
fi

if [ -z "$UNITY_EDITOR_PATH" ]; then
    echo "❌ Unity Editor с Android Build Support не найден!"
    echo ""
    echo "📝 Вариант 1: Указать путь вручную:"
    echo "   export UNITY_EDITOR_PATH='/Applications/Unity/Hub/Editor/6000.0.46f1/Unity.app/Contents'"
    echo "   ./setup_and_build.sh"
    echo ""
    echo "📝 Вариант 2: Установить Android Build Support:"
    echo "   1. Откройте Unity Hub"
    echo "   2. Installs → [Ваша версия Unity] → ⚠️ → Add Modules"
    echo "   3. Выберите 'Android Build Support' ✅"
    echo "   4. Нажмите Done и дождитесь установки"
    echo ""
    echo "🔍 Для диагностики запустите: ./find_unity.sh"
    echo "🌐 Unity Hub: https://unity.com/download"
    exit 1
fi

# Проверка необходимых файлов
# Поддержка разных версий Android API
POSSIBLE_ANDROID_APIS=("35" "34" "33" "32" "31" "30" "29" "28")

ANDROID_JAR_PATH=""
for api in "${POSSIBLE_ANDROID_APIS[@]}"; do
    jar_path="${ANDROID_HOME}/platforms/android-${api}/android.jar"
    if [ -f "$jar_path" ]; then
        ANDROID_JAR_PATH="$jar_path"
        echo "✅ Найден Android API $api: $jar_path"
        break
    fi
done

# Поддержка разных версий Unity (Unity 6 и более старые)
POSSIBLE_UNITY_CLASSES_PATHS=(
    "${UNITY_EDITOR_PATH}/PlaybackEngines/AndroidPlayer/Variations/il2cpp/Release/Classes/classes.jar"  # Unity 6+
    "${UNITY_EDITOR_PATH}/Editor/Data/PlaybackEngines/AndroidPlayer/Variations/il2cpp/Release/Classes/classes.jar"  # Unity 2022 и старше
    "${UNITY_EDITOR_PATH}/Editor/Data/PlaybackEngines/AndroidPlayer/Variations/mono/Release/Classes/classes.jar"  # Mono версия
)

UNITY_CLASSES_JAR_PATH=""
for jar_path in "${POSSIBLE_UNITY_CLASSES_PATHS[@]}"; do
    if [ -f "$jar_path" ]; then
        UNITY_CLASSES_JAR_PATH="$jar_path"
        break
    fi
done

echo ""
echo "🔍 Проверка необходимых файлов..."

if [ -z "$ANDROID_JAR_PATH" ]; then
    echo "❌ android.jar не найден ни в одной версии Android API!"
    echo "📂 Проверьте установленные версии: ls ${ANDROID_HOME}/platforms/"
    echo "📝 Установите Android API через Android Studio SDK Manager"
    echo "   Tools → SDK Manager → SDK Platforms → Android 13 (API 33)"
    exit 1
else
    echo "✅ android.jar найден"
fi

if [ ! -f "$UNITY_CLASSES_JAR_PATH" ]; then
    echo "❌ Unity classes.jar не найден по пути: $UNITY_CLASSES_JAR_PATH"
    echo "📥 Установите Android Build Support в Unity Hub"
    echo "   Unity Hub → Installs → [Your Unity Version] → Add Modules → Android Build Support"
    exit 1
else
    echo "✅ Unity classes.jar найден"
fi

echo ""
echo "🎯 Переменные среды установлены:"
echo "   ANDROID_HOME=$ANDROID_HOME"
echo "   UNITY_EDITOR_PATH=$UNITY_EDITOR_PATH"
echo ""

# Запуск сборки
echo "🚀 Запуск сборки Android AAR..."
./build_android_aar.sh

echo ""
echo "✅ Сборка завершена!"
