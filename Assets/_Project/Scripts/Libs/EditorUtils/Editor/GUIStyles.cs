using UnityEngine;

namespace FS.GUIStyles
{
    /// <summary>
    /// Provides centralized access to Unity's built-in GUI styles.
    /// All styles are initialized as static readonly to ensure they're only created once.
    /// </summary>
    public static class GUIStyles
    {
        // About Window Styles
        public static readonly GUIStyle AboutWindowLicenseLabel = new GUIStyle("AboutWIndowLicenseLabel");

        // Animation Component Styles
        public static readonly GUIStyle ACBoldHeader = new GUIStyle("AC BoldHeader");
        public static readonly GUIStyle ACButton = new GUIStyle("AC Button");
        public static readonly GUIStyle ACComponentButton = new GUIStyle("AC ComponentButton");
        public static readonly GUIStyle ACGroupButton = new GUIStyle("AC GroupButton");
        public static readonly GUIStyle ACLeftArrow = new GUIStyle("AC LeftArrow");
        public static readonly GUIStyle ACPreviewHeader = new GUIStyle("AC PreviewHeader");
        public static readonly GUIStyle ACPreviewText = new GUIStyle("AC PreviewText");
        public static readonly GUIStyle ACRightArrow = new GUIStyle("AC RightArrow");

        // Audio Mixer Styles
        public static readonly GUIStyle AMChannelStripHeaderStyle = new GUIStyle("AM ChannelStripHeaderStyle");
        public static readonly GUIStyle AMEffectName = new GUIStyle("AM EffectName");
        public static readonly GUIStyle AMHeaderStyle = new GUIStyle("AM HeaderStyle");
        public static readonly GUIStyle AMMixerHeader2 = new GUIStyle("AM MixerHeader2");
        public static readonly GUIStyle AMMixerHeader = new GUIStyle("AM MixerHeader");
        public static readonly GUIStyle AMToolbarLabel = new GUIStyle("AM ToolbarLabel");
        public static readonly GUIStyle AMToolbarObjectField = new GUIStyle("AM ToolbarObjectField");
        public static readonly GUIStyle AMTotalVuLabel = new GUIStyle("AM TotalVuLabel");
        public static readonly GUIStyle AMVuValue = new GUIStyle("AM VuValue");

        // Animation Styles
        public static readonly GUIStyle AnimationEventBackground = new GUIStyle("AnimationEventBackground");
        public static readonly GUIStyle AnimationEventTooltipArrow = new GUIStyle("AnimationEventTooltipArrow");
        public static readonly GUIStyle AnimationEventTooltip = new GUIStyle("AnimationEventTooltip");
        public static readonly GUIStyle AnimationKeyframeBackground = new GUIStyle("AnimationKeyframeBackground");
        public static readonly GUIStyle AnimationPlayHead = new GUIStyle("AnimationPlayHead");
        public static readonly GUIStyle AnimationRowEven = new GUIStyle("AnimationRowEven");
        public static readonly GUIStyle AnimationRowOdd = new GUIStyle("AnimationRowOdd");
        public static readonly GUIStyle AnimationSelectionTextField = new GUIStyle("AnimationSelectionTextField");
        public static readonly GUIStyle AnimationTimelineTick = new GUIStyle("AnimationTimelineTick");

        // Animation Clip Styles
        public static readonly GUIStyle AnimClipToolbarButton = new GUIStyle("AnimClipToolbarButton");
        public static readonly GUIStyle AnimClipToolbarPopup = new GUIStyle("AnimClipToolbarPopup");
        public static readonly GUIStyle AnimClipToolbar = new GUIStyle("AnimClipToolbar");
        public static readonly GUIStyle AnimItemBackground = new GUIStyle("AnimItemBackground");
        public static readonly GUIStyle AnimLeftPaneSeparator = new GUIStyle("AnimLeftPaneSeparator");
        public static readonly GUIStyle AnimPlayToolbar = new GUIStyle("AnimPlayToolbar");
        public static readonly GUIStyle AnimPropDropdown = new GUIStyle("AnimPropDropdown");

        // App Command Styles
        public static readonly GUIStyle AppCommandLeftOn = new GUIStyle("AppCommandLeftOn");
        public static readonly GUIStyle AppCommandLeft = new GUIStyle("AppCommandLeft");
        public static readonly GUIStyle AppCommandMid = new GUIStyle("AppCommandMid");
        public static readonly GUIStyle AppCommandRight = new GUIStyle("AppCommandRight");
        public static readonly GUIStyle AppCommand = new GUIStyle("AppCommand");

        // App Toolbar Styles
        public static readonly GUIStyle AppToolbarButtonLeft = new GUIStyle("AppToolbarButtonLeft");
        public static readonly GUIStyle AppToolbarButtonMid = new GUIStyle("AppToolbarButtonMid");
        public static readonly GUIStyle AppToolbarButtonRight = new GUIStyle("AppToolbarButtonRight");
        public static readonly GUIStyle AppToolbar = new GUIStyle("AppToolbar");

        // Navigation Styles
        public static readonly GUIStyle ArrowNavigationLeft = new GUIStyle("ArrowNavigationLeft");
        public static readonly GUIStyle ArrowNavigationRight = new GUIStyle("ArrowNavigationRight");

        // Asset Label Styles
        public static readonly GUIStyle AssetLabelIcon = new GUIStyle("AssetLabel Icon");
        public static readonly GUIStyle AssetLabelPartial = new GUIStyle("AssetLabel Partial");
        public static readonly GUIStyle AssetLabel = new GUIStyle("AssetLabel");

        // Avatar Styles
        public static readonly GUIStyle AvatarMappingBox = new GUIStyle("AvatarMappingBox");
        public static readonly GUIStyle AvatarMappingErrorLabel = new GUIStyle("AvatarMappingErrorLabel");
        public static readonly GUIStyle AxisLabelNumberField = new GUIStyle("AxisLabelNumberField");

        // Basic UI Styles
        public static readonly GUIStyle Badge = new GUIStyle("Badge");
        public static readonly GUIStyle BoldLabel = new GUIStyle("BoldLabel");
        public static readonly GUIStyle BoldTextField = new GUIStyle("BoldTextField");
        public static readonly GUIStyle BoldToggle = new GUIStyle("BoldToggle");
        public static readonly GUIStyle BottomShadowInwards = new GUIStyle("BottomShadowInwards");
        public static readonly GUIStyle BreadcrumbsSeparator = new GUIStyle("BreadcrumbsSeparator");

        // Button Styles
        public static readonly GUIStyle ButtonLeft = new GUIStyle("ButtonLeft");
        public static readonly GUIStyle ButtonMid = new GUIStyle("ButtonMid");
        public static readonly GUIStyle ButtonRight = new GUIStyle("ButtonRight");
        public static readonly GUIStyle BypassToggle = new GUIStyle("BypassToggle");

        // Cache and Label Styles
        public static readonly GUIStyle CacheFolderLocation = new GUIStyle("CacheFolderLocation");
        public static readonly GUIStyle CenteredLabel = new GUIStyle("CenteredLabel");

        // Channel Strip Styles
        public static readonly GUIStyle ChannelStripAttenuationBar = new GUIStyle("ChannelStripAttenuationBar");

        public static readonly GUIStyle ChannelStripAttenuationMarkerSquare =
            new GUIStyle("ChannelStripAttenuationMarkerSquare");

        public static readonly GUIStyle ChannelStripBg = new GUIStyle("ChannelStripBg");
        public static readonly GUIStyle ChannelStripDuckingMarker = new GUIStyle("ChannelStripDuckingMarker");
        public static readonly GUIStyle ChannelStripEffectBar = new GUIStyle("ChannelStripEffectBar");
        public static readonly GUIStyle ChannelStripSendReturnBar = new GUIStyle("ChannelStripSendReturnBar");
        public static readonly GUIStyle ChannelStripVUMeterBg = new GUIStyle("ChannelStripVUMeterBg");

        // Toggle and Box Styles
        public static readonly GUIStyle CircularToggle = new GUIStyle("CircularToggle");
        public static readonly GUIStyle CNBox = new GUIStyle("CN Box");

