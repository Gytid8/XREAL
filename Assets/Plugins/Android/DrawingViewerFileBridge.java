package com.netzero.drawingviewer;

import android.app.Activity;

public class DrawingViewerFileBridge {
    public static void startImagePicker(Activity activity, String gameObject, String method) {
        DrawingViewerPickerActivity.launch(activity, gameObject, method);
    }
}
