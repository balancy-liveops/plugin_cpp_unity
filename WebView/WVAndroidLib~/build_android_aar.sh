#!/bin/bash

set -e

echo "Building Android .aar for Balancy WebView..."

ANDROID_PROJECT_DIR="AndroidProject"
ANDROID_JAR_PATH="${ANDROID_HOME}/platforms/android-33/android.jar"
UNITY_CLASSES_JAR_PATH="${UNITY_EDITOR_PATH}/Editor/Data/PlaybackEngines/AndroidPlayer/Variations/il2cpp/Release/Classes/classes.jar"

OUTPUT_DIR="../Plugins/Android"
AAR_NAME="balancywebview.aar"

if [ ! -f "$ANDROID_JAR_PATH" ]; then
    echo ""
    echo "[ERROR] 'android.jar' not found!"
    echo "Searched at: $ANDROID_JAR_PATH"
    echo "Please check that your UNITY_EDITOR_PATH variable at the top of this script is correct."
    echo ""
    exit 1
fi
if [ ! -f "$UNITY_CLASSES_JAR_PATH" ]; then
    echo "[ERROR] Unity's 'classes.jar' not found at: $UNITY_CLASSES_JAR_PATH"
    echo "Please check your UNITY_EDITOR_PATH variable."
    exit 1
fi

echo "Found required libraries."

echo "sdk.dir=$ANDROID_HOME" > "$ANDROID_PROJECT_DIR/local.properties"

if [ ! -f "$ANDROID_PROJECT_DIR/gradlew" ]; then
    echo "Initializing Gradle Wrapper..."
    temp_dir=$(mktemp -d)
    (cd "$temp_dir" && gradle wrapper --gradle-version 8.4)
    cp -r "$temp_dir/gradle" "$ANDROID_PROJECT_DIR/"
    cp "$temp_dir/gradlew" "$ANDROID_PROJECT_DIR/"
    cp "$temp_dir/gradlew.bat" "$ANDROID_PROJECT_DIR/"
    rm -rf "$temp_dir"
fi

echo "Starting Gradle build..."
(
  cd "$ANDROID_PROJECT_DIR" && \
  chmod +x ./gradlew && \
  ./gradlew :app:assembleRelease -PunityClassesJar="$UNITY_CLASSES_JAR_PATH"
)

echo "Gradle build successful."

echo "Copying artifact to Unity plugin folder..."
mkdir -p "$OUTPUT_DIR"
cp "$ANDROID_PROJECT_DIR/app/build/outputs/aar/app-release.aar" "$OUTPUT_DIR/$AAR_NAME"

echo "Cleaning up build artifacts..."
(
  cd "$ANDROID_PROJECT_DIR" && \
  ./gradlew clean
)
rm -rf "$ANDROID_PROJECT_DIR/.gradle"
rm "$ANDROID_PROJECT_DIR/local.properties"

echo ""
echo "-----------------------------------------------------"
echo "✅ Successfully built $AAR_NAME"
echo "   Output location: $OUTPUT_DIR"
echo "-----------------------------------------------------"