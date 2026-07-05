package com.netzero.drawingviewer;

import android.app.Activity;
import android.content.Intent;
import android.net.Uri;
import android.os.Bundle;
import android.text.TextUtils;

import com.unity3d.player.UnityPlayer;

import java.io.File;
import java.io.FileOutputStream;
import java.io.InputStream;

public class DrawingViewerPickerActivity extends Activity {
    private static final int REQUEST_CODE_PICK_IMAGE = 53001;
    private static String callbackGameObject;
    private static String callbackMethod;

    public static void launch(Activity hostActivity, String gameObject, String method) {
        callbackGameObject = gameObject;
        callbackMethod = method;

        Intent intent = new Intent(hostActivity, DrawingViewerPickerActivity.class);
        hostActivity.startActivity(intent);
    }

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);

        try {
            Intent intent = new Intent(Intent.ACTION_GET_CONTENT);
            intent.setType("image/*");
            intent.addCategory(Intent.CATEGORY_OPENABLE);
            Intent chooser = Intent.createChooser(intent, "选择图纸");
            startActivityForResult(chooser, REQUEST_CODE_PICK_IMAGE);
        } catch (Exception ex) {
            sendResult("");
            finish();
        }
    }

    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        super.onActivityResult(requestCode, resultCode, data);

        if (requestCode != REQUEST_CODE_PICK_IMAGE) {
            finish();
            return;
        }

        if (resultCode != RESULT_OK || data == null || data.getData() == null) {
            sendResult("");
            finish();
            return;
        }

        String copiedPath = copyUriToCache(data.getData());
        sendResult(copiedPath != null ? copiedPath : "");
        finish();
    }

    private String copyUriToCache(Uri uri) {
        InputStream inputStream = null;
        FileOutputStream outputStream = null;

        try {
            inputStream = getContentResolver().openInputStream(uri);
            if (inputStream == null) {
                return null;
            }

            String extension = guessExtension(uri);
            File cacheDir = new File(getCacheDir(), "picked_drawings");
            if (!cacheDir.exists() && !cacheDir.mkdirs()) {
                return null;
            }

            File outFile = new File(cacheDir, "picked_" + System.currentTimeMillis() + extension);
            outputStream = new FileOutputStream(outFile);

            byte[] buffer = new byte[8192];
            int read;
            while ((read = inputStream.read(buffer)) != -1) {
                outputStream.write(buffer, 0, read);
            }

            outputStream.flush();
            return outFile.getAbsolutePath();
        } catch (Exception ex) {
            return null;
        } finally {
            try {
                if (inputStream != null) inputStream.close();
            } catch (Exception ignored) {
            }
            try {
                if (outputStream != null) outputStream.close();
            } catch (Exception ignored) {
            }
        }
    }

    private String guessExtension(Uri uri) {
        String mime = getContentResolver().getType(uri);
        if (!TextUtils.isEmpty(mime)) {
            if (mime.contains("png")) return ".png";
            if (mime.contains("jpeg") || mime.contains("jpg")) return ".jpg";
        }

        String path = uri.getLastPathSegment();
        if (!TextUtils.isEmpty(path)) {
            int dot = path.lastIndexOf('.');
            if (dot >= 0) {
                String ext = path.substring(dot).toLowerCase();
                if (ext.equals(".png") || ext.equals(".jpg") || ext.equals(".jpeg")) {
                    return ext.equals(".jpeg") ? ".jpg" : ext;
                }
            }
        }

        return ".png";
    }

    private static void sendResult(String path) {
        if (callbackGameObject == null || callbackMethod == null) {
            return;
        }

        UnityPlayer.UnitySendMessage(callbackGameObject, callbackMethod, path != null ? path : "");
        callbackGameObject = null;
        callbackMethod = null;
    }
}