        // Console Window Styles
        public static readonly GUIStyle CNCenteredText = new GUIStyle("CN CenteredText");
        public static readonly GUIStyle CNCountBadge = new GUIStyle("CN CountBadge");
        public static readonly GUIStyle CNEntryBackEven = new GUIStyle("CN EntryBackEven");
        public static readonly GUIStyle CNEntryBackOdd = new GUIStyle("CN EntryBackOdd");
        public static readonly GUIStyle CNEntryErrorIconSmall = new GUIStyle("CN EntryErrorIconSmall");
        public static readonly GUIStyle CNEntryErrorIcon = new GUIStyle("CN EntryErrorIcon");
        public static readonly GUIStyle CNEntryErrorSmall = new GUIStyle("CN EntryErrorSmall");
        public static readonly GUIStyle CNEntryError = new GUIStyle("CN EntryError");
        public static readonly GUIStyle CNEntryInfoIconSmall = new GUIStyle("CN EntryInfoIconSmall");
        public static readonly GUIStyle CNEntryInfoIcon = new GUIStyle("CN EntryInfoIcon");
        public static readonly GUIStyle CNEntryInfoSmall = new GUIStyle("CN EntryInfoSmall");
        public static readonly GUIStyle CNEntryInfo = new GUIStyle("CN EntryInfo");
        public static readonly GUIStyle CNEntryWarnIconSmall = new GUIStyle("CN EntryWarnIconSmall");
        public static readonly GUIStyle CNEntryWarnIcon = new GUIStyle("CN EntryWarnIcon");
        public static readonly GUIStyle CNEntryWarnSmall = new GUIStyle("CN EntryWarnSmall");
        public static readonly GUIStyle CNEntryWarn = new GUIStyle("CN EntryWarn");
        public static readonly GUIStyle CNMessage = new GUIStyle("CN Message");
        public static readonly GUIStyle CNStacktraceBackground = new GUIStyle("CN StacktraceBackground");
        public static readonly GUIStyle CNStacktraceStyle = new GUIStyle("CN StacktraceStyle");
        public static readonly GUIStyle CNStatusError = new GUIStyle("CN StatusError");
        public static readonly GUIStyle CNStatusInfo = new GUIStyle("CN StatusInfo");
        public static readonly GUIStyle CNStatusWarn = new GUIStyle("CN StatusWarn");

        // Color Related Styles
        public static readonly GUIStyle ColorField = new GUIStyle("ColorField");
        public static readonly GUIStyle ColorPicker2DThumb = new GUIStyle("ColorPicker2DThumb");
        public static readonly GUIStyle ColorPickerBackground = new GUIStyle("ColorPickerBackground");
        public static readonly GUIStyle ColorPickerBox = new GUIStyle("ColorPickerBox");
        public static readonly GUIStyle ColorPickerCurrentColor = new GUIStyle("ColorPickerCurrentColor");

        public static readonly GUIStyle ColorPickerCurrentExposureSwatchBorder =
            new GUIStyle("ColorPickerCurrentExposureSwatchBorder");

        public static readonly GUIStyle ColorPickerExposureSwatch = new GUIStyle("ColorPickerExposureSwatch");
        public static readonly GUIStyle ColorPickerHorizThumb = new GUIStyle("ColorPickerHorizThumb");
        public static readonly GUIStyle ColorPickerHueRingHDR = new GUIStyle("ColorPickerHueRing HDR");
        public static readonly GUIStyle ColorPickerHueRingThumb = new GUIStyle("ColorPickerHueRingThumb");
        public static readonly GUIStyle ColorPickerHueRing = new GUIStyle("ColorPickerHueRing");
        public static readonly GUIStyle ColorPickerOriginalColor = new GUIStyle("ColorPickerOriginalColor");
        public static readonly GUIStyle ColorPickerSliderBackground = new GUIStyle("ColorPickerSliderBackground");

        // Command Styles
        public static readonly GUIStyle CommandLeft = new GUIStyle("CommandLeft");
        public static readonly GUIStyle CommandMid = new GUIStyle("CommandMid");
        public static readonly GUIStyle CommandRight = new GUIStyle("CommandRight");
        public static readonly GUIStyle Command = new GUIStyle("Command");

        // Content and Control Styles
        public static readonly GUIStyle ContentToolbar = new GUIStyle("ContentToolbar");
        public static readonly GUIStyle ControlHighlight = new GUIStyle("ControlHighlight");
        public static readonly GUIStyle ControlLabel = new GUIStyle("ControlLabel");
        
        // Curve Editor Styles
        public static readonly GUIStyle CurveEditorBackground = new GUIStyle("CurveEditorBackground");
        public static readonly GUIStyle CurveEditorLabelTickmarksOverflow = new GUIStyle("CurveEditorLabelTickmarksOverflow");
        public static readonly GUIStyle CurveEditorLabelTickmarks = new GUIStyle("CurveEditorLabelTickmarks");
        public static readonly GUIStyle CurveEditorRightAlignedLabel = new GUIStyle("CurveEditorRightAlignedLabel");

        // Dropdown (DD) Styles
        public static readonly GUIStyle DDBackground = new GUIStyle("DD Background");
        public static readonly GUIStyle DDHeaderStyle = new GUIStyle("DD HeaderStyle");
        public static readonly GUIStyle DDItemCheckmark = new GUIStyle("DD ItemCheckmark");
        public static readonly GUIStyle DDItemStyle = new GUIStyle("DD ItemStyle");
        public static readonly GUIStyle DDLargeItemStyle = new GUIStyle("DD LargeItemStyle");

        // Default Text Styles
        public static readonly GUIStyle DefaultCenteredLargeText = new GUIStyle("DefaultCenteredLargeText");
        public static readonly GUIStyle DefaultCenteredText = new GUIStyle("DefaultCenteredText");
        public static readonly GUIStyle DefaultLineSeparator = new GUIStyle("DefaultLineSeparator");

        // Dock Area Styles
        public static readonly GUIStyle DockareaOverlay = new GUIStyle("dockareaOverlay");
        public static readonly GUIStyle DockareaStandalone = new GUIStyle("dockareaStandalone");
        public static readonly GUIStyle Dockarea = new GUIStyle("dockarea");
        public static readonly GUIStyle DockHeader = new GUIStyle("dockHeader");

        // Dopesheet Styles
        public static readonly GUIStyle DopesheetBackground = new GUIStyle("DopesheetBackground");
        public static readonly GUIStyle Dopesheetkeyframe = new GUIStyle("Dopesheetkeyframe");
        public static readonly GUIStyle DopesheetRippleLeft = new GUIStyle("DopesheetRippleLeft");
        public static readonly GUIStyle DopesheetRippleRight = new GUIStyle("DopesheetRippleRight");
        public static readonly GUIStyle DopesheetScaleLeft = new GUIStyle("DopesheetScaleLeft");
        public static readonly GUIStyle DopesheetScaleRight = new GUIStyle("DopesheetScaleRight");

        // Drag Tab Styles
        public static readonly GUIStyle DragtabFirst = new GUIStyle("dragtab first");
        public static readonly GUIStyle DragtabScrollerNext = new GUIStyle("dragtab scroller next");
        public static readonly GUIStyle DragtabScrollerPrev = new GUIStyle("dragtab scroller prev");
        public static readonly GUIStyle Dragtabdropwindow = new GUIStyle("dragtabdropwindow");
        public static readonly GUIStyle Dragtab = new GUIStyle("dragtab");

        // Dropdown Styles
        public static readonly GUIStyle DropDownButton = new GUIStyle("DropDownButton");
        public static readonly GUIStyle DropDownToggleButton = new GUIStyle("DropDownToggleButton");
        public static readonly GUIStyle DropDown = new GUIStyle("DropDown");
        public static readonly GUIStyle DropzoneStyle = new GUIStyle("DropzoneStyle");

        // Edit Mode Styles
        public static readonly GUIStyle EditModeSingleButton = new GUIStyle("EditModeSingleButton");
        public static readonly GUIStyle ErrorLabel = new GUIStyle("ErrorLabel");

        // Exposable Popup Styles
        public static readonly GUIStyle ExposablePopupItem = new GUIStyle("ExposablePopupItem");
        public static readonly GUIStyle ExposablePopupMenu = new GUIStyle("ExposablePopupMenu");

