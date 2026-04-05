using Frosty.Core;
using Frosty.Core.Controls;
using Frosty.Core.Windows;
using FrostySdk.Attributes;
using FrostySdk.Ebx;
using FrostySdk.Interfaces;
using FrostySdk.IO;
using FrostySdk.Managers;
using FrostySdk.Resources;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using UIBlueprintEditor.Editor.Text;

namespace UIBlueprintEditor.Editor
{
    [TemplatePart(Name = PART_SwitchView, Type = typeof(Button))]
    [TemplatePart(Name = PART_DefaultEditorLayer, Type = typeof(Grid))]
    [TemplatePart(Name = PART_UIEditorLayer, Type = typeof(Grid))]
    [TemplatePart(Name = PART_AddObject, Type = typeof(Button))]
    [TemplatePart(Name = PART_UISize, Type = typeof(Grid))]
    [TemplatePart(Name = PART_UICanvas, Type = typeof(Canvas))]
    [TemplatePart(Name = PART_Refresh, Type = typeof(Button))]
    [TemplatePart(Name = PART_Precise, Type = typeof(Button))]
    [TemplatePart(Name = PART_PreciseImage, Type = typeof(Button))]
    [TemplatePart(Name = PART_UIComponentInfo, Type = typeof(TextBlock))]
    [TemplatePart(Name = PART_Unhide, Type = typeof(Button))]
    [TemplatePart(Name = PART_UISizeText, Type = typeof(TextBlock))]
    [TemplatePart(Name = PART_ZoomPercent, Type = typeof(TextBlock))]
    [TemplatePart(Name = PART_AssetPropertyGrid, Type = typeof(FrostyPropertyGrid))]
    [TemplatePart(Name = PART_BackgroundGrid, Type = typeof(FrostyPropertyGrid))]
    public class UIEditor : FrostyAssetEditor
    {
        #region UI Parts
        private const string PART_SwitchView = "PART_SwitchView";
        private const string PART_DefaultEditorLayer = "PART_DefaultEditorLayer";
        private const string PART_UIEditorLayer = "PART_UIEditorLayer";
        private const string PART_AddObject = "PART_AddObject";
        private const string PART_UISize = "PART_UISize";
        private const string PART_TemplateUI = "PART_TemplateUI";
        private const string PART_UICanvas = "PART_UICanvas";
        private const string PART_Refresh = "PART_Refresh";
        private const string PART_Precise = "PART_Precise";
        private const string PART_PreciseImage = "PART_PreciseImage";
        private const string PART_UIComponentInfo = "PART_UIComponentInfo";
        private const string PART_Unhide = "PART_Unhide";
        private const string PART_UISizeText = "PART_UISizeText";
        private const string PART_ZoomPercent = "PART_ZoomPercent";
        private const string PART_AssetPropertyGrid = "PART_AssetPropertyGrid";
        private const string PART_BackgroundGrid = "PART_BackgroundGrid";

        private Button _switchViewButton;
        private FrameworkElement _uiEditorLayer;
        private FrameworkElement _defaultEditorLayer;
        private Button _addObjectButton;
        private FrameworkElement _uiSize;
        private TextBlock _zoomPercent;
        private Button _refreshButton;
        private Canvas _uiCanvas;
        private Button _preciseButton;
        private TextBlock _uiComponentInfo;
        private Button _unhideButton;
        private TextBlock _uiSizeText;
        private Image _preciseImage;
        private ImageBrush _backgroundGrid;

        private FrostyPropertyGrid _pgAsset;
        #endregion

        public static readonly bool debugging = true; // make this 'true' to have a lot of useful info logged

        // these dictionaries are used later to reference certain values using the TextureId as the key
        public static Dictionary<dynamic, dynamic> mappingUvRect = new Dictionary<dynamic, dynamic>();
        public static Dictionary<dynamic, dynamic> mappingMinValue = new Dictionary<dynamic, dynamic>();
        public static Dictionary<dynamic, dynamic> mappingMaxValue = new Dictionary<dynamic, dynamic>();
        public static Dictionary<dynamic, BitmapImage> mappingTexture = new Dictionary<dynamic, BitmapImage>();

        bool dragging = false;
        bool panning = false;

        TransformGroup transformGroup;
        ScaleTransform scaleTransform;
        TranslateTransform translateTransform;

        private bool isEditorActive = false;

        private Action<object> refreshPropertyGrid;

        static float VX(dynamic v)
        {
            try { return v.X; }
            catch { return v.x; }
        }

        static void SX(dynamic v, float value)
        {
            try { v.X = value; }
            catch { v.x = value; }
        }

        public UIEditor(ILogger inLogger) : base(inLogger)
        {
             App.Logger.Log(App.SelectedAsset.Type.ToString());

            // arrow key/WASD precise movement
            KeyDown += UICanvasKeyDown;

            // pan/zoom stuff
            MouseWheel += UICanvas_MouseWheel;
            MouseDown += UICanvas_MouseDown;
            MouseUp += UICanvas_MouseUp;
            MouseMove += UICanvas_MouseMove;

            transformGroup = new TransformGroup();
            scaleTransform = new ScaleTransform(1, 1);
            translateTransform = new TranslateTransform();

            transformGroup.Children.Add(scaleTransform);
            transformGroup.Children.Add(translateTransform);
        }

