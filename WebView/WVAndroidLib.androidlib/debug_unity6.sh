#!/bin/bash

echo "🔍 Поиск classes.jar в Unity 6000.0.46f1..."

UNITY_PATH="/Applications/Unity/Hub/Editor/6000.0.46f1/Unity.app/Contents"

echo "🔍 Ищем classes.jar в разных местах..."

# Возможные пути для Unity 6
POSSIBLE_CLASSES_JAR_PATHS=(
    "${UNITY_PATH}/Editor/Data/PlaybackEngines/AndroidPlayer/Variations/il2cpp/Release/Classes/classes.jar"
    "${UNITY_PATH}/Editor/Data/PlaybackEngines/AndroidPlayer/Variations/mono/Release/Classes/classes.jar"
    "${UNITY_PATH}/Editor/Data/PlaybackEngines/AndroidPlayer/Release/Classes/classes.jar"
    "${UNITY_PATH}/Editor/Data/PlaybackEngines/AndroidPlayer/Classes/classes.jar"
    "${UNITY_PATH}/PlaybackEngines/AndroidPlayer/Variations/il2cpp/Release/Classes/classes.jar"
    "${UNITY_PATH}/PlaybackEngines/AndroidPlayer/Release/Classes/classes.jar"
    "${UNITY_PATH}/PlaybackEngines/AndroidPlayer/Classes/classes.jar"
)

FOUND_CLASSES_JAR=""

for jar_path in "${POSSIBLE_CLASSES_JAR_PATHS[@]}"; do
    echo "   Проверяем: $jar_path"
    if [ -f "$jar_path" ]; then
        echo "   ✅ НАЙДЕН!"
        FOUND_CLASSES_JAR="$jar_path"
        break
    else
        echo "   ❌ Не найден"
    fi
done

if [ -z "$FOUND_CLASSES_JAR" ]; then
    echo ""
    echo "❌ classes.jar не найден в стандартных местах"
    echo "🔍 Поиск через find..."
    
    FIND_RESULT=$(find "$UNITY_PATH" -name "classes.jar" -type f 2>/dev/null)
    if [ ! -z "$FIND_RESULT" ]; then
        echo "✅ Найдено через find:"
        echo "$FIND_RESULT"
        FOUND_CLASSES_JAR=$(echo "$FIND_RESULT" | head -1)
    else
        echo "❌ classes.jar вообще не найден в Unity!"
        echo "🔍 Поиск Android-related файлов..."
        find "$UNITY_PATH" -name "*android*" -type f 2>/dev/null | head -10
    fi
else
    echo ""
    echo "🎯 classes.jar найден: $FOUND_CLASSES_JAR"
fi

echo ""
echo "🔍 Проверим структуру PlaybackEngines:"
ls -la "${UNITY_PATH}/Editor/Data/PlaybackEngines/" 2>/dev/null || echo "PlaybackEngines не найден в Editor/Data/"
ls -la "${UNITY_PATH}/PlaybackEngines/" 2>/dev/null || echo "PlaybackEngines не найден в корне"

if [ ! -z "$FOUND_CLASSES_JAR" ]; then
    echo ""
    echo "💡 Правильная команда для сборки:"
    echo "export UNITY_EDITOR_PATH='$UNITY_PATH'"
    echo "./setup_and_build.sh"
fi