        // Eye Dropper Styles
        public static readonly GUIStyle EyeDropperHorizontalLine = new GUIStyle("EyeDropperHorizontalLine");
        public static readonly GUIStyle EyeDropperPickedPixel = new GUIStyle("EyeDropperPickedPixel");
        public static readonly GUIStyle EyeDropperVerticalLine = new GUIStyle("EyeDropperVerticalLine");

        // Flow Related Styles
        public static readonly GUIStyle FloatFieldLinkButton = new GUIStyle("FloatFieldLinkButton");
        public static readonly GUIStyle FlowBackground = new GUIStyle("flow background");
        public static readonly GUIStyle FlowNode0On = new GUIStyle("flow node 0 on");
        public static readonly GUIStyle FlowNode0 = new GUIStyle("flow node 0");
        public static readonly GUIStyle FlowNode1On = new GUIStyle("flow node 1 on");
        public static readonly GUIStyle FlowNode1 = new GUIStyle("flow node 1");
        public static readonly GUIStyle FlowNode2On = new GUIStyle("flow node 2 on");
        public static readonly GUIStyle FlowNode2 = new GUIStyle("flow node 2");
        public static readonly GUIStyle FlowNode3On = new GUIStyle("flow node 3 on");
        public static readonly GUIStyle FlowNode3 = new GUIStyle("flow node 3");
        public static readonly GUIStyle FlowNode4On = new GUIStyle("flow node 4 on");
        public static readonly GUIStyle FlowNode4 = new GUIStyle("flow node 4");
        public static readonly GUIStyle FlowNode5On = new GUIStyle("flow node 5 on");
        public static readonly GUIStyle FlowNode5 = new GUIStyle("flow node 5");
        public static readonly GUIStyle FlowNode6On = new GUIStyle("flow node 6 on");
        public static readonly GUIStyle FlowNode6 = new GUIStyle("flow node 6");
        public static readonly GUIStyle FlowNodeBase = new GUIStyle("flow node base");

        // Flow Node Hex Styles
        public static readonly GUIStyle FlowNodeHex0On = new GUIStyle("flow node hex 0 on");
        public static readonly GUIStyle FlowNodeHex0 = new GUIStyle("flow node hex 0");
        public static readonly GUIStyle FlowNodeHex1On = new GUIStyle("flow node hex 1 on");
        public static readonly GUIStyle FlowNodeHex1 = new GUIStyle("flow node hex 1");
        public static readonly GUIStyle FlowNodeHex2On = new GUIStyle("flow node hex 2 on");
        public static readonly GUIStyle FlowNodeHex2 = new GUIStyle("flow node hex 2");
        public static readonly GUIStyle FlowNodeHex3On = new GUIStyle("flow node hex 3 on");
        public static readonly GUIStyle FlowNodeHex3 = new GUIStyle("flow node hex 3");
        public static readonly GUIStyle FlowNodeHex4On = new GUIStyle("flow node hex 4 on");
        public static readonly GUIStyle FlowNodeHex4 = new GUIStyle("flow node hex 4");
        public static readonly GUIStyle FlowNodeHex5On = new GUIStyle("flow node hex 5 on");
        public static readonly GUIStyle FlowNodeHex5 = new GUIStyle("flow node hex 5");
        public static readonly GUIStyle FlowNodeHex6On = new GUIStyle("flow node hex 6 on");
        public static readonly GUIStyle FlowNodeHex6 = new GUIStyle("flow node hex 6");
        public static readonly GUIStyle FlowNodeHexBase = new GUIStyle("flow node hex base");
        
        // Flow Additional Styles
        public static readonly GUIStyle FlowNodeTitlebar = new GUIStyle("flow node titlebar");
        public static readonly GUIStyle FlowTargetIn = new GUIStyle("flow target in");
        public static readonly GUIStyle FlowTriggerPinIn = new GUIStyle("flow triggerPin in");
        public static readonly GUIStyle FlowTriggerPinOut = new GUIStyle("flow triggerPin out");
        public static readonly GUIStyle FlowVarPinIn = new GUIStyle("flow varPin in");
        public static readonly GUIStyle FlowVarPinOut = new GUIStyle("flow varPin out");
        public static readonly GUIStyle FlowVarPinTooltip = new GUIStyle("flow varPin tooltip");

        // Foldout Styles
        public static readonly GUIStyle FoldoutHeaderIcon = new GUIStyle("FoldoutHeaderIcon");
        public static readonly GUIStyle FoldoutHeader = new GUIStyle("FoldoutHeader");
        public static readonly GUIStyle FoldOutPreDrop = new GUIStyle("FoldOutPreDrop");
        public static readonly GUIStyle Foldout = new GUIStyle("Foldout");

        // Frame Styles
        public static readonly GUIStyle FrameBox = new GUIStyle("FrameBox");
        public static readonly GUIStyle Frame = new GUIStyle("Frame");

        // Game View Styles
        public static readonly GUIStyle GameViewBackground = new GUIStyle("GameViewBackground");

        // Gradient Styles
        public static readonly GUIStyle GradDownSwatchOverlay = new GUIStyle("Grad Down Swatch Overlay");
        public static readonly GUIStyle GradDownSwatch = new GUIStyle("Grad Down Swatch");
        public static readonly GUIStyle GradUpSwatchOverlay = new GUIStyle("Grad Up Swatch Overlay");
        public static readonly GUIStyle GradUpSwatch = new GUIStyle("Grad Up Swatch");

        // Grid and Border Styles
        public static readonly GUIStyle GreyBorder = new GUIStyle("grey_border");
        public static readonly GUIStyle GridListText = new GUIStyle("GridListText");
        public static readonly GUIStyle GridList = new GUIStyle("GridList");
        public static readonly GUIStyle GroupBox = new GUIStyle("GroupBox");

        // GUI Editor Styles
        public static readonly GUIStyle GUIEditorBreadcrumbLeftBackground = new GUIStyle("GUIEditor.BreadcrumbLeftBackground");
        public static readonly GUIStyle GUIEditorBreadcrumbLeft = new GUIStyle("GUIEditor.BreadcrumbLeft");
        public static readonly GUIStyle GUIEditorBreadcrumbMidBackground = new GUIStyle("GUIEditor.BreadcrumbMidBackground");
        public static readonly GUIStyle GUIEditorBreadcrumbMid = new GUIStyle("GUIEditor.BreadcrumbMid");

        // Gizmo Styles
        public static readonly GUIStyle GVGizmoDropDown = new GUIStyle("GV Gizmo DropDown");

        // Header Styles
        public static readonly GUIStyle HeaderButton = new GUIStyle("HeaderButton");
        public static readonly GUIStyle HeaderLabel = new GUIStyle("HeaderLabel");
        public static readonly GUIStyle HelpBox = new GUIStyle("HelpBox");
        public static readonly GUIStyle HiLabel = new GUIStyle("Hi Label");

        // Horizontal Slider Styles
        public static readonly GUIStyle HorizontalMinMaxScrollbarThumb = new GUIStyle("HorizontalMinMaxScrollbarThumb");
        public static readonly GUIStyle HorizontalSliderThumbExtent = new GUIStyle("HorizontalSliderThumbExtent");

        // Host View Styles
        public static readonly GUIStyle HostView = new GUIStyle("hostview");
        public static readonly GUIStyle HoverHighlight = new GUIStyle("HoverHighlight");

        // Icon Styles
        public static readonly GUIStyle IconButton = new GUIStyle("IconButton");

