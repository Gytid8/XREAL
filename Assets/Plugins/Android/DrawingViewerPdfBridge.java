package com.netzero.drawingviewer;

import android.graphics.Bitmap;
import android.graphics.pdf.PdfRenderer;
import android.os.ParcelFileDescriptor;

import java.io.File;
import java.io.FileOutputStream;

public class DrawingViewerPdfBridge {
    private static final Object LOCK = new Object();

    public static int getPageCount(String path) {
        synchronized (LOCK) {
            ParcelFileDescriptor fd = null;
            PdfRenderer renderer = null;
            try {
                fd = ParcelFileDescriptor.open(new File(path), ParcelFileDescriptor.MODE_READ_ONLY);
                renderer = new PdfRenderer(fd);
                return renderer.getPageCount();
            } catch (Exception ex) {
                throw new RuntimeException(ex.getMessage(), ex);
            } finally {
                if (renderer != null) {
                    renderer.close();
                }
                if (fd != null) {
                    try {
                        fd.close();
                    } catch (Exception ignored) {
                    }
                }
            }
        }
    }

    public static String renderPageToPng(String path, int pageIndex, int maxSize, String outputDir) {
        synchronized (LOCK) {
            ParcelFileDescriptor fd = null;
            PdfRenderer renderer = null;
            PdfRenderer.Page page = null;
            Bitmap bitmap = null;
            try {
                fd = ParcelFileDescriptor.open(new File(path), ParcelFileDescriptor.MODE_READ_ONLY);
                renderer = new PdfRenderer(fd);

                if (pageIndex < 0 || pageIndex >= renderer.getPageCount()) {
                    throw new IllegalArgumentException("Invalid PDF page index: " + pageIndex);
                }

                page = renderer.openPage(pageIndex);

                int width = page.getWidth();
                int height = page.getHeight();
                float scale = 1f;
                if (width > maxSize || height > maxSize) {
                    scale = Math.min((float) maxSize / width, (float) maxSize / height);
                }

                int outW = Math.max(1, Math.round(width * scale));
                int outH = Math.max(1, Math.round(height * scale));

                bitmap = Bitmap.createBitmap(outW, outH, Bitmap.Config.ARGB_8888);
                bitmap.eraseColor(0xFFFFFFFF);
                page.render(bitmap, null, null, PdfRenderer.Page.RENDER_MODE_FOR_DISPLAY);

                File dir = new File(outputDir);
                if (!dir.exists() && !dir.mkdirs()) {
                    throw new IllegalStateException("Failed to create PDF render cache directory.");
                }

                File out = new File(dir, "pdf_page_" + pageIndex + ".png");
                try (FileOutputStream fos = new FileOutputStream(out)) {
                    if (!bitmap.compress(Bitmap.CompressFormat.PNG, 100, fos)) {
                        throw new IllegalStateException("Failed to encode PDF page PNG.");
                    }
                }

                return out.getAbsolutePath();
            } catch (Exception ex) {
                throw new RuntimeException(ex.getMessage(), ex);
            } finally {
                if (bitmap != null) {
                    bitmap.recycle();
                }
                if (page != null) {
                    page.close();
                }
                if (renderer != null) {
                    renderer.close();
                }
                if (fd != null) {
                    try {
                        fd.close();
                    } catch (Exception ignored) {
                    }
                }
            }
        }
    }
}
