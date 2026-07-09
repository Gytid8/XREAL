本次主要完成了两处修改：
第一处是图纸查看器中的缩放逻辑，
修改文件为 Assets/_App/DrawingViewer/Scripts/Runtime/Interaction/DrawingControllerInput.cs，
将原先触控板竖向滑动时连续不断放大或缩小的逻辑，调整为累计滑动量达到阈值后才触发一次固定比例缩放，使图纸缩放变成一档一档变化，操作更稳定、不容易放大过头。
第二处是图纸查看器工具栏中的“排版 单页”按钮显示效果，
修改文件为 Assets/_App/DrawingViewer/Scripts/Runtime/UI/DrawingToolbarUI.cs 和 Assets/_App/DrawingViewer/Scripts/Runtime/UI/DrawingViewerUiFactory.cs，
将原本容易被挤成不规则多行的“排版·单页”改为固定两行显示，即第一行显示“排版”，第二行显示当前模式如“单页”“并排”“展开”，同时适当调整该按钮的文字样式和尺寸，保证显示更规整。