        // Inspector (IN) Styles
        public static readonly GUIStyle INBigTitleInner = new GUIStyle("IN BigTitle Inner");
        public static readonly GUIStyle INBigTitlePost = new GUIStyle("IN BigTitle Post");
        public static readonly GUIStyle INBigTitle = new GUIStyle("IN BigTitle");
        public static readonly GUIStyle INCenteredLabel = new GUIStyle("IN CenteredLabel");
        public static readonly GUIStyle INDropDown = new GUIStyle("IN DropDown");
        public static readonly GUIStyle INEditColliderButton = new GUIStyle("IN EditColliderButton");
        public static readonly GUIStyle INFoldout = new GUIStyle("IN Foldout");
        public static readonly GUIStyle INFooter = new GUIStyle("IN Footer");
        public static readonly GUIStyle INLabel = new GUIStyle("IN Label");
        public static readonly GUIStyle INLockButton = new GUIStyle("IN LockButton");
        public static readonly GUIStyle INMinMaxStateDropDown = new GUIStyle("IN MinMaxStateDropDown");
        public static readonly GUIStyle INObjectField = new GUIStyle("IN ObjectField");
        public static readonly GUIStyle INTextField = new GUIStyle("IN TextField");
        public static readonly GUIStyle INThumbnailSelection = new GUIStyle("IN ThumbnailSelection");
        public static readonly GUIStyle INThumbnailShadow = new GUIStyle("IN ThumbnailShadow");
        public static readonly GUIStyle INTitleFlat = new GUIStyle("IN Title Flat");
        public static readonly GUIStyle INTitleText = new GUIStyle("IN TitleText");
        public static readonly GUIStyle INTitle = new GUIStyle("IN Title");
        public static readonly GUIStyle INTypeSelection = new GUIStyle("IN TypeSelection");

        // Inner Shadow Styles
        public static readonly GUIStyle InnerShadowBg = new GUIStyle("InnerShadowBg");
        public static readonly GUIStyle InsertionMarker = new GUIStyle("InsertionMarker");
        public static readonly GUIStyle InvisibleButton = new GUIStyle("InvisibleButton");
        
        // Label Styles
        public static readonly GUIStyle LargeBoldLabel = new GUIStyle("LargeBoldLabel");
        public static readonly GUIStyle LargeLabel = new GUIStyle("LargeLabel");

        // Large Button Styles
        public static readonly GUIStyle LargeButtonLeft = new GUIStyle("LargeButtonLeft");
        public static readonly GUIStyle LargeButtonMid = new GUIStyle("LargeButtonMid");
        public static readonly GUIStyle LargeButtonRight = new GUIStyle("LargeButtonRight");
        public static readonly GUIStyle LargeButton = new GUIStyle("LargeButton");

        // Lightmap Editor Styles
        public static readonly GUIStyle LightmapEditorSelectedHighlight = new GUIStyle("LightmapEditorSelectedHighlight");
        public static readonly GUIStyle LinkLabel = new GUIStyle("LinkLabel");

        // LOD Related Styles
        public static readonly GUIStyle LODBlackBox = new GUIStyle("LODBlackBox");
        public static readonly GUIStyle LODCameraLine = new GUIStyle("LODCameraLine");
        public static readonly GUIStyle LODLevelNotifyText = new GUIStyle("LODLevelNotifyText");
        public static readonly GUIStyle LODRendererAddButton = new GUIStyle("LODRendererAddButton");
        public static readonly GUIStyle LODRendererButton = new GUIStyle("LODRendererButton");
        public static readonly GUIStyle LODRendererRemove = new GUIStyle("LODRendererRemove");
        public static readonly GUIStyle LODRenderersText = new GUIStyle("LODRenderersText");
        public static readonly GUIStyle LODSceneText = new GUIStyle("LODSceneText");
        public static readonly GUIStyle LODSliderBG = new GUIStyle("LODSliderBG");
        public static readonly GUIStyle LODSliderRangeSelected = new GUIStyle("LODSliderRangeSelected");
        public static readonly GUIStyle LODSliderRange = new GUIStyle("LODSliderRange");
        public static readonly GUIStyle LODSliderTextSelected = new GUIStyle("LODSliderTextSelected");
        public static readonly GUIStyle LODSliderText = new GUIStyle("LODSliderText");

        // ME (Motion Editor) Styles
        public static readonly GUIStyle MeBlendBackground = new GUIStyle("MeBlendBackground");
        public static readonly GUIStyle MeBlendPosition = new GUIStyle("MeBlendPosition");
        public static readonly GUIStyle MeBlendTriangleLeft = new GUIStyle("MeBlendTriangleLeft");
        public static readonly GUIStyle MeBlendTriangleRight = new GUIStyle("MeBlendTriangleRight");
        public static readonly GUIStyle MeLivePlayBackground = new GUIStyle("MeLivePlayBackground");
        public static readonly GUIStyle MeLivePlayBar = new GUIStyle("MeLivePlayBar");
        public static readonly GUIStyle MenuItemMixed = new GUIStyle("MenuItemMixed");
        public static readonly GUIStyle MenuItem = new GUIStyle("MenuItem");
        public static readonly GUIStyle MenuToggleItem = new GUIStyle("MenuToggleItem");
        public static readonly GUIStyle MeTimeBlockLeft = new GUIStyle("MeTimeBlockLeft");
        public static readonly GUIStyle MeTimeBlockRight = new GUIStyle("MeTimeBlockRight");
        public static readonly GUIStyle MeTimeLabel = new GUIStyle("MeTimeLabel");
        public static readonly GUIStyle MeTransitionBack = new GUIStyle("MeTransitionBack");
        public static readonly GUIStyle MeTransitionBlock = new GUIStyle("MeTransitionBlock");
        public static readonly GUIStyle MeTransitionHandleLeftPrev = new GUIStyle("MeTransitionHandleLeftPrev");
        public static readonly GUIStyle MeTransitionHandleLeft = new GUIStyle("MeTransitionHandleLeft");
        public static readonly GUIStyle MeTransitionHandleRight = new GUIStyle("MeTransitionHandleRight");
        public static readonly GUIStyle MeTransitionHead = new GUIStyle("MeTransitionHead");
        public static readonly GUIStyle MeTransitionSelectHead = new GUIStyle("MeTransitionSelectHead");
        public static readonly GUIStyle MeTransitionSelect = new GUIStyle("MeTransitionSelect");
        public static readonly GUIStyle MeTransOff2On = new GUIStyle("MeTransOff2On");
        public static readonly GUIStyle MeTransOffLeft = new GUIStyle("MeTransOffLeft");
        public static readonly GUIStyle MeTransOffRight = new GUIStyle("MeTransOffRight");
        public static readonly GUIStyle MeTransOn2Off = new GUIStyle("MeTransOn2Off");
        public static readonly GUIStyle MeTransOnLeft = new GUIStyle("MeTransOnLeft");
        public static readonly GUIStyle MeTransOnRight = new GUIStyle("MeTransOnRight");
        public static readonly GUIStyle MeTransPlayhead = new GUIStyle("MeTransPlayhead");
        
        // MinMax Slider Styles
        public static readonly GUIStyle MinMaxHorizontalSliderThumb = new GUIStyle("MinMaxHorizontalSliderThumb");
        
        // Mini Styles
        public static readonly GUIStyle MiniBoldLabel = new GUIStyle("MiniBoldLabel");
        public static readonly GUIStyle MiniButtonLeft = new GUIStyle("minibuttonleft");
        public static readonly GUIStyle MiniButtonMid = new GUIStyle("minibuttonmid");
        public static readonly GUIStyle MiniButtonRight = new GUIStyle("minibuttonright");
        public static readonly GUIStyle MiniButton = new GUIStyle("minibutton");
        public static readonly GUIStyle MiniLabel = new GUIStyle("MiniLabel");
        public static readonly GUIStyle MiniMinMaxSliderHorizontal = new GUIStyle("MiniMinMaxSliderHorizontal");
        public static readonly GUIStyle MiniMinMaxSliderVertical = new GUIStyle("MiniMinMaxSliderVertical");
        public static readonly GUIStyle MiniPopup = new GUIStyle("MiniPopup");
        public static readonly GUIStyle MiniPullDown = new GUIStyle("MiniPullDown");
        public static readonly GUIStyle MiniSliderHorizontal = new GUIStyle("MiniSliderHorizontal");
        public static readonly GUIStyle MiniSliderVertical = new GUIStyle("MiniSliderVertical");
        public static readonly GUIStyle MiniTextField = new GUIStyle("MiniTextField");
        public static readonly GUIStyle MiniToolbarButtonLeft = new GUIStyle("MiniToolbarButtonLeft");
        public static readonly GUIStyle MiniToolbarButton = new GUIStyle("MiniToolbarButton");

