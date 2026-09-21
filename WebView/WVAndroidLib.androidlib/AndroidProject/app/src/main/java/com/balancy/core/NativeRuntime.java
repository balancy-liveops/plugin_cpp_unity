package com.balancy.core;

/** JNI access shared by Unity versions, including Unity 2021. */
public final class NativeRuntime {
    static {
        System.loadLibrary("BalancyCore");
    }

    private NativeRuntime() {}

    public static native long getJavaVM();
}
