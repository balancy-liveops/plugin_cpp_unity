using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;


public class ImportScript : MonoBehaviour
{
    #if UNITY_IPHONE && !UNITY_EDITOR
    [DllImport ("__Internal")]
    #else
    [DllImport("libBalancy")]
    #endif
    private static extern int testInit();
    #if UNITY_IPHONE && !UNITY_EDITOR
    [DllImport ("__Internal")]
    #else
    [DllImport("libBalancy")]
    #endif
    private static extern IntPtr testGenerate([MarshalAs(UnmanagedType.LPStr)] string inPath);
    
    #if UNITY_IPHONE && !UNITY_EDITOR
    [DllImport ("__Internal")]
    #else
    [DllImport("libBalancy")]
#endif
    private static extern int baseInit();
    // Start is called before the first frame update
    void Start()
    {
        print(Application.persistentDataPath);
        //print(testInit());
        print(Marshal.PtrToStringAuto(testGenerate(Application.persistentDataPath+"/Assets/")));
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