        // MultiColumn Styles
        public static readonly GUIStyle MultiColumnArrow = new GUIStyle("MultiColumnArrow");
        public static readonly GUIStyle MultiColumnHeaderCenter = new GUIStyle("MultiColumnHeaderCenter");
        public static readonly GUIStyle MultiColumnHeaderRight = new GUIStyle("MultiColumnHeaderRight");
        public static readonly GUIStyle MultiColumnHeader = new GUIStyle("MultiColumnHeader");
        public static readonly GUIStyle MultiColumnTopBar = new GUIStyle("MultiColumnTopBar");

        // Mute Toggle Style
        public static readonly GUIStyle MuteToggle = new GUIStyle("MuteToggle");

        // Notification Styles
        public static readonly GUIStyle NotificationBackground = new GUIStyle("NotificationBackground");
        public static readonly GUIStyle NotificationText = new GUIStyle("NotificationText");

        // Object Field Styles
        public static readonly GUIStyle ObjectFieldButton = new GUIStyle("ObjectFieldButton");
        public static readonly GUIStyle ObjectFieldMiniThumb = new GUIStyle("ObjectFieldMiniThumb");
        public static readonly GUIStyle ObjectFieldThumbLightmapPreviewOverlay = new GUIStyle("ObjectFieldThumbLightmapPreviewOverlay");
        public static readonly GUIStyle ObjectFieldThumbOverlay2 = new GUIStyle("ObjectFieldThumbOverlay2");
        public static readonly GUIStyle ObjectFieldThumbOverlay = new GUIStyle("ObjectFieldThumbOverlay");
        public static readonly GUIStyle ObjectFieldThumb = new GUIStyle("ObjectFieldThumb");
        public static readonly GUIStyle ObjectField = new GUIStyle("ObjectField");

        // Object Picker Styles
        public static readonly GUIStyle ObjectPickerBackground = new GUIStyle("ObjectPickerBackground");
        public static readonly GUIStyle ObjectPickerLargeStatus = new GUIStyle("ObjectPickerLargeStatus");
        public static readonly GUIStyle ObjectPickerPreviewBackground = new GUIStyle("ObjectPickerPreviewBackground");
        public static readonly GUIStyle ObjectPickerResultsEven = new GUIStyle("ObjectPickerResultsEven");
        public static readonly GUIStyle ObjectPickerResultsGrid = new GUIStyle("ObjectPickerResultsGrid");
        public static readonly GUIStyle ObjectPickerResultsOdd = new GUIStyle("ObjectPickerResultsOdd");
        public static readonly GUIStyle ObjectPickerSmallStatus = new GUIStyle("ObjectPickerSmallStatus");
        public static readonly GUIStyle ObjectPickerTab = new GUIStyle("ObjectPickerTab");
        public static readonly GUIStyle ObjectPickerToolbar = new GUIStyle("ObjectPickerToolbar");

        // Offset Styles
        public static readonly GUIStyle OffsetDropDown = new GUIStyle("OffsetDropDown");
        
        // Object List (OL) Styles
        public static readonly GUIStyle OLBoxFlat = new GUIStyle("OL box flat");
        public static readonly GUIStyle OLBoxNoExpand = new GUIStyle("OL box NoExpand");
        public static readonly GUIStyle OLBox = new GUIStyle("OL box");
        public static readonly GUIStyle OLEntryBackEven = new GUIStyle("OL EntryBackEven");
        public static readonly GUIStyle OLEntryBackOdd = new GUIStyle("OL EntryBackOdd");
        public static readonly GUIStyle OLLabel = new GUIStyle("OL Label");
        public static readonly GUIStyle OLMiniPing = new GUIStyle("OL MiniPing");
        public static readonly GUIStyle OLMiniRenameField = new GUIStyle("OL MiniRenameField");
        public static readonly GUIStyle OLMinus = new GUIStyle("OL Minus");
        public static readonly GUIStyle OLPing = new GUIStyle("OL Ping");
        public static readonly GUIStyle OLPlus = new GUIStyle("OL Plus");
        public static readonly GUIStyle OLResultFocusMarker = new GUIStyle("OL ResultFocusMarker");
        public static readonly GUIStyle OLResultLabel = new GUIStyle("OL ResultLabel");
        public static readonly GUIStyle OLRightLabel = new GUIStyle("OL RightLabel");
        public static readonly GUIStyle OLSelectedRow = new GUIStyle("OL SelectedRow");
        public static readonly GUIStyle OLTitleTextRight = new GUIStyle("OL Title TextRight");
        public static readonly GUIStyle OLTitle = new GUIStyle("OL Title");
        public static readonly GUIStyle OLToggleMixed = new GUIStyle("OL ToggleMixed");
        public static readonly GUIStyle OLToggleWhite = new GUIStyle("OL ToggleWhite");
        public static readonly GUIStyle OLToggle = new GUIStyle("OL Toggle");

        // Object Tab (OT) Styles
        public static readonly GUIStyle OTBottomBar = new GUIStyle("OT BottomBar");
        public static readonly GUIStyle OTTopBar = new GUIStyle("OT TopBar");

        // Override Styles
        public static readonly GUIStyle OverrideMargin = new GUIStyle("OverrideMargin");

        // Panel Styles
        public static readonly GUIStyle PaneOptions = new GUIStyle("PaneOptions");

        // Player Settings Styles
        public static readonly GUIStyle PlayerSettingsLevel = new GUIStyle("PlayerSettingsLevel");
        public static readonly GUIStyle PlayerSettingsPlatform = new GUIStyle("PlayerSettingsPlatform");

        // Popup Styles
        public static readonly GUIStyle PopupCurveDropdown = new GUIStyle("PopupCurveDropdown");
        public static readonly GUIStyle PopupCurveEditorBackground = new GUIStyle("PopupCurveEditorBackground");
        public static readonly GUIStyle PopupCurveEditorSwatch = new GUIStyle("PopupCurveEditorSwatch");
        public static readonly GUIStyle PopupCurveSwatchBackground = new GUIStyle("PopupCurveSwatchBackground");
        public static readonly GUIStyle Popup = new GUIStyle("Popup");

        // Prefab Related (PR) Styles
        public static readonly GUIStyle PRBrokenPrefabLabel = new GUIStyle("PR BrokenPrefabLabel");
        public static readonly GUIStyle PRDisabledBrokenPrefabLabel = new GUIStyle("PR DisabledBrokenPrefabLabel");
        public static readonly GUIStyle PRDisabledLabel = new GUIStyle("PR DisabledLabel");
        public static readonly GUIStyle PRDisabledPrefabLabel = new GUIStyle("PR DisabledPrefabLabel");
        public static readonly GUIStyle PRInsertion = new GUIStyle("PR Insertion");
        public static readonly GUIStyle PRLabel = new GUIStyle("PR Label");
        public static readonly GUIStyle PRPing = new GUIStyle("PR Ping");
        public static readonly GUIStyle PRPrefabLabel = new GUIStyle("PR PrefabLabel");
        public static readonly GUIStyle PRTextField = new GUIStyle("PR TextField");

        // Pre Styles
        public static readonly GUIStyle PreBackgroundSolid = new GUIStyle("PreBackgroundSolid");
        public static readonly GUIStyle PreBackground = new GUIStyle("PreBackground");
        public static readonly GUIStyle PreButtonBlue = new GUIStyle("PreButtonBlue");
        public static readonly GUIStyle PreButtonGreen = new GUIStyle("PreButtonGreen");
        public static readonly GUIStyle PreButtonRed = new GUIStyle("PreButtonRed");
        public static readonly GUIStyle PreButton = new GUIStyle("PreButton");
        public static readonly GUIStyle PreDropDown = new GUIStyle("PreDropDown");

        // Preferences Styles
        public static readonly GUIStyle PreferencesKeysElement = new GUIStyle("PreferencesKeysElement");
        public static readonly GUIStyle PreferencesSectionBox = new GUIStyle("PreferencesSectionBox");
        public static readonly GUIStyle PreferencesSection = new GUIStyle("PreferencesSection");
        
