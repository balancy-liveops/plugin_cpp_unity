#!/bin/bash

echo "🔍 Проверка установленных Android API..."

ANDROID_HOME="/Users/pavelignatov/Library/Android/sdk"

if [ ! -d "$ANDROID_HOME/platforms" ]; then
    echo "❌ Папка platforms не найдена в Android SDK"
    exit 1
fi

echo "📂 Найденные Android API:"
ls -la "$ANDROID_HOME/platforms/" | grep android- | while read line; do
    api_dir=$(echo "$line" | awk '{print $NF}')
    api_level=$(echo "$api_dir" | sed 's/android-//')
    
    # Проверим наличие android.jar
    android_jar="$ANDROID_HOME/platforms/$api_dir/android.jar"
    if [ -f "$android_jar" ]; then
        echo "   ✅ API $api_level ($api_dir) - android.jar есть"
    else
        echo "   ❌ API $api_level ($api_dir) - android.jar отсутствует"
    fi
done

echo ""
echo "🔍 Можем ли мы использовать другую версию API?"

# Найдем любую подходящую версию API (28+ обычно достаточно)
SUITABLE_API=""
for api_dir in "$ANDROID_HOME/platforms/"android-*; do
    if [ -d "$api_dir" ]; then
        api_level=$(basename "$api_dir" | sed 's/android-//')
        android_jar="$api_dir/android.jar"
        
        if [ -f "$android_jar" ] && [ "$api_level" -ge 28 ]; then
            SUITABLE_API="$api_level"
            echo "   ✅ Можно использовать API $api_level: $android_jar"
            break
        fi
    fi
done

if [ ! -z "$SUITABLE_API" ]; then
    echo ""
    echo "💡 Рекомендация: Обновите скрипт для использования API $SUITABLE_API:"
    echo "   Измените android-33 на android-$SUITABLE_API в setup_and_build.sh"
    echo ""
    echo "   Или установите API 33 через Android Studio:"
    echo "   Android Studio → Tools → SDK Manager → SDK Platforms → Android 13 (API 33)"
else
    echo ""
    echo "❌ Подходящих версий Android API не найдено"
    echo "📥 Установите Android API через Android Studio:"
    echo "   Android Studio → Tools → SDK Manager → SDK Platforms"
    echo "   Выберите любую версию API 28+ (рекомендуется API 33)"
fi

echo ""
echo "🔍 Альтернативно - проверим, есть ли командная строка Android SDK:"
if command -v sdkmanager >/dev/null 2>&1; then
    echo "✅ sdkmanager найден"
    echo "💡 Можно установить API 33 через командную строку:"
    echo "   sdkmanager 'platforms;android-33'"
else
    echo "❌ sdkmanager не найден в PATH"
    echo "💡 Используйте Android Studio для установки API"
fi
