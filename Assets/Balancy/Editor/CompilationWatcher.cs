#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

[InitializeOnLoad]
public static class CompilationWatcher
{
    static CompilationWatcher()
    {
        CompilationPipeline.compilationStarted += OnCompilationStarted;
    }

    // This method is called when Unity starts compiling C# scripts
    private static void OnCompilationStarted(object obj)
    {
        Debug.Log("Compilation started. Stopping the C++ plugin.");

        // Call C++ plugin's Stop method before recompilation
        Balancy.Main.Stop();
    }
}
#endif