        // Prefix and Label Styles
        public static readonly GUIStyle PrefixLabel = new GUIStyle("PrefixLabel");
        public static readonly GUIStyle PreHorizontalScrollbarThumb = new GUIStyle("PreHorizontalScrollbarThumb");
        public static readonly GUIStyle PreHorizontalScrollbar = new GUIStyle("PreHorizontalScrollbar");
        public static readonly GUIStyle PreLabelUpper = new GUIStyle("PreLabelUpper");
        public static readonly GUIStyle PreLabel = new GUIStyle("PreLabel");
        public static readonly GUIStyle PreMiniLabel = new GUIStyle("PreMiniLabel");
        public static readonly GUIStyle PreOverlayLabel = new GUIStyle("PreOverlayLabel");
        public static readonly GUIStyle PreSliderThumb = new GUIStyle("PreSliderThumb");
        public static readonly GUIStyle PreSlider = new GUIStyle("PreSlider");
        public static readonly GUIStyle PreToolbar2 = new GUIStyle("PreToolbar2");
        public static readonly GUIStyle PreToolbar = new GUIStyle("PreToolbar");
        public static readonly GUIStyle PreVerticalScrollbarThumb = new GUIStyle("PreVerticalScrollbarThumb");
        public static readonly GUIStyle PreVerticalScrollbar = new GUIStyle("PreVerticalScrollbar");

        // Preview Styles
        public static readonly GUIStyle PreviewPackageInUse = new GUIStyle("PreviewPackageInUse");

        // Profiler Styles
        public static readonly GUIStyle ProfilerBadge = new GUIStyle("ProfilerBadge");
        public static readonly GUIStyle ProfilerDetailViewBackground = new GUIStyle("ProfilerDetailViewBackground");
        public static readonly GUIStyle ProfilerGraphBackground = new GUIStyle("ProfilerGraphBackground");
        public static readonly GUIStyle ProfilerHeaderLabel = new GUIStyle("ProfilerHeaderLabel");
        public static readonly GUIStyle ProfilerLeftPane = new GUIStyle("ProfilerLeftPane");
        public static readonly GUIStyle ProfilerNoDataAvailable = new GUIStyle("ProfilerNoDataAvailable");
        public static readonly GUIStyle ProfilerNotSupportedWarningLabel = new GUIStyle("ProfilerNotSupportedWarningLabel");
        public static readonly GUIStyle ProfilerPaneSubLabel = new GUIStyle("ProfilerPaneSubLabel");
        public static readonly GUIStyle ProfilerRightPane = new GUIStyle("ProfilerRightPane");
        public static readonly GUIStyle ProfilerScrollViewBackground = new GUIStyle("ProfilerScrollviewBackground");
        public static readonly GUIStyle ProfilerSelectedLabel = new GUIStyle("ProfilerSelectedLabel");
        public static readonly GUIStyle ProfilerTimelineBar = new GUIStyle("ProfilerTimelineBar");
        public static readonly GUIStyle ProfilerTimelineDigDownArrow = new GUIStyle("ProfilerTimelineDigDownArrow");
        public static readonly GUIStyle ProfilerTimelineFoldout = new GUIStyle("ProfilerTimelineFoldout");
        public static readonly GUIStyle ProfilerTimelineLeftPane = new GUIStyle("ProfilerTimelineLeftPane");
        public static readonly GUIStyle ProfilerTimelineRollUpArrow = new GUIStyle("ProfilerTimelineRollUpArrow");

        // Progress Bar Styles
        public static readonly GUIStyle ProgressBarBack = new GUIStyle("ProgressBarBack");
        public static readonly GUIStyle ProgressBarBar = new GUIStyle("ProgressBarBar");
        public static readonly GUIStyle ProgressBarText = new GUIStyle("ProgressBarText");

        // Project Browser Styles
        public static readonly GUIStyle ProjectBrowserBottomBarBg = new GUIStyle("ProjectBrowserBottomBarBg");
        public static readonly GUIStyle ProjectBrowserGridLabel = new GUIStyle("ProjectBrowserGridLabel");
        public static readonly GUIStyle ProjectBrowserHeaderBgMiddle = new GUIStyle("ProjectBrowserHeaderBgMiddle");
        public static readonly GUIStyle ProjectBrowserHeaderBgTop = new GUIStyle("ProjectBrowserHeaderBgTop");
        public static readonly GUIStyle ProjectBrowserIconAreaBg = new GUIStyle("ProjectBrowserIconAreaBg");
        public static readonly GUIStyle ProjectBrowserIconDropShadow = new GUIStyle("ProjectBrowserIconDropShadow");
        public static readonly GUIStyle ProjectBrowserPreviewBg = new GUIStyle("ProjectBrowserPreviewBg");
        public static readonly GUIStyle ProjectBrowserSubAssetBgCloseEnded = new GUIStyle("ProjectBrowserSubAssetBgCloseEnded");
        public static readonly GUIStyle ProjectBrowserSubAssetBgDivider = new GUIStyle("ProjectBrowserSubAssetBgDivider");
        public static readonly GUIStyle ProjectBrowserSubAssetBgMiddle = new GUIStyle("ProjectBrowserSubAssetBgMiddle");
        public static readonly GUIStyle ProjectBrowserSubAssetBgOpenEnded = new GUIStyle("ProjectBrowserSubAssetBgOpenEnded");
        public static readonly GUIStyle ProjectBrowserSubAssetBg = new GUIStyle("ProjectBrowserSubAssetBg");
        public static readonly GUIStyle ProjectBrowserSubAssetExpandBtnMedium = new GUIStyle("ProjectBrowserSubAssetExpandBtnMedium");
        public static readonly GUIStyle ProjectBrowserSubAssetExpandBtnSmall = new GUIStyle("ProjectBrowserSubAssetExpandBtnSmall");
        public static readonly GUIStyle ProjectBrowserSubAssetExpandBtn = new GUIStyle("ProjectBrowserSubAssetExpandBtn");
        public static readonly GUIStyle ProjectBrowserTextureIconDropShadow = new GUIStyle("ProjectBrowserTextureIconDropShadow");
        public static readonly GUIStyle ProjectBrowserTopBarBg = new GUIStyle("ProjectBrowserTopBarBg");

        // Quality Settings Styles
        public static readonly GUIStyle QualitySettingsDefault = new GUIStyle("QualitySettingsDefault");

        // Quick Search Styles
        public static readonly GUIStyle QuickSearchTab = new GUIStyle("quick search tab");

        // Radio Button Styles
        public static readonly GUIStyle Radio = new GUIStyle("Radio");

        // Rectangle Tool Styles
        public static readonly GUIStyle RectangleToolHBarLeft = new GUIStyle("RectangleToolHBarLeft");
        public static readonly GUIStyle RectangleToolHBarRight = new GUIStyle("RectangleToolHBarRight");
        public static readonly GUIStyle RectangleToolHBar = new GUIStyle("RectangleToolHBar");
        public static readonly GUIStyle RectangleToolHighlight = new GUIStyle("RectangleToolHighlight");
        public static readonly GUIStyle RectangleToolRippleLeft = new GUIStyle("RectangleToolRippleLeft");
        public static readonly GUIStyle RectangleToolRippleRight = new GUIStyle("RectangleToolRippleRight");
        public static readonly GUIStyle RectangleToolScaleBottom = new GUIStyle("RectangleToolScaleBottom");
        public static readonly GUIStyle RectangleToolScaleLeft = new GUIStyle("RectangleToolScaleLeft");
        public static readonly GUIStyle RectangleToolScaleRight = new GUIStyle("RectangleToolScaleRight");
        public static readonly GUIStyle RectangleToolScaleTop = new GUIStyle("RectangleToolScaleTop");
        public static readonly GUIStyle RectangleToolSelection = new GUIStyle("RectangleToolSelection");
        public static readonly GUIStyle RectangleToolVBarBottom = new GUIStyle("RectangleToolVBarBottom");
        public static readonly GUIStyle RectangleToolVBarTop = new GUIStyle("RectangleToolVBarTop");
        public static readonly GUIStyle RectangleToolVBar = new GUIStyle("RectangleToolVBar");

        // Regions and List Styles
        public static readonly GUIStyle RegionBg = new GUIStyle("RegionBg");
        public static readonly GUIStyle ReorderableListRightAligned = new GUIStyle("ReorderableListRightAligned");
        public static readonly GUIStyle ReorderableList = new GUIStyle("ReorderableList");

