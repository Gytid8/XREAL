AR Engineering Drawing Viewer - Instructions
===========================================

Place your engineering drawing files in this folder.

Supported formats:
  - PNG images (.png) - Recommended for best performance
  - JPG images (.jpg, .jpeg)

For multi-page documents:
  - Place all pages of a document in a subfolder
  - Name pages sequentially: page-01.png, page-02.png, etc.

Example structure:
  Drawings/
    project-alpha/
      page-01.png
      page-02.png
      page-03.png
    schematic-b.png
    floor-plan.jpg

Note:
  - Images are automatically compressed to ASTC 6x6 on Android
  - Recommended max resolution: 2048x2048 pixels
  - For PDF files, pre-convert to PNG images before building the APK
  - Files placed here are bundled inside the APK at build time
