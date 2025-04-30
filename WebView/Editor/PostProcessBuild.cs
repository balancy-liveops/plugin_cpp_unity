using UnityEditor;
using UnityEditor.iOS.Xcode;
using System.IO;
using UnityEditor.Callbacks;

public class PostProcessBuild
{
    [PostProcessBuild(1)]
    public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
    {
        // Проверяем, что цель сборки - iOS
        if (target == BuildTarget.iOS)
        {
            // Путь к проекту Xcode (проверьте, существует ли он)
            string projectPath = Path.Combine(pathToBuiltProject, "project.pbxproj");

            // Если проект Xcode существует
            if (File.Exists(projectPath))
            {
                UnityEngine.Debug.Log("Found Xcode project: " + projectPath);

                // Загружаем проект Xcode
                PBXProject project = new PBXProject();
                project.ReadFromFile(projectPath);

                // Получаем основной target проекта
                string targetGuid = project.GetUnityMainTargetGuid();

                // Добавляем фреймворк Cocoa
                project.AddFrameworkToProject(targetGuid, "Cocoa.framework", false);

                // Сохраняем изменения
                project.WriteToFile(projectPath);
            }
            else
            {
                UnityEngine.Debug.LogError("Could not find project.pbxproj at " + projectPath);
            }
        }
    }
}