        // Label Alignment Styles
        public static readonly GUIStyle RightAlignedLabel = new GUIStyle("RightAlignedLabel");
        public static readonly GUIStyle RightLabel = new GUIStyle("RightLabel");

        // Reorderable List (RL) Styles
        public static readonly GUIStyle RLBackground = new GUIStyle("RL Background");
        public static readonly GUIStyle RLDragHandle = new GUIStyle("RL DragHandle");
        public static readonly GUIStyle RLElement = new GUIStyle("RL Element");
        public static readonly GUIStyle RLEmptyHeader = new GUIStyle("RL Empty Header");
        public static readonly GUIStyle RLFooterButton = new GUIStyle("RL FooterButton");
        public static readonly GUIStyle RLFooter = new GUIStyle("RL Footer");
        public static readonly GUIStyle RLHeader = new GUIStyle("RL Header");

        // Scene View (SC) Styles
        public static readonly GUIStyle SCViewAxisLabel = new GUIStyle("SC ViewAxisLabel");
        public static readonly GUIStyle SCViewLabelCentered = new GUIStyle("SC ViewLabelCentered");
        public static readonly GUIStyle SCViewLabelLeftAligned = new GUIStyle("SC ViewLabelLeftAligned");
        public static readonly GUIStyle SCViewLabel = new GUIStyle("SC ViewLabel");

        // Scene Related Styles
        public static readonly GUIStyle SceneTopBarBg = new GUIStyle("SceneTopBarBg");
        public static readonly GUIStyle SceneViewOverlayTransparentBackground = new GUIStyle("SceneViewOverlayTransparentBackground");
        public static readonly GUIStyle SceneVisibility = new GUIStyle("SceneVisibility");

        // Script and Scroll View Styles
        public static readonly GUIStyle ScriptText = new GUIStyle("ScriptText");
        public static readonly GUIStyle ScrollViewAlt = new GUIStyle("ScrollViewAlt");

        // Search Related Styles
        public static readonly GUIStyle SearchCancelButtonEmpty = new GUIStyle("SearchCancelButtonEmpty");
        public static readonly GUIStyle SearchCancelButton = new GUIStyle("SearchCancelButton");
        public static readonly GUIStyle SearchModeFilter = new GUIStyle("SearchModeFilter");
        public static readonly GUIStyle SearchTextField = new GUIStyle("SearchTextField");

        // Selection and Settings Styles
        public static readonly GUIStyle SelectionRect = new GUIStyle("SelectionRect");
        public static readonly GUIStyle SettingsHeader = new GUIStyle("SettingsHeader");
        public static readonly GUIStyle SettingsIconButton = new GUIStyle("SettingsIconButton");
        public static readonly GUIStyle SettingsListItem = new GUIStyle("SettingsListItem");
        public static readonly GUIStyle SettingsTreeItem = new GUIStyle("SettingsTreeItem");

        // Shuriken Styles
        public static readonly GUIStyle ShurikenCheckMarkMixed = new GUIStyle("ShurikenCheckMarkMixed");
        public static readonly GUIStyle ShurikenCheckMark = new GUIStyle("ShurikenCheckMark");
        public static readonly GUIStyle ShurikenDropdown = new GUIStyle("ShurikenDropdown");
        public static readonly GUIStyle ShurikenEditableLabel = new GUIStyle("ShurikenEditableLabel");
        public static readonly GUIStyle ShurikenEffectBg = new GUIStyle("ShurikenEffectBg");
        public static readonly GUIStyle ShurikenEmitterTitle = new GUIStyle("ShurikenEmitterTitle");
        public static readonly GUIStyle ShurikenLabel = new GUIStyle("ShurikenLabel");
        public static readonly GUIStyle ShurikenMinus = new GUIStyle("ShurikenMinus");
        public static readonly GUIStyle ShurikenModuleBg = new GUIStyle("ShurikenModuleBg");
        public static readonly GUIStyle ShurikenModuleTitle = new GUIStyle("ShurikenModuleTitle");
        public static readonly GUIStyle ShurikenObjectField = new GUIStyle("ShurikenObjectField");
        public static readonly GUIStyle ShurikenPlus = new GUIStyle("ShurikenPlus");
        public static readonly GUIStyle ShurikenPopup = new GUIStyle("ShurikenPopup");
        public static readonly GUIStyle ShurikenToggleMixed = new GUIStyle("ShurikenToggleMixed");
        public static readonly GUIStyle ShurikenToggle = new GUIStyle("ShurikenToggle");
        public static readonly GUIStyle ShurikenValue = new GUIStyle("ShurikenValue");

        // Slider and Toggle Styles
        public static readonly GUIStyle SliderMixed = new GUIStyle("SliderMixed");
        public static readonly GUIStyle SoloToggle = new GUIStyle("SoloToggle");

        // Static UI Styles
        public static readonly GUIStyle StaticDropdown = new GUIStyle("StaticDropdown");
        public static readonly GUIStyle StatusBarIcon = new GUIStyle("StatusBarIcon");
        
        // Scene View Icon Selector Styles
        public static readonly GUIStyle SVIconSelectorBack = new GUIStyle("sv_iconselector_back");
        public static readonly GUIStyle SVIconSelectorButton = new GUIStyle("sv_iconselector_button");
        public static readonly GUIStyle SVIconSelectorLabelSelection = new GUIStyle("sv_iconselector_labelselection");
        public static readonly GUIStyle SVIconSelectorSelection = new GUIStyle("sv_iconselector_selection");
        public static readonly GUIStyle SVIconSelectorSep = new GUIStyle("sv_iconselector_sep");

        // Scene View Label Styles
        public static readonly GUIStyle SVLabel0 = new GUIStyle("sv_label_0");
        public static readonly GUIStyle SVLabel1 = new GUIStyle("sv_label_1");
        public static readonly GUIStyle SVLabel2 = new GUIStyle("sv_label_2");
        public static readonly GUIStyle SVLabel3 = new GUIStyle("sv_label_3");
        public static readonly GUIStyle SVLabel4 = new GUIStyle("sv_label_4");
        public static readonly GUIStyle SVLabel5 = new GUIStyle("sv_label_5");
        public static readonly GUIStyle SVLabel6 = new GUIStyle("sv_label_6");
        public static readonly GUIStyle SVLabel7 = new GUIStyle("sv_label_7");

        // Tab Styles
        public static readonly GUIStyle TabFirst = new GUIStyle("Tab first");
        public static readonly GUIStyle TabLast = new GUIStyle("Tab last");
        public static readonly GUIStyle TabMiddle = new GUIStyle("Tab middle");
        public static readonly GUIStyle TabOnlyOne = new GUIStyle("Tab onlyOne");
        public static readonly GUIStyle TabWindowBackground = new GUIStyle("TabWindowBackground");

        // Tag Styles
        public static readonly GUIStyle TagMenuItem = new GUIStyle("Tag MenuItem");

        // Timeline Editor (TE) Styles
        public static readonly GUIStyle TEBoxBackground = new GUIStyle("TE BoxBackground");
        public static readonly GUIStyle TEDefaultTime = new GUIStyle("TE DefaultTime");
        public static readonly GUIStyle TEDropField = new GUIStyle("TE DropField");
        public static readonly GUIStyle TEElementBackground = new GUIStyle("TE ElementBackground");
        public static readonly GUIStyle TENodeBackground = new GUIStyle("TE NodeBackground");
        public static readonly GUIStyle TENodeBoxSelected = new GUIStyle("TE NodeBoxSelected");
        public static readonly GUIStyle TENodeBox = new GUIStyle("TE NodeBox");
        public static readonly GUIStyle TENodeLabelBot = new GUIStyle("TE NodeLabelBot");
        public static readonly GUIStyle TENodeLabelTop = new GUIStyle("TE NodeLabelTop");
        public static readonly GUIStyle TEPinLabel = new GUIStyle("TE PinLabel");
        public static readonly GUIStyle TEToolbarbutton = new GUIStyle("TE toolbarbutton");
        public static readonly GUIStyle TEToolbarDropDown = new GUIStyle("TE ToolbarDropDown");
        public static readonly GUIStyle TEToolbar = new GUIStyle("TE Toolbar");
        
