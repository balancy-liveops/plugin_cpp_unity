#!/bin/bash

# 🔧 Упрощенный скрипт поиска Unity для отладки

echo "🔍 Диагностика Unity установки..."
echo ""

# Проверим, не задан ли уже путь
if [ ! -z "$UNITY_EDITOR_PATH" ]; then
    echo "✅ UNITY_EDITOR_PATH уже задан: $UNITY_EDITOR_PATH"
    if [ -d "$UNITY_EDITOR_PATH" ]; then
        echo "✅ Путь существует"
    else
        echo "❌ Путь не существует"
    fi
    echo ""
fi

echo "🔍 Поиск Unity в стандартных местах..."

# Основные паттерны поиска
SEARCH_PATTERNS=(
    "/Applications/Unity/Hub/Editor/*/Unity.app"
    "/Applications/Unity*.app"
    "/Applications/*/Unity*.app"
)

FOUND_UNITY=()

for pattern in "${SEARCH_PATTERNS[@]}"; do
    echo "   Ищем: $pattern"
    for path in $pattern; do
        if [ -d "$path" ]; then
            FOUND_UNITY+=("$path")
            echo "   ✅ Найден: $path"
        fi
    done
done

echo ""
if [ ${#FOUND_UNITY[@]} -eq 0 ]; then
    echo "❌ Unity не найден в стандартных местах"
    echo ""
    echo "💡 Попробуйте:"
    echo "   1. Проверить установку Unity Hub: ls /Applications/Unity/"
    echo "   2. Проверить версии Unity: ls /Applications/Unity/Hub/Editor/"
    echo "   3. Найти Unity вручную: find /Applications -name 'Unity.app' 2>/dev/null"
    echo ""
    echo "📝 Если Unity установлен, укажите путь вручную:"
    echo "   export UNITY_EDITOR_PATH='/путь/к/Unity.app/Contents'"
    echo "   ./setup_and_build.sh"
else
    echo "🎯 Найденные установки Unity:"
    for unity_path in "${FOUND_UNITY[@]}"; do
        contents_path="${unity_path}/Contents"
        echo "   $unity_path"
        echo "   → Contents: $contents_path"
        if [ -d "$contents_path" ]; then
            echo "   ✅ Contents папка существует"
        else
            echo "   ❌ Contents папка отсутствует"
        fi
        
        # Проверим classes.jar
        classes_jar_path="${contents_path}/Editor/Data/PlaybackEngines/AndroidPlayer/Variations/il2cpp/Release/Classes/classes.jar"
        if [ -f "$classes_jar_path" ]; then
            echo "   ✅ Unity classes.jar найден"
        else
            echo "   ❌ Unity classes.jar отсутствует (нужен Android Build Support)"
        fi
        echo ""
    done
    
    # Предложим первый найденный путь
    if [ ${#FOUND_UNITY[@]} -gt 0 ]; then
        RECOMMENDED_PATH="${FOUND_UNITY[0]}/Contents"
        echo "💡 Рекомендуемая команда:"
        echo "   export UNITY_EDITOR_PATH='$RECOMMENDED_PATH'"
        echo "   ./setup_and_build.sh"
    fi
fi

echo ""
echo "🔍 Проверка Android SDK..."
if [ -d "$HOME/Library/Android/sdk" ]; then
    echo "✅ Android SDK найден: $HOME/Library/Android/sdk"
else
    echo "❌ Android SDK не найден в $HOME/Library/Android/sdk"
fi
