#!/bin/bash

set -e

echo "🚀 Building Android .aar for Balancy WebView (Standalone Mode)..."
echo "🔧 No Unity dependencies required!"

ANDROID_PROJECT_DIR="AndroidProject"
OUTPUT_DIR="../Plugins/Android"
AAR_NAME="balancywebview.aar"

# 🔍 Найти Android SDK
POSSIBLE_ANDROID_PATHS=(
    "$HOME/Library/Android/sdk"
    "$HOME/Android/Sdk" 
    "/usr/local/android-sdk"
    "/opt/android-sdk"
    "$ANDROID_HOME"
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
    exit 1
fi

# 🔍 Найти подходящую версию Android API
POSSIBLE_ANDROID_APIS=("35" "34" "33" "32" "31" "30")

ANDROID_JAR_PATH=""
for api in "${POSSIBLE_ANDROID_APIS[@]}"; do
    jar_path="${ANDROID_HOME}/platforms/android-${api}/android.jar"
    if [ -f "$jar_path" ]; then
        ANDROID_JAR_PATH="$jar_path"
        echo "✅ Найден Android API $api: $jar_path"
        break
    fi
done

if [ -z "$ANDROID_JAR_PATH" ]; then
    echo "❌ android.jar не найден!"
    exit 1
fi

# 📝 Создать local.properties
echo "🔧 Настройка Android проекта..."
echo "sdk.dir=$ANDROID_HOME" > "$ANDROID_PROJECT_DIR/local.properties"

# 🔧 ПРИНУДИТЕЛЬНО обновить Gradle Wrapper до 8.7
echo "🔧 Принудительное обновление Gradle Wrapper до 8.7..."
echo "distributionBase=GRADLE_USER_HOME
distributionPath=wrapper/dists
distributionUrl=https\\://services.gradle.org/distributions/gradle-8.7-all.zip
zipStoreBase=GRADLE_USER_HOME
zipStorePath=wrapper/dists" > "$ANDROID_PROJECT_DIR/gradle/wrapper/gradle-wrapper.properties"

# 🚀 Сборка
echo "🚀 Запуск Gradle сборки с Gradle 8.7..."
(
  cd "$ANDROID_PROJECT_DIR" && \
  chmod +x ./gradlew && \
  ./gradlew clean && \
  ./gradlew :app:assembleRelease
)

echo "✅ Gradle сборка успешна!"

# 📦 Копирование результата
echo "📦 Копирование .aar файла..."
mkdir -p "$OUTPUT_DIR"

AAR_SOURCE=$(find "$ANDROID_PROJECT_DIR/app/build/outputs/aar" -name "*.aar" | head -1)

if [ -z "$AAR_SOURCE" ]; then
    echo "❌ .aar файл не найден!"
    exit 1
fi

cp "$AAR_SOURCE" "$OUTPUT_DIR/$AAR_NAME"

# 🧹 Очистка
echo "🧹 Очистка..."
(cd "$ANDROID_PROJECT_DIR" && ./gradlew clean)
rm -rf "$ANDROID_PROJECT_DIR/.gradle"
rm -f "$ANDROID_PROJECT_DIR/local.properties"

echo ""
echo "🎉 =================================================="
echo "✅ Successfully built $AAR_NAME"
echo "📂 Output: $OUTPUT_DIR/$AAR_NAME"
echo "🚀 Ready for Unity!"
echo "=================================================="