        // Text Field Styles
        public static readonly GUIStyle TextFieldDropDownText = new GUIStyle("TextFieldDropDownText");
        public static readonly GUIStyle TextFieldDropDown = new GUIStyle("TextFieldDropDown");

        // Time Related Styles
        public static readonly GUIStyle TimeAreaToolbar = new GUIStyle("TimeAreaToolbar");
        public static readonly GUIStyle TimeRulerBackground = new GUIStyle("TimeRulerBackground");
        public static readonly GUIStyle TimeScrubberButton = new GUIStyle("TimeScrubberButton");
        public static readonly GUIStyle TimeScrubber = new GUIStyle("TimeScrubber");
        
        // Title Bar Styles
        public static readonly GUIStyle TitlebarFoldout = new GUIStyle("Titlebar Foldout");

        // Timeline (TL) Styles
        public static readonly GUIStyle TLInPoint = new GUIStyle("TL InPoint");
        public static readonly GUIStyle TLOutPoint = new GUIStyle("TL OutPoint");
        public static readonly GUIStyle TLPlayhead = new GUIStyle("TL Playhead");

        // Toggle Styles
        public static readonly GUIStyle ToggleMixed = new GUIStyle("ToggleMixed");

        // Toolbar Styles
        public static readonly GUIStyle ToolbarBoldLabel = new GUIStyle("ToolbarBoldLabel");
        public static readonly GUIStyle ToolbarBottom = new GUIStyle("ToolbarBottom");
        public static readonly GUIStyle ToolbarButtonFlat = new GUIStyle("ToolbarButtonFlat");
        public static readonly GUIStyle ToolbarButtonLeft = new GUIStyle("toolbarbuttonLeft");
        public static readonly GUIStyle ToolbarButtonRight = new GUIStyle("toolbarbuttonRight");
        public static readonly GUIStyle ToolbarButton = new GUIStyle("toolbarbutton");
        public static readonly GUIStyle ToolbarCreateAddNewDropDown = new GUIStyle("ToolbarCreateAddNewDropDown");
        public static readonly GUIStyle ToolbarDropDownLeft = new GUIStyle("ToolbarDropDownLeft");
        public static readonly GUIStyle ToolbarDropDownRight = new GUIStyle("ToolbarDropDownRight");
        public static readonly GUIStyle ToolbarDropDownToggleButton = new GUIStyle("ToolbarDropDownToggleButton");
        public static readonly GUIStyle ToolbarDropDownToggleRight = new GUIStyle("ToolbarDropDownToggleRight");
        public static readonly GUIStyle ToolbarDropDownToggle = new GUIStyle("ToolbarDropDownToggle");
        public static readonly GUIStyle ToolbarDropDown = new GUIStyle("ToolbarDropDown");
        public static readonly GUIStyle ToolbarLabel = new GUIStyle("ToolbarLabel");
        public static readonly GUIStyle ToolbarPopupLeft = new GUIStyle("ToolbarPopupLeft");
        public static readonly GUIStyle ToolbarPopupRight = new GUIStyle("ToolbarPopupRight");
        public static readonly GUIStyle ToolbarPopup = new GUIStyle("ToolbarPopup");
        public static readonly GUIStyle ToolbarSearchCancelButtonWithJumpEmpty = new GUIStyle("ToolbarSearchCancelButtonWithJumpEmpty");
        public static readonly GUIStyle ToolbarSearchCancelButtonWithJump = new GUIStyle("ToolbarSearchCancelButtonWithJump");
        public static readonly GUIStyle ToolbarSearchField = new GUIStyle("ToolbarSearchField");
        public static readonly GUIStyle ToolbarSearchTextFieldJumpButton = new GUIStyle("ToolbarSearchTextFieldJumpButton");
        public static readonly GUIStyle ToolbarSearchTextFieldWithJumpPopupSynced = new GUIStyle("ToolbarSearchTextFieldWithJumpPopupSynced");
        public static readonly GUIStyle ToolbarSearchTextFieldWithJumpPopup = new GUIStyle("ToolbarSearchTextFieldWithJumpPopup");
        public static readonly GUIStyle ToolbarSearchTextFieldWithJumpSynced = new GUIStyle("ToolbarSearchTextFieldWithJumpSynced");
        public static readonly GUIStyle ToolbarSearchTextFieldWithJump = new GUIStyle("ToolbarSearchTextFieldWithJump");
        public static readonly GUIStyle ToolbarSliderTextField = new GUIStyle("ToolbarSliderTextField");
        public static readonly GUIStyle ToolbarSlider = new GUIStyle("ToolbarSlider");
        public static readonly GUIStyle ToolbarTextField = new GUIStyle("ToolbarTextField");
        public static readonly GUIStyle Toolbar = new GUIStyle("Toolbar");
        
        // Tooltip Styles
        public static readonly GUIStyle Tooltip = new GUIStyle("Tooltip");
        
        // Tree View (TV) Styles
        public static readonly GUIStyle TVInsertionRelativeToSibling = new GUIStyle("TV InsertionRelativeToSibling");
        public static readonly GUIStyle TVInsertion = new GUIStyle("TV Insertion");
        public static readonly GUIStyle TVLineBold = new GUIStyle("TV LineBold");
        public static readonly GUIStyle TVLine = new GUIStyle("TV Line");
        public static readonly GUIStyle TVPing = new GUIStyle("TV Ping");
        public static readonly GUIStyle TVSelection = new GUIStyle("TV Selection");

        // Unity 2D (U2D) Styles
        public static readonly GUIStyle U2DCreateRect = new GUIStyle("U2D.createRect");
        public static readonly GUIStyle U2DDragDotActive = new GUIStyle("U2D.dragDotActive");
        public static readonly GUIStyle U2DDragDotDimmed = new GUIStyle("U2D.dragDotDimmed");
        public static readonly GUIStyle U2DDragDot = new GUIStyle("U2D.dragDot");
        public static readonly GUIStyle U2DPivotDotActive = new GUIStyle("U2D.pivotDotActive");
        public static readonly GUIStyle U2DPivotDot = new GUIStyle("U2D.pivotDot");

        // Vertical Slider Styles
        public static readonly GUIStyle VerticalMinMaxScrollbarThumb = new GUIStyle("VerticalMinMaxScrollbarThumb");
        public static readonly GUIStyle VerticalSliderThumbExtent = new GUIStyle("VerticalSliderThumbExtent");

        // Video and Warning Styles
        public static readonly GUIStyle VideoClipImporterLabel = new GUIStyle("VideoClipImporterLabel");
        public static readonly GUIStyle WarningOverlay = new GUIStyle("WarningOverlay");

        // White Background and Label Styles
        public static readonly GUIStyle WhiteBackground = new GUIStyle("WhiteBackground");
        public static readonly GUIStyle WhiteBoldLabel = new GUIStyle("WhiteBoldLabel");
        public static readonly GUIStyle WhiteLabel = new GUIStyle("WhiteLabel");
        public static readonly GUIStyle WhiteLargeCenterLabel = new GUIStyle("WhiteLargeCenterLabel");
        public static readonly GUIStyle WhiteLargeLabel = new GUIStyle("WhiteLargeLabel");
        public static readonly GUIStyle WhiteMiniLabel = new GUIStyle("WhiteMiniLabel");
        
        // Window and Wizard Styles
        public static readonly GUIStyle WindowBottomResize = new GUIStyle("WindowBottomResize");
        public static readonly GUIStyle WizardBox = new GUIStyle("Wizard Box");
        public static readonly GUIStyle WizardError = new GUIStyle("Wizard Error");

        // Word Wrap Styles
        public static readonly GUIStyle WordWrapLabel = new GUIStyle("WordWrapLabel");
        public static readonly GUIStyle WordWrapMiniButton = new GUIStyle("wordwrapminibutton");
        public static readonly GUIStyle WordWrappedLabel = new GUIStyle("WordWrappedLabel");
        public static readonly GUIStyle WordWrappedMiniLabel = new GUIStyle("WordWrappedMiniLabel");
    }
}