        static UIEditor()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(UIEditor), new FrameworkPropertyMetadata(typeof(UIEditor)));
        }
        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            _uiEditorLayer = GetTemplateChild(PART_UIEditorLayer) as FrameworkElement;

            _defaultEditorLayer = GetTemplateChild(PART_DefaultEditorLayer) as FrameworkElement;

            _switchViewButton = GetTemplateChild(PART_SwitchView) as Button;
            _switchViewButton.Click += SwitchViewButton_Click;

            _addObjectButton = GetTemplateChild(PART_AddObject) as Button;
            _addObjectButton.Click += AddObjectButton_Click;

            _uiSize = GetTemplateChild(PART_UISize) as FrameworkElement;

            _uiCanvas = GetTemplateChild(PART_UICanvas) as Canvas;

            _refreshButton = GetTemplateChild(PART_Refresh) as Button;
            _refreshButton.Click += RefreshButton_Click;

            _preciseButton = GetTemplateChild(PART_Precise) as Button;
            _preciseButton.Click += PreciseButton_Click;

            _preciseImage = GetTemplateChild(PART_PreciseImage) as Image;

            _uiComponentInfo = GetTemplateChild(PART_UIComponentInfo) as TextBlock;

            _unhideButton = GetTemplateChild(PART_Unhide) as Button;
            _unhideButton.Click += UnhideButton_Click;

            _uiSizeText = GetTemplateChild(PART_UISizeText) as TextBlock;

            _zoomPercent = GetTemplateChild(PART_ZoomPercent) as TextBlock;

            _backgroundGrid = GetTemplateChild(PART_BackgroundGrid) as ImageBrush;

            _pgAsset = GetTemplateChild(PART_AssetPropertyGrid) as FrostyPropertyGrid;
            refreshPropertyGrid = new Action<object>((_) =>
            {
                _pgAsset.Object = null;
                _pgAsset.Object = asset.RootObject;
            });
        }

        // switches between the default editor and the ui editor
        private void SwitchViewButton_Click(object sender, RoutedEventArgs e)
        {
            // toggles the bool
            isEditorActive = !isEditorActive;
            if (isEditorActive)
            {
                // hides the default view and shows the editor view 
                _uiEditorLayer.Visibility = Visibility.Visible;
                _defaultEditorLayer.Visibility = Visibility.Hidden;

                // gets the opened asset as an EbxAssetEntry

                EbxAssetEntry openedAsset = App.EditorWindow.GetOpenedAssetEntry() as EbxAssetEntry;

                if (openedAsset == null)
                    return;

                // loads all the ui elements with the openedAsset, isWidget as false
                // and no widget canvas since we aren't loading a widget
                LoadUI(openedAsset, false, null);
            }
            else
            {
                // hides the editor view and shows the default view
                _uiEditorLayer.Visibility = Visibility.Hidden;
                _defaultEditorLayer.Visibility = Visibility.Visible;

                // refreshes the asset that you're on so any changes you made won't be overwritten if you
                // change something in the normal editor
                refreshPropertyGrid.Invoke(null);

                // clears the texture dictionaries so that new textures will be created everytime
                mappingUvRect.Clear();
                mappingMinValue.Clear();
                mappingMaxValue.Clear();
                mappingTexture.Clear();
            }
        }

        // loads every asset/component in the ui blueprint that you're currently on
        private void LoadUI(EbxAssetEntry ebxEntry, bool isWidget, Canvas widgetCanvas)
        {
            bool createImages = Config.Get("RenderTextures", true);
            bool createWidgets = Config.Get("RenderWidgets", true);
            bool createText = Config.Get("RenderText", true);
            bool createFontEffects = Config.Get("RenderFontEffects", true);

            EbxAsset asset = App.AssetManager.GetEbx(ebxEntry);
            dynamic rootObject = asset.RootObject;

            if (debugging)
            {
                App.Logger.Log("");
                App.Logger.Log("---- " + rootObject.Name + " ----");
            }

            // NFS: Size lives directly on the root UIWidgetEntityData object
            float mainSizeX = rootObject.Object.Internal.Size.X;
            float mainSizeY = rootObject.Object.Internal.Size.Y;

            if (!isWidget)
            {
                _uiCanvas.Children.Clear();
                _uiSize.Width = mainSizeX;
                _uiSize.Height = mainSizeY;
                _uiSizeText.Text = string.Format("Size: {0}, {1}", mainSizeX, mainSizeY);
            }

            bool ShowAllUI = Config.Get("ShowAllUI", false);

            #region UI Rendering

            // NFS: Layers are still at rootObject.Object.Internal.Layers,
            // each layer is UIElementLayerEntityData with an Elements collection.
            foreach (var layer in rootObject.Object.Internal.Layers)
            {
                // NFS layers have a Visible field just like PvZ
                if (layer.Internal.Visible == false && !ShowAllUI)
                    continue;

                foreach (var uiComponent in layer.Internal.Elements)
                {
                    double offsetX = (double)uiComponent.Internal.Offset.X;
                    double offsetY = (double)uiComponent.Internal.Offset.Y;

                    double anchorX = (double)uiComponent.Internal.Anchor.X;
                    double anchorY = (double)uiComponent.Internal.Anchor.Y;

                    double width = (double)uiComponent.Internal.Size.x;
                    double height = (double)uiComponent.Internal.Size.y;
                    double x = offsetX;
                    double y = offsetY;

                    if (debugging)
                    {
                        App.Logger.Log("{0} Offset: {1} {2}, Size: {3} {4}, Anchor: {5} {6}",
                            uiComponent.Internal.InstanceName,
                            offsetX, offsetY, width, height, anchorX, anchorY);
                    }

                    double finalX = anchorX * (mainSizeX - width) + x;
                    double finalY = anchorY * (mainSizeY - height) + y;

                    double opacity = 1;
                    try { opacity = ShowAllUI ? 1 : (double)uiComponent.Internal.Alpha; } catch { }

                    var componentName = uiComponent.Internal.ToString();

                    // ------------------------------------------------------------------
                    // NFS sliced texture (equivalent of PvZ bitmap entity)
                    // Type: FrostySdk.Ebx.UIElementSlicedTextureEntityData
                    // TextureId is nested: uiComponent.Internal.Texture.Id
                    // ------------------------------------------------------------------
                    if (componentName == "FrostySdk.Ebx.UIElementSlicedTextureEntityData" && createImages)
                    {
                        try
                        {
                            if (uiComponent.Internal.Visible == true || ShowAllUI)
                            {
                                // NFS stores the texture id inside a UIAutoMappedTexture struct
                                string textureMapId = uiComponent.Internal.Texture.Id.ToString();

                                CreateTextures.GetTextures(rootObject, textureMapId);

                                double actualWidth = width;
                                double actualHeight = height;

                                width = width < 0 ? Math.Abs(width) : width;
                                height = height < 0 ? Math.Abs(height) : height;

                                var canvas = new Canvas
                                {
                                    Width = width,
                                    Height = height,
                                    Tag = uiComponent.Internal.__InstanceGuid,
                                };

                                var image = new Image
                                {
                                    Width = width,
                                    Height = height,
                                    Stretch = Stretch.Fill,
                                };

                                var texture = mappingTexture[textureMapId];
                                image.Source = texture;

                                // NFS uses UvRect (Vec4) instead of separate Min/Max
                                // UvRect: x=left, y=top, z=right, w=bottom (0-1 range)
                                var uvRect = mappingUvRect.ContainsKey(textureMapId)
                                    ? mappingUvRect[textureMapId]
                                    : null;

                                double minX = 0, minY = 0, maxX = width, maxY = height;

                                if (uvRect != null)
                                {
                                    minX = (double)uvRect.x * width;
                                    minY = (double)uvRect.y * height;
                                    maxX = (double)uvRect.z * width;
                                    maxY = (double)uvRect.w * height;
                                }

                                Point min = new Point(minX, minY);
                                Point max = new Point(maxX, maxY);

                                image.Clip = new RectangleGeometry(new Rect(min, max));
                                RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.Fant);

                                double croppedWidth = maxX - minX;
                                double croppedHeight = maxY - minY;

                                double scaleX = actualWidth / (croppedWidth > 0 ? croppedWidth : 1);
                                double scaleY = actualHeight / (croppedHeight > 0 ? croppedHeight : 1);

                                var transformGroupImage = new TransformGroup();
                                transformGroupImage.Children.Add(new TranslateTransform(-minX, -minY));
                                transformGroupImage.Children.Add(new ScaleTransform(scaleX, scaleY));
                                image.RenderTransform = transformGroupImage;

                                image.Opacity = opacity;

                                RotateElement(uiComponent, canvas);

                                Canvas.SetLeft(canvas, finalX);
                                Canvas.SetTop(canvas, finalY);

                                if (isWidget)
                                {
                                    widgetCanvas.Children.Add(canvas);
                                    canvas.Children.Add(image);
                                }
                                else
                                {
                                    _uiCanvas.Children.Add(canvas);
                                    canvas.Children.Add(image);
                                    ControlUI(canvas);
                                }
                            }
                        }
                        catch (KeyNotFoundException)
                        {
                            App.Logger.LogError($"The texture for '{uiComponent.Internal.InstanceName}' wasn't found.");
                        }
                        catch (Exception ex)
                        {
                            App.Logger.LogError($"Error rendering '{uiComponent.Internal.InstanceName}': {ex}");
                        }
                    }

                    else if (componentName == "FrostySdk.Ebx.UIElementStaticTextureEntityData" && createImages)
                    {
                        try
                        {
                            if (uiComponent.Internal.Visible == true || ShowAllUI)
                            {
                                // the texture id for a static texture is stored inside the Texture struct
                                // as an Id field, e.g. "Rimloader [37607F02]"
                                string staticTextureId = uiComponent.Internal.Texture.Id.ToString();

                                // gets all the textures needed — same call as bitmaps,
                                // CreateTextures.GetTextures should handle this id the same way
                                CreateTextures.GetTextures(rootObject, staticTextureId);

                                double actualWidth = width;
                                double actualHeight = height;

                                width = width < 0 ? Math.Abs(width) : width;
                                height = height < 0 ? Math.Abs(height) : height;

                                var canvas = new Canvas
                                {
                                    Width = width,
                                    Height = height,
                                    Tag = uiComponent.Internal.__InstanceGuid,
                                };

                                var image = new Image
                                {
                                    Width = width,
                                    Height = height,
                                    Stretch = Stretch.Fill,
                                };

                                var texture = mappingTexture[staticTextureId];
                                image.Source = texture;

                                var uvRectFull = uiComponent.Internal.UvRect;
                                Vector4 uvRect = new Vector4(uvRectFull.x, uvRectFull.y, uvRectFull.z, uvRectFull.w);

                                double minX = mappingMinValue[staticTextureId].x * width;
                                double minY = mappingMinValue[staticTextureId].y * height;
                                double maxX = mappingMaxValue[staticTextureId].x * width;
                                double maxY = mappingMaxValue[staticTextureId].y * height;

                                image.Clip = new RectangleGeometry(new Rect(new Point(minX, minY), new Point(maxX, maxY)));
                                RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.Fant);

                                double croppedWidth = maxX - minX;
                                double croppedHeight = maxY - minY;

                                double scaleX, scaleY;

                                if (uvRect == new Vector4(1, 0, 0, 1))
                                {
                                    scaleX = -actualWidth / croppedWidth;
                                    scaleY = actualHeight / croppedHeight;
                                    image.Margin = new Thickness(width, 0, 0, 0);
                                }
                                else
                                {
                                    scaleX = actualWidth / croppedWidth;
                                    scaleY = actualHeight / croppedHeight;
                                }

                                var transformGroupImage = new TransformGroup();
                                transformGroupImage.Children.Add(new TranslateTransform(-minX, -minY));
                                transformGroupImage.Children.Add(new ScaleTransform(scaleX, scaleY));
                                image.RenderTransform = transformGroupImage;

                                image.Opacity = opacity;

                                RotateElement(uiComponent, canvas);

                                Canvas.SetLeft(canvas, finalX);
                                Canvas.SetTop(canvas, finalY);

                                if (isWidget)
                                {
                                    widgetCanvas.Children.Add(canvas);
                                    canvas.Children.Add(image);
                                }
                                else
                                {
                                    _uiCanvas.Children.Add(canvas);
                                    canvas.Children.Add(image);
                                    ControlUI(canvas);
                                }
                            }
                        }
                        catch (KeyNotFoundException)
                        {
                            App.Logger.LogError($"The texture '{uiComponent.Internal.Texture.Id}' wasn't found in '{uiComponent.Internal.InstanceName}'");
                        }
                        catch (Exception ex)
                        {
                            App.Logger.LogError($"An error occurred while rendering the static texture '{uiComponent.Internal.InstanceName}': {ex}");
                        }
                    }

                    else if (componentName == "FrostySdk.Ebx.UIElementDynamicTextureEntityData" && createImages)
                    {
                        if (uiComponent.Internal.Visible == true || ShowAllUI)
                        {
                            var canvas = new Canvas
                            {
                                Width = width,
                                Height = height,
                                Tag = uiComponent.Internal.__InstanceGuid,
                            };

                            RotateElement(uiComponent, canvas);

                            Canvas.SetLeft(canvas, finalX);
                            Canvas.SetTop(canvas, finalY);

                            if (isWidget)
                                widgetCanvas.Children.Add(canvas);
                            else
                            {
                                _uiCanvas.Children.Add(canvas);
                                ControlUI(canvas);
                            }

                            try
                            {
                                var texturePointer = (PointerRef)uiComponent.Internal.Texture;

                                // if Texture is nullptr it's assigned at runtime via PropertyConnection
                                // nothing we can do, just leave the canvas empty as a placeholder
                                if (texturePointer.Type == PointerRefType.Null)
                                {
                                    if (debugging)
                                        App.Logger.Log($"Dynamic texture '{uiComponent.Internal.InstanceName}' has no static texture (runtime assigned)");
                                }
                                else
                                {
                                    // Texture is a direct EBX pointer ref to a texture asset
                                    var textureGuid = texturePointer.External.FileGuid;
                                    var textureEbx = App.AssetManager.GetEbxEntry(textureGuid);

                                    EbxAsset textureAsset = App.AssetManager.GetEbx(textureEbx);
                                    dynamic rootObjectTexture = textureAsset.RootObject;

                                    // get the res entry using the texture resource hash on the EBX
                                    ulong textureResHash = rootObjectTexture.Resource;
                                    ResAssetEntry resEntry = App.AssetManager.GetResEntry(textureResHash);

                                    if (resEntry != null)
                                    {
                                        Texture texture = App.AssetManager.GetResAs<Texture>(resEntry);

                                        TextureExporterToMemory.Export(texture);
                                        BitmapImage bitmap = CreateTextures.CreateBitmap(TextureExporterToMemory.textureBytes);

                                        double actualWidth = width;
                                        double actualHeight = height;

                                        width = width < 0 ? Math.Abs(width) : width;
                                        height = height < 0 ? Math.Abs(height) : height;

                                        var image = new Image
                                        {
                                            Width = width,
                                            Height = height,
                                            Stretch = Stretch.Fill,
                                            Source = bitmap,
                                            Opacity = opacity,
                                        };

                                        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.Fant);

                                        canvas.Children.Add(image);
                                    }
                                    else
                                    {
                                        App.Logger.LogError($"Could not find res entry for dynamic texture '{uiComponent.Internal.InstanceName}'");
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                App.Logger.LogError($"An error occurred while rendering the dynamic texture '{uiComponent.Internal.InstanceName}': {ex}");
                            }
                        }
                    }
                    // ------------------------------------------------------------------
                    // Text fields — wrapped in try/catch because NFS text field
                    // properties (Text.Sid, Text.Wordwrap, FontStyle, etc.) may differ
                    // or be absent. On failure it falls back to an orange placeholder.
                    // ------------------------------------------------------------------
                    else if (componentName == "FrostySdk.Ebx.UIElementTextFieldEntityData" && createText)
                    {
                        if (uiComponent.Internal.Visible == true || ShowAllUI)
                        {
                            var canvas = new Canvas
                            {
                                Width = width,
                                Height = height,
                                Tag = uiComponent.Internal.__InstanceGuid,
                            };

                            try
                            {
                                var border = new Border { Width = width, Height = height };
                                var tb = new TextBlock();

                                // NFS may not have a .Text sub-object — try Sid/FieldText,
                                // fall back to InstanceName if neither exists
                                string outcome = uiComponent.Internal.InstanceName;
                                try
                                {
                                    string sid = uiComponent.Internal.Text.Sid;
                                    string fieldText = uiComponent.Internal.FieldText;
                                    outcome = sid == "" ? fieldText : sid;
                                }
                                catch { /* .Text struct not present, keep InstanceName */ }

                                if (outcome != "" && outcome != null)
                                {
                                    tb.Text = outcome.StartsWith("ID_")
                                        ? LocalizedStringDatabase.Current.GetString(outcome)
                                        : outcome;
                                }
                                else
                                {
                                    tb.Text = uiComponent.Internal.InstanceName;
                                }

                                tb.Opacity = opacity;

                                try
                                {
                                    if (uiComponent.Internal.Text.Wordwrap)
                                        tb.TextWrapping = TextWrapping.Wrap;
                                }
                                catch { /* no Wordwrap field */ }

                                // Font loading — skip if FontStyle pointer is missing
                                try
                                {
                                    var fontGuid = ((PointerRef)uiComponent.Internal.FontStyle).External.FileGuid;
                                    var fontEbx = App.AssetManager.GetEbxEntry(fontGuid);

                                    EbxAsset fontAsset = App.AssetManager.GetEbx(fontEbx);
                                    dynamic rootObjectFont = fontAsset.RootObject;

                                    var fontEbxPath = rootObjectFont.Hd.Internal.FontLookup[0].FontAssetPath;
                                    var fontEbxTTF = App.AssetManager.GetEbx(fontEbxPath);
                                    ulong ttfRes = fontEbxTTF.RootObject.FontResource;
                                    ResAssetEntry ttfResEntry = App.AssetManager.GetResEntry(ttfRes);

                                    using (Stream ttfStream = App.AssetManager.GetRes(ttfResEntry))
                                    {
                                        string fontName = "./#" + fontEbxTTF.RootObject.FontFamilyName;
                                        string tempFile = Path.Combine(Path.GetTempPath(),
                                            string.Format("{0:X16}.ttf", fontEbxTTF.RootObject.FontResource));

                                        if (!File.Exists(tempFile))
                                        {
                                            using (var fs = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.Read))
                                                ttfStream.CopyTo(fs);
                                        }

                                        tb.FontFamily = new FontFamily(new Uri(tempFile, UriKind.Absolute), fontName);
                                    }

                                    tb.FontSize = (double)rootObjectFont.Hd.Internal.PointSize;
                                }
                                catch (Exception ex)
                                {
                                    App.Logger.Log($"Could not load font for '{uiComponent.Internal.InstanceName}': {ex.Message}");
                                }

                                // Alignment — skip if .Text alignment fields are absent
                                try
                                {
                                    switch (uiComponent.Internal.Text.VerticalAlignment.ToString())
                                    {
                                        case "UIElementAlignment_Top": tb.VerticalAlignment = VerticalAlignment.Top; break;
                                        case "UIElementAlignment_Center": tb.VerticalAlignment = VerticalAlignment.Center; break;
                                        case "UIElementAlignment_Bottom": tb.VerticalAlignment = VerticalAlignment.Bottom; break;
                                        default: tb.VerticalAlignment = VerticalAlignment.Center; break;
                                    }

                                    switch (uiComponent.Internal.Text.HorizonalAlignment.ToString())
                                    {
                                        case "UIElementAlignment_Left": tb.TextAlignment = TextAlignment.Left; break;
                                        case "UIElementAlignment_Center": tb.TextAlignment = TextAlignment.Center; break;
                                        case "UIElementAlignment_Right": tb.TextAlignment = TextAlignment.Right; break;
                                        default: tb.TextAlignment = TextAlignment.Center; break;
                                    }
                                }
                                catch { /* alignment fields not present, use WPF defaults */ }

                                RotateElement(uiComponent, canvas);

                                // Font effect — optional, skip if not present
                                try
                                {
                                    var fontEffectGuid = ((PointerRef)uiComponent.Internal.FontEffect).External.FileGuid;
                                    var fontEffectEbx = App.AssetManager.GetEbxEntry(fontEffectGuid);
                                    if (fontEffectEbx != null && createFontEffects)
                                        ApplyFontEffect(tb, border, canvas, fontEffectEbx);
                                }
                                catch { /* no FontEffect on this component */ }

                                Canvas.SetLeft(canvas, finalX);
                                Canvas.SetTop(canvas, finalY);

                                if (isWidget)
                                {
                                    widgetCanvas.Children.Add(canvas);
                                    canvas.Children.Add(border);
                                    border.Child = tb;
                                }
                                else
                                {
                                    _uiCanvas.Children.Add(canvas);
                                    canvas.Children.Add(border);
                                    border.Child = tb;
                                    ControlUI(canvas);
                                }
                            }
                            catch (Exception ex)
                            {
                                // Fallback: render an orange placeholder so the rest of the UI still loads
                                App.Logger.LogError($"Error rendering text field '{uiComponent.Internal.InstanceName}': {ex.Message}");

                                var rect = new System.Windows.Shapes.Rectangle
                                {
                                    Width = width,
                                    Height = height,
                                    Fill = Brushes.Orange,
                                    Opacity = 0.15,
                                };
                                var label = new TextBlock
                                {
                                    Text = uiComponent.Internal.InstanceName,
                                    FontSize = 14,
                                    Opacity = 0.4,
                                };

                                Canvas.SetLeft(canvas, finalX);
                                Canvas.SetTop(canvas, finalY);

                                if (isWidget)
                                {
                                    widgetCanvas.Children.Add(canvas);
                                    canvas.Children.Add(rect);
                                    canvas.Children.Add(label);
                                }
                                else
                                {
                                    _uiCanvas.Children.Add(canvas);
                                    canvas.Children.Add(rect);
                                    canvas.Children.Add(label);
                                    ControlUI(canvas);
                                }
                            }
                        }
                    }

                    else if (componentName == "FrostySdk.Ebx.UIElementLetterBoxEntityData")
                    {
                        if (uiComponent.Internal.Visible == true || ShowAllUI)
                        {
                            var canvas = new Canvas
                            {
                                Width = width,
                                Height = height,
                                Tag = uiComponent.Internal.__InstanceGuid,
                            };

                            var rect = new System.Windows.Shapes.Rectangle
                            {
                                Width = width,
                                Height = height,
                            };

                            var colorR = (byte)Math.Round(uiComponent.Internal.Color.x * 255);
                            var colorG = (byte)Math.Round(uiComponent.Internal.Color.y * 255);
                            var colorB = (byte)Math.Round(uiComponent.Internal.Color.z * 255);

                            rect.Fill = new SolidColorBrush(Color.FromRgb(colorR, colorG, colorB));
                            rect.Opacity = opacity;

                            RotateElement(uiComponent, canvas);

                            Canvas.SetLeft(canvas, finalX);
                            Canvas.SetTop(canvas, finalY);

                            if (isWidget)
                            {
                                widgetCanvas.Children.Add(canvas);
                                canvas.Children.Add(rect);
                            }
                            else
                            {
                                _uiCanvas.Children.Add(canvas);
                                canvas.Children.Add(rect);
                                ControlUI(canvas);
                            }
                        }
                    }
                    // ------------------------------------------------------------------
                    // Fill/colour rect
                    // ------------------------------------------------------------------
                    else if (componentName == "FrostySdk.Ebx.UIElementFillEntityData")
                    {
                        try
                        {
                            if (uiComponent.Internal.Visible == true || ShowAllUI)
                            {
                                var canvas = new Canvas
                                {
                                    Width = width,
                                    Height = height,
                                    Tag = uiComponent.Internal.__InstanceGuid,
                                };

                                var rect = new System.Windows.Shapes.Rectangle
                                {
                                    Width = width,
                                    Height = height,
                                };

                                var colorR = (byte)Math.Round(uiComponent.Internal.Color.x * 255);
                                var colorG = (byte)Math.Round(uiComponent.Internal.Color.y * 255);
                                var colorB = (byte)Math.Round(uiComponent.Internal.Color.z * 255);

                                rect.Fill = new SolidColorBrush(Color.FromRgb(colorR, colorG, colorB));
                                rect.Opacity = opacity;

                                RotateElement(uiComponent, canvas);

                                Canvas.SetLeft(canvas, finalX);
                                Canvas.SetTop(canvas, finalY);

                                if (isWidget)
                                {
                                    widgetCanvas.Children.Add(canvas);
                                    canvas.Children.Add(rect);
                                }
                                else
                                {
                                    _uiCanvas.Children.Add(canvas);
                                    canvas.Children.Add(rect);
                                    ControlUI(canvas);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            App.Logger.LogError($"Error rendering fill rect '{uiComponent.Internal.InstanceName}': {ex.Message}");
                        }
                    }
                    // ------------------------------------------------------------------
                    // Button — just a hitbox, nothing to render
                    // ------------------------------------------------------------------
                    else if (componentName == "FrostySdk.Ebx.UIElementButtonEntityData")
                    {
                        // no visual output needed
                    }
                    // ------------------------------------------------------------------
                    // Widget reference — recurse into child blueprint
                    // ------------------------------------------------------------------
                    else if (componentName == "FrostySdk.Ebx.UIElementWidgetReferenceEntityData" && createWidgets)
                    {
                        var canvasWidget = new Canvas { Tag = uiComponent.Internal.__InstanceGuid };

                        var widgetGuid = ((PointerRef)uiComponent.Internal.Blueprint).External.FileGuid;
                        var widgetEbx = App.AssetManager.GetEbxEntry(widgetGuid);

                        EbxAsset widgetAsset = App.AssetManager.GetEbx(widgetEbx);
                        dynamic rootObjectWidget = widgetAsset.RootObject;

                        var widgetSize = rootObjectWidget.Object.Internal.Size;

                        if (!uiComponent.Internal.UseElementSize)
                        {
                            canvasWidget.Width = widgetSize.X;
                            canvasWidget.Height = widgetSize.Y;
                        }
                        else
                        {
                            canvasWidget.Width = width;
                            canvasWidget.Height = height;
                        }

                        double widgetFinalX = anchorX * (mainSizeX - (double)widgetSize.X) + x;
                        double widgetFinalY = anchorY * (mainSizeY - (double)widgetSize.Y) + y;

                        canvasWidget.Opacity = opacity;
                        RotateElement(uiComponent, canvasWidget);

                        Canvas.SetLeft(canvasWidget, widgetFinalX);
                        Canvas.SetTop(canvasWidget, widgetFinalY);

                        if (isWidget)
                            widgetCanvas.Children.Add(canvasWidget);
                        else
                        {
                            _uiCanvas.Children.Add(canvasWidget);
                            ControlUI(canvasWidget);
                        }

                        LoadUI(widgetEbx, true, canvasWidget);
                    }

                    else if (componentName == "FrostySdk.Ebx.UIContainerEntityData" && createWidgets)
                    {
                        if (uiComponent.Internal.Visible == true || ShowAllUI)
                        {
                            var containerCanvas = new Canvas
                            {
                                Width = width,
                                Height = height,
                                Tag = uiComponent.Internal.__InstanceGuid,
                                Opacity = opacity,
                            };

                            RotateElement(uiComponent, containerCanvas);

                            Canvas.SetLeft(containerCanvas, finalX);
                            Canvas.SetTop(containerCanvas, finalY);

                            if (isWidget)
                            {
                                widgetCanvas.Children.Add(containerCanvas);
                            }
                            else
                            {
                                _uiCanvas.Children.Add(containerCanvas);
                                ControlUI(containerCanvas);
                            }

                            foreach (var childRef in uiComponent.Internal.Elements)
                            {
                                dynamic child = childRef.Internal;

                                if (child == null) continue;

                                double childOffsetX = (double)child.Offset.X;
                                double childOffsetY = (double)child.Offset.Y;
                                double childAnchorX = (double)child.Anchor.X;
                                double childAnchorY = (double)child.Anchor.Y;
                                double childWidth = (double)child.Size.x;
                                double childHeight = (double)child.Size.y;
                                double childX = (double)child.Offset.X;
                                double childY = (double)child.Offset.Y;

                                double childFinalX = childAnchorX * (width - childWidth) + childX;
                                double childFinalY = childAnchorY * (height - childHeight) + childY;

                                double childOpacity = 1;
                                if (child.Alpha != null)
                                    childOpacity = ShowAllUI ? 1 : child.Alpha;

                                var childName = child.ToString();
                            }
                        }
                    }
                    // ------------------------------------------------------------------
                    // Unknown / unrecognized component — render a labelled placeholder
                    // ------------------------------------------------------------------
                    else
                    {
                        App.Logger.Log("Unrecognized UI component: " + componentName);

                        var canvas = new Canvas
                        {
                            Width = width,
                            Height = height,
                            Tag = uiComponent.Internal.__InstanceGuid,
                        };

                        var rect = new System.Windows.Shapes.Rectangle
                        {
                            Width = width,
                            Height = height,
                            Fill = Brushes.Orange,
                            Opacity = 0.05,
                        };

                        var tb = new TextBlock
                        {
                            Text = uiComponent.Internal.InstanceName,
                            FontSize = 24,
                            Opacity = 0.2,
                        };

                        Canvas.SetLeft(canvas, finalX);
                        Canvas.SetTop(canvas, finalY);

                        if (isWidget)
                        {
                            widgetCanvas.Children.Add(canvas);
                            canvas.Children.Add(rect);
                            canvas.Children.Add(tb);
                        }
                        else
                        {
                            _uiCanvas.Children.Add(canvas);
                            canvas.Children.Add(rect);
                            canvas.Children.Add(tb);
                            ControlUI(canvas);
                        }
                    }
                }

                _uiCanvas.UpdateLayout();
            }

            #endregion
        }

        private void RotateElement(dynamic uiComponent, Canvas canvas)
        {
            try
            {
                // NFS stores rotation as a full LinearTransform on the element.
                // The 'right' vector gives us the cosine/sine of the rotation angle.
                double rightX = (double)uiComponent.Internal.Transform.right.x;
                double rightY = (double)uiComponent.Internal.Transform.right.y;

                // atan2 gives us the angle in radians; convert to degrees
                double rotation = Math.Atan2(rightY, rightX) * (180.0 / Math.PI);

                // pivot is stored in TransformPivot (Vec3, we only need x/y)
                double pivotX = (double)uiComponent.Internal.TransformPivot.x;
                double pivotY = (double)uiComponent.Internal.TransformPivot.y;

                if (Math.Abs(rotation) < 0.001)
                    return; // no rotation, skip

                var transformGroupCanvas = new TransformGroup();
                var rotateTransform = new RotateTransform(rotation)
                {
                    CenterX = pivotX,
                    CenterY = pivotY,
                };
                transformGroupCanvas.Children.Add(rotateTransform);
                canvas.RenderTransform = transformGroupCanvas;
            }
            catch
            {
                // if the transform fields don't exist on this component, just skip rotation
            }
        }
        #region Font Effects
        // info on effects are in this google doc: https://docs.google.com/document/d/1EdNMCM0jUy4g_uLQMIZm5RP2XCep35ui5dGl61VXOTc/edit?usp=sharing
        // credits to brekko for giving me a txt file for it, i just put it in a google doc so it's easier to read
        private void ApplyFontEffect(TextBlock tb, Border border, Canvas canvas, EbxAssetEntry fontEffectEbx)
        {
            // most effects aren't used or don't make much of a difference so
            // there's no point of coding it in, so only these font effects will have an effect on text
            string[] effectWhitelist = { "SetGlyphColor", "SetGlyphOffset", "SetGlyphBrush", "DrawGlyph", "DrawGlyphSmearOutline", "Merge", "Clear" };

            EbxAsset fontEffectAsset = App.AssetManager.GetEbx(fontEffectEbx);
            dynamic rootObjectFontEffect = fontEffectAsset.RootObject;

            string fontEffect = rootObjectFontEffect.EffectScript;

            using (StringReader reader = new StringReader(fontEffect))
            {
                Dictionary<string, string> currentValues = new Dictionary<string, string>();

                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string[] effectArray = line.Split(' ');

                    string effectName = effectArray[0];

                    if (effectWhitelist.Contains(effectName))
                    {
                        try
                        {
                            if (effectArray.Length == 1) // if the effect has no arguments (for example: Merge, Clear...)
                            {
                                switch (effectName)
                                {
                                    case "DrawGlyph":
                                        // draws the text

                                        if (currentValues.ContainsKey("SetGlyphColor"))
                                        {
                                            tb.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(currentValues["SetGlyphColor"]));
                                        }
                                        break;
                                    case "DrawGlyphSmearOutline":
                                        // draws an outline

                                        SolidColorBrush color = new SolidColorBrush((Color)ColorConverter.ConvertFromString(currentValues["SetGlyphColor"]));

                                        // 'AdornerLayer.GetAdornerLayer()' will return null if this isn't used
                                        var adornerLayer = new AdornerDecorator
                                        {
                                            ClipToBounds = true,
                                        };

                                        // this basically copies everything that was done when first rendering the text field
                                        var borderStroke = new Border
                                        {
                                            Width = border.Width,
                                            Height = border.Height,
                                        };

                                        var stroke = new TextBlock
                                        {
                                            Text = tb.Text,
                                            Margin = tb.Margin,
                                            Opacity = tb.Opacity,
                                            Padding = tb.Padding,
                                            TextWrapping = tb.TextWrapping,
                                            FontFamily = tb.FontFamily,
                                            FontSize = tb.FontSize,
                                            TextAlignment = tb.TextAlignment,
                                            VerticalAlignment = tb.VerticalAlignment,
                                            RenderTransform = tb.RenderTransform,
                                        };

                                        // sets it below the tb
                                        Panel.SetZIndex(stroke, Panel.GetZIndex(tb) - 1);

                                        // sets an offset if it exists
                                        if (currentValues.ContainsKey("SetGlyphOffset"))
                                        {
                                            string[] offset = currentValues["SetGlyphOffset"].Split(',');

                                            double x = Convert.ToDouble(offset[0]);
                                            double y = Convert.ToDouble(offset[1]);

                                            borderStroke.Margin = new Thickness(x, y, 0, 0);
                                        }

                                        // if there is no "SetGlyphBrush" it will use 5 for the default thickness
                                        ushort thickness = 5;
                                        if (currentValues.ContainsKey("SetGlyphBrush"))
                                        {
                                            // dividing by 1.2 seems about right, otherwise some strokes are too big
                                            double brush = Convert.ToDouble(currentValues["SetGlyphBrush"]) / 1.2;

                                            thickness = (ushort)brush;
                                        }

                                        canvas.Children.Add(adornerLayer);
                                        adornerLayer.Child = borderStroke;
                                        borderStroke.Child = stroke;

                                        AdornerLayer adorner = AdornerLayer.GetAdornerLayer(stroke);

                                        StrokeAdorner strokeAdorner = new StrokeAdorner(stroke);

                                        // this is so the opacity of the text block is included
                                        var colorWithAlpha = new SolidColorBrush(Color.FromArgb((byte)Math.Round(tb.Opacity * 255), color.Color.R, color.Color.G, color.Color.B));

                                        strokeAdorner.Stroke = colorWithAlpha;
                                        strokeAdorner.StrokeThickness = thickness;
                                        strokeAdorner.Fill = colorWithAlpha;

                                        adorner.Add(strokeAdorner);
                                        break;
                                    case "Merge":
                                        // moves onto the next part

                                        currentValues.Clear();
                                        break;
                                    case "Clear":
                                        // the same thing as Merge, this is only here because some font effects skip Merge

                                        currentValues.Clear();
                                        break;
                                }
                            }
                            else
                            {
                                // these are the arugments for each effect (most of the time there is only one)
                                // the first index is skipped because that is just the name of the effect
                                string[] effectValues = effectArray.Skip(1).ToArray();

                                switch (effectName)
                                {
                                    case "SetGlyphColor":
                                        string value = effectValues[0];

                                        // removes the first 4 characters (0xff) and puts a '#' before it
                                        // sometimes the hex doesnt include the 'ff' after '0x' so only 2 is cut off
                                        string fullHex = value.Remove(0, value.Length > 8 ? 4 : 2).Insert(0, "#");

                                        // this will limit the hex from being longer than 7 (not 6 because the '#' is included)
                                        string hex = fullHex.Remove(7, fullHex.Length - 7);

                                        currentValues.Add("SetGlyphColor", hex);
                                        break;
                                    case "SetGlyphOffset":
                                        string offset = $"{effectValues[0]},{effectValues[1]}";

                                        currentValues.Add("SetGlyphOffset", offset);
                                        break;
                                    case "SetGlyphBrush":
                                        // this gets the second value (index 1) for the size, there are actually 4 arguments
                                        // but we only really need the size. the full arguments are: uint32_t shape, size, hardness, opacity

                                        string size = effectValues[1];

                                        currentValues.Add("SetGlyphBrush", size);
                                        break;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            App.Logger.Log($"Error loading the Font Effect {fontEffectEbx.Name}: {ex}");
                        }
                    }
                }
            }
        }
        #endregion

        // i wanted to have the draggable ui code somewhere else but it broke a lot of stuff
        // so it has to say here for now
        #region Dragging
        bool showHitboxes = Config.Get("ShowHitboxes", true);

        // this is used for the precise movement / snapping
        int roundTo = 1;

        Canvas selectedCanvas;
        dynamic selectedElement;

        public void ControlUI(Canvas canvas)
        {
            canvas.MouseMove += CanvasMouseMove;
            canvas.MouseLeftButtonDown += CanvasMouseDown;
            canvas.MouseLeftButtonUp += CanvasMouseUp;

            canvas.MouseEnter += CanvasMouseEnter;
            canvas.MouseLeave += CanvasMouseLeave;

            canvas.MouseRightButtonDown += CanvasHideUI;
        }

        Point startPosition;
        Point startPositionPan;
        private void CanvasMouseDown(object sender, MouseButtonEventArgs e)
        {
            Canvas canvas = sender as Canvas;
            selectedCanvas = canvas;

            dragging = true;

            // gets the mouse position
            startPosition = Mouse.GetPosition(_uiCanvas);
        }

        private void CanvasMouseUp(object sender, MouseButtonEventArgs e)
        {
            dragging = false;

            Canvas canvas = sender as Canvas;
            selectedCanvas = canvas;

            if (showHitboxes)
            {
                canvas.Background = new SolidColorBrush(Color.FromArgb(15, 157, 198, 252));
            }

            // reset ZIndex after moving it
            Panel.SetZIndex(canvas, 0);

            double roundedX = Math.Round(Canvas.GetLeft(canvas) / roundTo) * roundTo;
            double roundedY = Math.Round(Canvas.GetTop(canvas) / roundTo) * roundTo;

            float movedX = (float)roundedX;
            float movedY = (float)roundedY;

            // sets the position
            Canvas.SetLeft(canvas, roundedX);
            Canvas.SetTop(canvas, roundedY);

            // gets the Guid of the canvas from the tag created earlier so we can use it as an ebx asset
            var canvasGuid = canvas.Tag;

            _refreshButton.Visibility = Visibility.Visible;
            _preciseButton.Visibility = Visibility.Visible;
            _unhideButton.Visibility = Visibility.Visible;
            _uiSizeText.Visibility = Visibility.Visible;
            _uiComponentInfo.Visibility = Visibility.Visible;

            EbxAssetEntry ebxEntry = App.EditorWindow.GetOpenedAssetEntry() as EbxAssetEntry;

            EbxAsset asset = App.AssetManager.GetEbx(ebxEntry);
            dynamic rootObject = asset.RootObject;

            // goes through every ui component until the guid of it matches guid from the tag
            foreach (var layer in rootObject.Object.Internal.Layers)
            {
                foreach (var uiComponent in layer.Internal.Elements)
                {
                    var guid = uiComponent.Internal.__InstanceGuid;

                    if (debugging)
                    {
                        App.Logger.Log(Convert.ToString("Guid: " + guid));
                        App.Logger.Log(Convert.ToString("Canvas Guid: " + canvasGuid));
                    }

                    if (guid.ToString() == canvasGuid.ToString())
                    {
                        selectedElement = uiComponent;

                        // the useAnchor option is removed here because for some reason the anchor positioning wasn't accurate
                        // so the 'else' part won't do anything for now

                        bool useAnchor = false;
                        //bool useAnchor = Config.Get<bool>("UseAnchor", false);

                        if (!useAnchor)
                        {
                            // if useAnchor is false, we remove the anchor and set the position with offset
                            uiComponent.Internal.Offset.X = movedX;
                            uiComponent.Internal.Offset.Y = movedY;

                            uiComponent.Internal.Anchor.X = 0;
                            uiComponent.Internal.Anchor.Y = 0;
                        }
                        else
                        {
                            // if useAnchor is true, we remove the offset and set the position with anchor
                            uiComponent.Internal.Anchor.X = movedX / rootObject.Object.Internal.Size.X;
                            uiComponent.Internal.Anchor.Y = movedY / rootObject.Object.Internal.Size.Y;

                            uiComponent.Internal.Offset.X = 0;
                            uiComponent.Internal.Offset.Y = 0;
                        }

                        // saves it to the ebx so that it will show up in game or in frosty
                        App.AssetManager.ModifyEbx(rootObject.Name, asset);

                        // refreshes the data explorer so that it shows as modified on the left
                        App.EditorWindow.DataExplorer.RefreshItems();

                        _uiComponentInfo.Text =
                            string.Format(
                            "InstanceName: '{0}'\nOffset: {1}, {2}\nAnchor: {3}, {4}\n{5}",
                            uiComponent.Internal.InstanceName,
                            uiComponent.Internal.Offset.X,
                            uiComponent.Internal.Offset.Y,
                            uiComponent.Internal.Anchor.X,
                            uiComponent.Internal.Anchor.Y,
                            guid.ToString());

                        if (debugging)
                        {
                            App.Logger.Log("Saved Position");
                        }
                    }
                }
            }
        }

        private void CanvasMouseMove(object sender, MouseEventArgs e)
        {
            if (dragging)
            {
                Canvas canvas = sender as Canvas;

                // if the wrong canvas is being selected it can be buggy so we will just cancel the movement
                if (canvas != selectedCanvas)
                {
                    dragging = false;

                    _refreshButton.Visibility = Visibility.Visible;
                    _preciseButton.Visibility = Visibility.Visible;
                    _unhideButton.Visibility = Visibility.Visible;
                    _uiSizeText.Visibility = Visibility.Visible;
                    _uiComponentInfo.Visibility = Visibility.Visible;

                    return;
                }

                if (showHitboxes)
                {
                    canvas.Background = new SolidColorBrush(Color.FromArgb(0, 157, 198, 252));
                }

                // sets the ZIndex above everything else so it doesn't glitch when moving near other ui elements
                Panel.SetZIndex(canvas, 9999);

                Point newPosition = Mouse.GetPosition(_uiCanvas);

                double left = Canvas.GetLeft(canvas);
                double top = Canvas.GetTop(canvas);

                Canvas.SetLeft(canvas, left + (newPosition.X - startPosition.X));
                Canvas.SetTop(canvas, top + (newPosition.Y - startPosition.Y));
                startPosition = newPosition;

                _refreshButton.Visibility = Visibility.Hidden;
                _preciseButton.Visibility = Visibility.Hidden;
                _unhideButton.Visibility = Visibility.Hidden;
                _uiSizeText.Visibility = Visibility.Hidden;
                _uiComponentInfo.Visibility = Visibility.Hidden;

                if (debugging)
                {
                    // this can make it very laggy if you have debugging on and not commented out
                    //App.Logger.Log(left.ToString());
                    //App.Logger.Log(top.ToString());

                    //App.Logger.Log(roundTo.ToString());
                }
            }
        }
        #endregion

        // the same thing with the dragging, i wanted to have the zoom/panning in another script but it just broke
        #region Zooming
        private void UICanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            // if e.Delta is less than 0, we're zooming out
            bool zoomOut = e.Delta < 0;

            double maxZoom = 3;
            double minZoom = 0.2;

            // this value is how much it will be zoomed by
            double zoomValue = 0.1;

            // this is the center of '_uiSize' and not the center of the screen
            // so if you pan out it wont zoom from the center which is annoying and idk how i would fix it
            _uiSize.RenderTransformOrigin = new Point(0.5, 0.5);

            _uiSize.RenderTransform = transformGroup;

            double scale = scaleTransform.ScaleX;

            if (zoomOut)
            {
                // zoom out
                scale -= zoomValue;
            }
            else
            {
                // zoom in
                scale += zoomValue;
            }

            scale = Clamp(scale, minZoom, maxZoom);

            scaleTransform.ScaleX = scale;
            scaleTransform.ScaleY = scale;

            // sets the background grid so that it looks like the background is also zooming in/out
            _backgroundGrid.Viewport = new Rect(0, 0, scale * 28, scale * 28);

            _zoomPercent.Text = Math.Round(scale * 100) + "% Zoom";
        }

        // Math.Clamp is annoyingly missing so its added here
        private double Clamp(double value, double min, double max)
        {
            if (value < min)
            {
                return min;
            }
            else if (value > max)
            {
                return max;
            }

            return value;
        }
        #endregion

        #region Panning

        Point lastPosition;
        private void UICanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle)
            {
                panning = true;

                startPositionPan = Mouse.GetPosition(this);
                lastPosition = new Point(translateTransform.X, translateTransform.Y);
            }
        }

        private void UICanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle)
            {
                panning = false;
            }
        }

        private void UICanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (panning)
            {
                Point mousePosition = e.GetPosition(this);
                Point newPosition = new Point(mousePosition.X, mousePosition.Y);

                double panMultiplier;

                // this will make it so the pan multiplier will scale with the size of the ui blueprint
                // otherwise on small ui blueprints the panning would be too fast or too slow on some UIs

                double averageSize = (_uiSize.ActualWidth + _uiSize.ActualHeight) / 2;
                panMultiplier = averageSize / 850; // the '850' is basically the overall speed

                // pans from the center
                _uiSize.RenderTransformOrigin = new Point(0.5, 0.5);

                _uiSize.RenderTransform = transformGroup;

                translateTransform.X = lastPosition.X + (newPosition.X - startPositionPan.X) * panMultiplier;
                translateTransform.Y = lastPosition.Y + (newPosition.Y - startPositionPan.Y) * panMultiplier;

                // changing the background grid

                // creates a new translate transform which is basically the same as the other one
                // but it doesnt multiply by panMultiplier, otherwise it would be too fast
                TranslateTransform gridTransform = new TranslateTransform();
                gridTransform.X = lastPosition.X + (newPosition.X - startPositionPan.X);
                gridTransform.Y = lastPosition.Y + (newPosition.Y - startPositionPan.Y);

                // then its just set to the transform
                _backgroundGrid.Transform = new MatrixTransform(gridTransform.Value);
            }
        }
        #endregion

        // shows a transparent background when your mouse is over the canvas
        // useful for showing where you can or can't drag certain elements
        private void CanvasMouseEnter(object sender, MouseEventArgs e)
        {
            if (showHitboxes)
            {
                Canvas canvas = sender as Canvas;

                canvas.Background = new SolidColorBrush(Color.FromArgb(9, 157, 198, 252));
            }
        }

        private void CanvasMouseLeave(object sender, MouseEventArgs e)
        {
            if (showHitboxes)
            {
                Canvas canvas = sender as Canvas;

                canvas.Background = new SolidColorBrush(Color.FromArgb(0, 157, 198, 252));
            }
        }

        // arrow key movement for precise movements
        public void UICanvasKeyDown(object sender, KeyEventArgs e)
        {
            if (selectedCanvas == null)
                return;

            bool movementKey = false;

            double left = Canvas.GetLeft(selectedCanvas);
            double top = Canvas.GetTop(selectedCanvas);

            int move = Config.Get("ArrowKeyMovementSetting", 5);

            // this stops the arrow keys from navigating to some random place, otherwise arrow keys would just break
            if (e.Key == Key.Up || e.Key == Key.Left || e.Key == Key.Down || e.Key == Key.Right)
            {
                e.Handled = true;
            }

            // i would've used a switch but i wanted to have both WASD and arrow keys
            if (e.Key == Key.W || e.Key == Key.Up)
            {
                Canvas.SetTop(selectedCanvas, top + -move);
                movementKey = true;
            }
            else if (e.Key == Key.A || e.Key == Key.Left)
            {
                Canvas.SetLeft(selectedCanvas, left + -move);
                movementKey = true;
            }
            else if (e.Key == Key.S || e.Key == Key.Down)
            {
                Canvas.SetTop(selectedCanvas, top + move);
                movementKey = true;
            }
            else if (e.Key == Key.D || e.Key == Key.Right)
            {
                Canvas.SetLeft(selectedCanvas, left + move);
                movementKey = true;
            }

            // checks if its a movement key (wasd or arrow keys) so that nothing happens if you touch any other keys
            if (movementKey)
            {
                selectedElement.Internal.Offset.X = (float)Canvas.GetLeft(selectedCanvas);
                selectedElement.Internal.Offset.Y = (float)Canvas.GetTop(selectedCanvas);

                // we'll just be using offset for this so there's no point of adding an anchor
                selectedElement.Internal.Anchor.X = 0;
                selectedElement.Internal.Anchor.Y = 0;

                EbxAssetEntry ebxEntry = App.EditorWindow.GetOpenedAssetEntry() as EbxAssetEntry;

                EbxAsset asset = App.AssetManager.GetEbx(ebxEntry);
                dynamic rootObject = asset.RootObject;

                // saves it to the ebx so that it will show up in game or in frosty
                App.AssetManager.ModifyEbx(rootObject.Name, asset);

                // refreshes the data explorer so that it shows as modified on the left
                App.EditorWindow.DataExplorer.RefreshItems();

                var guid = selectedElement.Internal.__InstanceGuid;

                _uiComponentInfo.Text =
                    string.Format(
                    "InstanceName: '{0}'\nOffset: {1}, {2}\nAnchor: {3}, {4}\n{5}",
                    selectedElement.Internal.InstanceName,
                    selectedElement.Internal.Offset.X,
                    selectedElement.Internal.Offset.Y,
                    selectedElement.Internal.Anchor.X,
                    selectedElement.Internal.Anchor.Y,
                    guid.ToString());
            }
        }

        // right clicking will hide the ui, this is useful if some ui elements are in the way of something you wanna move
        private void CanvasHideUI(object sender, EventArgs e)
        {
            // it will only work if you aren't dragging a ui element otherwise it will be buggy
            if (!dragging)
            {
                Canvas canvas = sender as Canvas;
                canvas.Visibility = Visibility.Hidden;
            }
        }

        public void UnhideButton_Click(object sender, RoutedEventArgs e)
        {
            // loops through each canvas in _uiCanvas and sets them all visible
            foreach (Canvas canvas in _uiCanvas.Children)
            {
                canvas.Visibility = Visibility.Visible;
            }
        }

        // switches between the roundTo value and 1 which changes how precise ui dragging is
        // idk why i didn't just call it "Snapping" lol
        bool isPrecise = true;
        public void PreciseButton_Click(object sender, RoutedEventArgs e)
        {
            // toggles the bool
            isPrecise = !isPrecise;

            // gets the icon for the off and on icons
            ImageSource offIcon = new ImageSourceConverter().ConvertFromString("pack://application:,,,/UIBlueprintEditor;component/Images/Precise_OFF.png") as ImageSource;
            ImageSource onIcon = new ImageSourceConverter().ConvertFromString("pack://application:,,,/UIBlueprintEditor;component/Images/Precise_ON.png") as ImageSource;

            if (isPrecise)
            {
                roundTo = 1;

                App.Logger.Log("Precise Movement: ON");

                _preciseButton.ToolTip = "Precise Movement (ON)";
                _preciseImage.Source = onIcon;
            }
            else
            {
                roundTo = Config.Get("PreciseMovementSetting", 25);

                App.Logger.Log("Precise Movement: OFF");

                _preciseButton.ToolTip = "Precise Movement (OFF)";
                _preciseImage.Source = offIcon;
            }
        }

        // refreshes the ui editor
        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            // clears all the dictionaries for the textures
            mappingUvRect.Clear();
            mappingMinValue.Clear();
            mappingMaxValue.Clear();
            mappingTexture.Clear();

            // clears everything in the ui canvas
            _uiCanvas.Children.Clear();

            // reloads all the ui
            EbxAssetEntry openedAsset = App.EditorWindow.GetOpenedAssetEntry() as EbxAssetEntry;
            LoadUI(openedAsset, false, null);

            _uiCanvas.UpdateLayout();

            App.Logger.Log("Refreshed UI");
        }

        // unused thing that was gonna add ui elements
        private void AddObjectButton_Click(object sender, RoutedEventArgs e)
        {
            App.Logger.Log("added object");
        }
    }
}
