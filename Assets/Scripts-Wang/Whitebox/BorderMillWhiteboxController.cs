using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
using UnityEngine.UI;

public enum WhiteboxTokenType
{
    Wheat,
    Flour,
    Water,
    Arrow,
    Dough,
    Bread
}

public enum WhiteboxDropZoneType
{
    Cart,
    KitchenDoor,
    CellarEntrance,
    PrepBoard,
    Firebox,
    Oven,
    CellarDelivery
}

public sealed class BorderMillWhiteboxController : MonoBehaviour
{
    private enum WhiteboxCartNodeId
    {
        MillRoad,
        MillStone,
        FieldRoad,
        FieldHarvest,
        KitchenDoor,
        CellarWell,
        CellarDoor
    }

    private enum WhiteboxIndoorRoom
    {
        None,
        Kitchen,
        Cellar
    }

    private enum KitchenProcessState
    {
        Empty,
        FlourPlaced,
        WaterAdded,
        DoughMixed,
        DoughFermented,
        DoughMolded,
        DoughInOven,
        Baking,
        BreadReady,
        BreadAtDoor
    }

    private sealed class RegionView
    {
        public RegionID Region;
        public RectTransform Root;
        public Image Background;
        public Sprite SpringBackground;
        public Sprite WarBackground;
        public Sprite AutumnBackground;
        public Text Title;
        public Text State;
        public Text Hint;
        public RectTransform TokenLayer;
        public Slider TimeSlider;
        public Text TimeSliderLabel;
        public readonly List<Button> ActionButtons = new List<Button>();
    }

    private sealed class CartNode
    {
        public WhiteboxCartNodeId Id;
        public string Name;
        public RegionID Region;
        public Vector2 BoardPosition;
        public bool UseFacingOverride;
        public CartDirection FacingOverride;
        public Button Button;
        public CanvasGroup ButtonCanvasGroup;
        public float ButtonBaseAlpha = 1f;
    }

    private sealed class RoadEdge
    {
        public WhiteboxCartNodeId A;
        public WhiteboxCartNodeId B;
        public RegionID[] RequiredSpringRegions;
    }

    private readonly Dictionary<RegionID, TimeState> regionTimes = new Dictionary<RegionID, TimeState>();
    private readonly Dictionary<RegionID, RegionView> regionViews = new Dictionary<RegionID, RegionView>();
    private readonly Dictionary<WhiteboxCartNodeId, CartNode> cartNodes = new Dictionary<WhiteboxCartNodeId, CartNode>();
    private readonly Dictionary<RegionID, Coroutine> regionFadeRoutines = new Dictionary<RegionID, Coroutine>();
    private readonly List<RoadEdge> roadEdges = new List<RoadEdge>();
    private readonly List<WhiteboxDraggableToken> activeTokens = new List<WhiteboxDraggableToken>();
    private readonly List<string> logLines = new List<string>();

    [System.Serializable]
    private sealed class RegionBinding
    {
        public RectTransform Root;
        public Image Background;
        public Sprite SpringBackground;
        public Sprite WarBackground;
        public Sprite AutumnBackground;
        public Text Title;
        public Text State;
        public Text Hint;
        public RectTransform TokenLayer;
        public Slider TimeSlider;
        public Text TimeSliderLabel;
    }

    [System.Serializable]
    private sealed class TokenSpriteBinding
    {
        public WhiteboxTokenType TokenType;
        public Sprite Sprite;
    }

    [System.Serializable]
    private sealed class CartNodeSpriteBinding
    {
        public WhiteboxCartNodeId Node;
        public CartCargo Cargo;
        public CartDirection Direction;
        public Sprite Sprite;
    }

    [System.Serializable]
    private sealed class CartDirectionSpriteBinding
    {
        public CartCargo Cargo;
        public CartDirection Direction;
        public Sprite Sprite;
    }

    [System.Serializable]
    private sealed class CartMoveSpriteBinding
    {
        public WhiteboxCartNodeId From;
        public WhiteboxCartNodeId To;
        public CartCargo Cargo;
        public Sprite Sprite;
    }

    [System.Serializable]
    private sealed class CartPointBinding
    {
        public bool Enabled = true;
        public WhiteboxCartNodeId Node;
        public string Name;
        public RegionID Region;
        public Vector2 BoardPosition;
        public bool UseFacingOverride = true;
        public CartDirection Facing = CartDirection.Right;
        public Button Button;
    }

    [System.Serializable]
    private sealed class CartRoadBinding
    {
        public bool Enabled = true;
        public WhiteboxCartNodeId From;
        public WhiteboxCartNodeId To;
        public List<RegionID> RequiredSpringRegions = new List<RegionID>();
    }

    [Header("Main UI - drag existing scene objects here")]
    [SerializeField] private RectTransform root;
    [SerializeField] private RectTransform sceneBoard;
    [SerializeField] private RectTransform boardOverlay;
    [SerializeField] private RectTransform dragLayer;

    [Header("Region UI")]
    [SerializeField] private RegionBinding millRegion;
    [SerializeField] private RegionBinding fieldRegion;
    [SerializeField] private RegionBinding kitchenRegion;
    [SerializeField] private RegionBinding cellarRegion;

    [Header("Road Node Buttons")]
    [SerializeField] private Button millRoadButton;
    [SerializeField] private Button millStoneButton;
    [SerializeField] private Button fieldRoadButton;
    [SerializeField] private Button fieldHarvestNodeButton;
    [SerializeField] private Button kitchenDoorNodeButton;
    [SerializeField] private Button cellarWellButton;
    [SerializeField] private Button cellarDoorButton;
    [SerializeField] private bool blinkReachableCartPoints = true;
    [SerializeField] private float reachablePointBlinkSpeed = 4f;
    [SerializeField] private float reachablePointMinAlpha = 0.38f;

    [Header("Region Action Buttons")]
    [SerializeField] private Button harvestWheatButton;
    [SerializeField] private Button openKitchenButton;
    [SerializeField] private Button collectWaterButton;
    [SerializeField] private Button openCellarButton;

    [Header("Outdoor Drop Zones")]
    [SerializeField] private RectTransform kitchenDoorDropZoneRect;
    [SerializeField] private RectTransform cellarEntranceDropZoneRect;

    [Header("Cart UI")]
    [SerializeField] private RectTransform cartVisual;
    [SerializeField] private Image cartImage;
    [SerializeField] private Text cartText;
    [SerializeField] private Vector2 cartVisualSize = new Vector2(104f, 64f);

    [Header("Cart Points - optional custom graph")]
    [SerializeField] private bool useCustomCartPoints;
    [SerializeField] private List<CartPointBinding> cartPoints = new List<CartPointBinding>();
    [SerializeField] private List<CartRoadBinding> cartRoads = new List<CartRoadBinding>();

    [Header("Cart Art - optional sprite overrides")]
    [SerializeField] private Sprite emptyCartSprite;
    [SerializeField] private Sprite wheatCartSprite;
    [SerializeField] private Sprite flourCartSprite;
    [SerializeField] private Sprite waterCartSprite;
    [SerializeField] private Sprite breadCartSprite;
    [SerializeField] private List<CartDirectionSpriteBinding> cartDirectionSprites = new List<CartDirectionSpriteBinding>();
    [SerializeField] private List<CartNodeSpriteBinding> cartPointSprites = new List<CartNodeSpriteBinding>();
    [SerializeField] private List<CartMoveSpriteBinding> cartMoveSprites = new List<CartMoveSpriteBinding>();

    [Header("Token Art - optional item sprites")]
    [SerializeField] private List<TokenSpriteBinding> tokenSprites = new List<TokenSpriteBinding>();

    [Header("Rocket Event Art")]
    [SerializeField] private Sprite kitchenRocketBackgroundSprite;
    [Header("War Rocket Animation")]
    [SerializeField] private Sprite rocketSprite;
    [SerializeField] private List<Sprite> rocketFrames = new List<Sprite>();
    [SerializeField] private Vector2 rocketSize = new Vector2(160f, 90f);
    [SerializeField] private float rocketMoveDuration = 0.8f;
    [SerializeField] private float rocketInterval = 2.2f;
    [SerializeField] private float rocketFrameSeconds = 0.08f;
    [SerializeField] private float rocketRotationZ = 0f;
    private readonly Dictionary<RegionID, Image> regionRocketImages = new Dictionary<RegionID, Image>();
    private readonly Dictionary<RegionID, Coroutine> regionRocketRoutines = new Dictionary<RegionID, Coroutine>();
    [Header("Season Rain Animation")]
    [SerializeField] private List<Sprite> rainFrames = new List<Sprite>();
    [SerializeField] private float rainFrameSeconds = 0.08f;

    private readonly Dictionary<RegionID, Image> regionRainImages = new Dictionary<RegionID, Image>();
    private readonly Dictionary<RegionID, Coroutine> regionRainRoutines = new Dictionary<RegionID, Coroutine>();
    [Header("Season Animator FX")]
    [SerializeField] private RectTransform rainFxRoot;
    [SerializeField] private Animator rainAnimator;
    [SerializeField] private float rainFxDuration = 1.2f;

    private Coroutine rainAnimatorRoutine;
    [Header("Season Change Animation")]
    [SerializeField] private Image rainFxImage;

    [SerializeField] private float rainDuration = 1.2f;

    [SerializeField] private Image rocketFxImage;
   
    [SerializeField] private float rocketDuration = 1.2f;

    private Coroutine rainFxRoutine;
    private Coroutine rocketFxRoutine;

    [Header("Mill Windmill Animation")]
    [SerializeField] private Image windmillImage;
    [SerializeField] private List<Sprite> windmillFrames = new List<Sprite>();
    [SerializeField] private float windmillFrameSeconds = 0.08f;

    private Coroutine windmillRoutine;

    [Header("Indoor UI")]
    [SerializeField] private RectTransform indoorRoot;
    [SerializeField] private RectTransform kitchenIndoorRoot;
    [SerializeField] private RectTransform cellarIndoorRoot;
    [SerializeField] private Text indoorTitle;
    [SerializeField] private Text indoorHint;
    [SerializeField] private Slider indoorTimeSlider;
    [SerializeField] private Text indoorTimeSliderLabel;
    [SerializeField] private RectTransform indoorTokenLayer;
    [SerializeField] private Button closeIndoorButton;
    [SerializeField] private Button indoorPrepBoardButton;
    [SerializeField] private RectTransform indoorPrepBoardDropZoneRect;
    [SerializeField] private RectTransform indoorFireboxDropZoneRect;
    [SerializeField] private RectTransform indoorOvenDropZoneRect;
    [SerializeField] private RectTransform indoorKitchenDoorDropZoneRect;
    [SerializeField] private Image indoorPrepBoardImage;
    [SerializeField] private Image indoorOvenImage;

    [Header("Indoor Background Art")]
    [SerializeField] private Image kitchenIndoorBackgroundImage;
    [SerializeField] private Image cellarIndoorBackgroundImage;
    [SerializeField] private Image kitchenIndoorSeasonFilterImage;
    [SerializeField] private Image cellarIndoorSeasonFilterImage;
    [SerializeField] private Color indoorSpringFilterColor = new Color(0.56f, 0.86f, 0.48f, 0.18f);
    [SerializeField] private Color indoorWarFilterColor = new Color(0.1f, 0.14f, 0.26f, 0.24f);
    [SerializeField] private Color indoorAutumnFilterColor = new Color(0.95f, 0.62f, 0.16f, 0.2f);
    [SerializeField] private Image kitchenWindowPatchImage;
    [SerializeField] private Sprite kitchenWindowSpringSprite;
    [SerializeField] private Sprite kitchenWindowWarSprite;
    [SerializeField] private Sprite kitchenWindowAutumnSprite;

    [Header("Cellar Object Art")]
    [SerializeField] private Image cellarEmptyBasketImage;
    [SerializeField] private Image cellarBreadBasketImage;
    [SerializeField] private Image cellarGirlImage;
    [SerializeField] private Text cellarGirlText;
    [SerializeField] private List<Sprite> cellarGirlWaitingSprites = new List<Sprite>();
    [SerializeField] private List<Sprite> cellarGirlEatingSprites = new List<Sprite>();
    [SerializeField] private float cellarGirlAnimationFrameSeconds = 0.18f;

    [Header("Kitchen Object Art")]
    [SerializeField] private Image kitchenDoorFlourImage;
    [SerializeField] private Image kitchenDoorWaterImage;
    [SerializeField] private Image kitchenRawDoughImage;
    [SerializeField] private Image kitchenFermentedDoughImage;
    [SerializeField] private Image kitchenMoldedDoughImage;
    [SerializeField] private Image kitchenBreadReadyImage;
    [SerializeField] private Sprite prepBoardEmptySprite;
    [SerializeField] private Sprite prepBoardFlourSprite;
    [SerializeField] private Sprite prepBoardFlourWaterSprite;
    [SerializeField] private Sprite prepBoardDoughSprite;
    [SerializeField] private Sprite prepBoardFermentedDoughSprite;
    [SerializeField] private Sprite prepBoardMoldedDoughSprite;
    [SerializeField] private Image kitchenFireImage;
    [SerializeField] private Sprite kitchenFireLitSprite;
    [SerializeField] private Sprite ovenDoughSprite;
    [SerializeField] private Sprite ovenBakingSprite;
    [SerializeField] private Sprite ovenBreadSprite;

    [Header("HUD / Ending")]
    [SerializeField] private Text statusText;
    [SerializeField] private Text checklistText;
    [SerializeField] private Text logText;
    [SerializeField] private RectTransform finalOverlay;
    [SerializeField] private Text finalText;
    [SerializeField] private Button finalRestartButton;
    [SerializeField] private Button finalQuitButton;

    private RectTransform cellarEmptyBasketVisual;
    private RectTransform cellarBreadBasketVisual;
    private RectTransform cellarGirlVisual;
    private CanvasGroup finalCanvasGroup;
    private Coroutine endingRoutine;
    private Coroutine bakeRoutine;
    private WhiteboxDropZone cartDropZone;
    private WhiteboxDropZone kitchenDoorDropZone;
    private WhiteboxDropZone cellarEntranceDropZone;
    private WhiteboxDropZone indoorPrepBoardDropZone;
    private WhiteboxDropZone indoorFireboxDropZone;
    private WhiteboxDropZone indoorOvenDropZone;
    private WhiteboxDropZone indoorKitchenDoorDropZone;
    private Font uiFont;
    private WhiteboxDraggableToken cartCargoToken;
    private CanvasGroup indoorCanvasGroup;
    private Coroutine indoorFadeRoutine;
    private WhiteboxCartNodeId movingFromNode;
    private WhiteboxCartNodeId movingToNode;
    private bool hasActiveMoveSegment;

    private WhiteboxCartNodeId cartNodeId;
    private CartCargo cartCargo;
    private CartDirection cartDirection;
    private WhiteboxIndoorRoom currentIndoorRoom;

    private KitchenProcessState kitchenProcess;
    private bool flourAtKitchenDoor;
    private bool waterAtKitchenDoor;
    private bool breadAtCellarEntrance;
    private bool wheatHarvested;
    private bool waterCollected;
    private bool rocketTriggered;
    private bool ovenLit;
    private bool breadDestroyed;
    private bool girlFed;
    private bool timeLocked;
    private bool cartMoving;

    private readonly Color ink = new Color(0.12f, 0.12f, 0.12f, 1f);
    private readonly Color muted = new Color(0.34f, 0.35f, 0.36f, 1f);
    private readonly Color spring = new Color(0.42f, 0.78f, 0.47f, 0.22f);
    private readonly Color war = new Color(0.1f, 0.14f, 0.26f, 0.28f);
    private readonly Color autumn = new Color(0.95f, 0.66f, 0.18f, 0.26f);

    public RectTransform DragLayer
    {
        get { return dragLayer; }
    }

    private void Awake()
    {
        uiFont = CreateReadableUIFont();
        EnsureEventSystem();
        BindInspectorInterface();
        InitializeState();

        if (indoorRoot != null)
        {
            indoorRoot.gameObject.SetActive(false);
        }

        if (finalOverlay != null)
        {
            finalOverlay.gameObject.SetActive(false);
        }
        if (rainFxRoot != null)
        {
            rainFxRoot.gameObject.SetActive(false);
        }
        if (rainFxImage != null)
        {
            rainFxImage.gameObject.SetActive(false);
            rainFxImage.raycastTarget = false;
        }

        if (rocketFxImage != null)
        {
            rocketFxImage.gameObject.SetActive(false);
            rocketFxImage.raycastTarget = false;
        }
        CreateRegionRainImages();
        CreateRegionRocketImages();

        RefreshAll();
        RefreshWindmillFx();
        RefreshRegionRainFx();
        RefreshRegionRocketFx();

        AddLog("初始全区域为春天。按道路点移动，跨格路段需要相关区域保持春天。");
    }

    private void Update()
    {
        UpdateCellarGirlAnimation();
        UpdateCartPointBlink();
    }

    private Font CreateReadableUIFont()
    {
        string[] preferredFonts =
        {
            "Microsoft YaHei",
            "SimHei",
            "Microsoft JhengHei",
            "Arial Unicode MS",
            "Arial"
        };

        Font font = Font.CreateDynamicFontFromOSFont(preferredFonts, 18);
        return font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private void BindInspectorInterface()
    {
        regionViews.Clear();

        RegisterRegionView(RegionID.Mill, millRegion);
        RegisterRegionView(RegionID.Field, fieldRegion, harvestWheatButton);
        RegisterRegionView(RegionID.Kitchen, kitchenRegion, openKitchenButton);
        RegisterRegionView(RegionID.Cellar, cellarRegion, collectWaterButton, openCellarButton);

        BuildRoadGraph();
        BindRoadNodeButton(WhiteboxCartNodeId.MillRoad, millRoadButton);
        BindRoadNodeButton(WhiteboxCartNodeId.MillStone, millStoneButton);
        BindRoadNodeButton(WhiteboxCartNodeId.FieldRoad, fieldRoadButton);
        BindRoadNodeButton(WhiteboxCartNodeId.FieldHarvest, fieldHarvestNodeButton);
        BindRoadNodeButton(WhiteboxCartNodeId.KitchenDoor, kitchenDoorNodeButton);
        BindRoadNodeButton(WhiteboxCartNodeId.CellarWell, cellarWellButton);
        BindRoadNodeButton(WhiteboxCartNodeId.CellarDoor, cellarDoorButton);
        BindCustomCartPointButtons();
        CaptureCartNodePositionsFromButtons();

        BindButton(harvestWheatButton, SpawnWheatToken);
        BindButton(openKitchenButton, OpenKitchen);
        BindButton(collectWaterButton, CollectWater);
        BindButton(openCellarButton, OpenCellar);
        BindButton(closeIndoorButton, CloseIndoor);
        BindButton(indoorPrepBoardButton, KneadDough);
        BindButton(finalRestartButton, ResetPrototype);
        BindButton(finalQuitButton, QuitGame);
        BindIndoorTimeSlider();
        EnsureDefaultSceneReferences();
        AutoBindIndoorImagesFromHierarchy();
        SyncCellarHierarchyReferences();
        AutoBindIndoorDropZonesFromHierarchy();

        kitchenDoorDropZone = SetupDropZone(kitchenDoorDropZoneRect, WhiteboxDropZoneType.KitchenDoor, RegionID.Kitchen);
        cellarEntranceDropZone = SetupDropZone(cellarEntranceDropZoneRect, WhiteboxDropZoneType.CellarEntrance, RegionID.Cellar);
        indoorPrepBoardDropZone = SetupDropZone(indoorPrepBoardDropZoneRect, WhiteboxDropZoneType.PrepBoard, RegionID.Kitchen);
        indoorFireboxDropZone = SetupDropZone(indoorFireboxDropZoneRect, WhiteboxDropZoneType.Firebox, RegionID.Kitchen);
        indoorOvenDropZone = SetupDropZone(indoorOvenDropZoneRect, WhiteboxDropZoneType.Oven, RegionID.Kitchen);
        indoorKitchenDoorDropZone = SetupDropZone(indoorKitchenDoorDropZoneRect, WhiteboxDropZoneType.KitchenDoor, RegionID.Kitchen);

        if (cartVisual != null)
        {
            if (cartImage == null)
            {
                cartImage = cartVisual.GetComponent<Image>();
            }

            cartDropZone = cartVisual.GetComponent<WhiteboxDropZone>();
            if (cartDropZone == null)
            {
                cartDropZone = cartVisual.gameObject.AddComponent<WhiteboxDropZone>();
            }

            cartDropZone.Initialize(this, WhiteboxDropZoneType.Cart, GetCartRegion());
        }

        if (dragLayer == null && root != null)
        {
            GameObject dragLayerObject = new GameObject("Runtime DragLayer", typeof(RectTransform), typeof(Image));
            dragLayerObject.transform.SetParent(root, false);
            dragLayer = dragLayerObject.GetComponent<RectTransform>();
            Stretch(dragLayer);
            Image image = dragLayerObject.GetComponent<Image>();
            image.color = Color.clear;
            image.raycastTarget = false;
        }

        if (dragLayer != null)
        {
            dragLayer.SetAsLastSibling();
        }
    }

    private void BindIndoorTimeSlider()
    {
        if (indoorTimeSlider == null)
        {
            return;
        }

        indoorTimeSlider.wholeNumbers = true;
        indoorTimeSlider.minValue = 0f;
        indoorTimeSlider.maxValue = 2f;
        indoorTimeSlider.onValueChanged.RemoveAllListeners();
        indoorTimeSlider.onValueChanged.AddListener(delegate(float value)
        {
            if (currentIndoorRoom == WhiteboxIndoorRoom.None)
            {
                return;
            }

            SetIndoorTime(SliderValueToTime(value));
        });
    }

    private void EnsureDefaultSceneReferences()
    {
        Canvas canvas = root != null ? root.GetComponentInParent<Canvas>() : null;
        if (canvas != null && canvas.GetComponent<GraphicRaycaster>() == null)
        {
            canvas.gameObject.AddComponent<GraphicRaycaster>();
        }

        if (cartVisual != null)
        {
            if (cartImage == null)
            {
                cartImage = cartVisual.GetComponent<Image>();
            }

            if (cartImage == null)
            {
                cartImage = cartVisual.gameObject.AddComponent<Image>();
                cartImage.color = new Color(1f, 1f, 1f, 0.92f);
            }

            cartImage.raycastTarget = true;
        }

        if (cartText != null)
        {
            cartText.raycastTarget = false;
        }

        if (indoorRoot != null)
        {
            DeactivateLegacyIndoorControls();
        }
    }

    private void SyncCellarHierarchyReferences()
    {
        if (cellarEmptyBasketImage != null)
        {
            cellarEmptyBasketVisual = cellarEmptyBasketImage.rectTransform;
        }

        if (cellarBreadBasketImage != null)
        {
            cellarBreadBasketVisual = cellarBreadBasketImage.rectTransform;
        }

        if (cellarGirlImage != null)
        {
            cellarGirlVisual = cellarGirlImage.rectTransform;
            if (cellarGirlText == null)
            {
                cellarGirlText = FindFirstTextChild(cellarGirlVisual);
            }
        }
    }

    private Text FindFirstTextChild(Transform parent)
    {
        if (parent == null)
        {
            return null;
        }

        Text ownText = parent.GetComponent<Text>();
        if (ownText != null)
        {
            return ownText;
        }

        return parent.GetComponentInChildren<Text>(true);
    }

    private void AutoBindIndoorImagesFromHierarchy()
    {
        if (indoorRoot == null)
        {
            return;
        }

        if (kitchenIndoorBackgroundImage == null)
        {
            kitchenIndoorBackgroundImage = FindFirstImageChild(indoorRoot, "kitchenBG", "KitchenBG", "KitchenBackground");
        }

        if (cellarIndoorBackgroundImage == null)
        {
            cellarIndoorBackgroundImage = FindFirstImageChild(indoorRoot, "BG", "CellarBG", "CellarBackground");
        }

        if (kitchenIndoorSeasonFilterImage == null)
        {
            kitchenIndoorSeasonFilterImage = FindFirstImageChild(kitchenIndoorRoot != null ? kitchenIndoorRoot : indoorRoot, "Filter", "KitchenFilter");
        }

        if (cellarIndoorSeasonFilterImage == null)
        {
            cellarIndoorSeasonFilterImage = FindFirstImageChild(cellarIndoorRoot != null ? cellarIndoorRoot : indoorRoot, "Filter", "CellarFilter");
        }

        if (kitchenWindowPatchImage == null)
        {
            kitchenWindowPatchImage = FindFirstImageChild(indoorRoot, "KCWindow", "KitchenWindow", "Window");
        }

        if (indoorPrepBoardImage == null)
        {
            indoorPrepBoardImage = FindFirstImageChild(indoorRoot, "DefaultPrepBoardImage", "PrepBoard", "案板");
        }

        if (indoorOvenImage == null)
        {
            indoorOvenImage = FindFirstImageChild(indoorRoot, "DefaultOvenImage", "Oven", "烤炉");
        }

        if (kitchenFireImage == null)
        {
            kitchenFireImage = FindFirstImageChild(indoorRoot, "Fire", "KitchenFire", "火");
        }

        if (kitchenDoorFlourImage == null)
        {
            kitchenDoorFlourImage = FindFirstImageChild(indoorRoot, "Flour", "DoorFlour", "Bag", "面粉");
        }

        if (kitchenDoorWaterImage == null)
        {
            kitchenDoorWaterImage = FindFirstImageChild(indoorRoot, "Water", "DoorWater", "清水");
        }

        if (kitchenRawDoughImage == null)
        {
            kitchenRawDoughImage = FindFirstImageChild(indoorRoot, "RawDough", "生面团");
        }

        if (kitchenFermentedDoughImage == null)
        {
            kitchenFermentedDoughImage = FindFirstImageChild(indoorRoot, "FermentedDough", "发酵面团");
        }

        if (kitchenMoldedDoughImage == null)
        {
            kitchenMoldedDoughImage = FindFirstImageChild(indoorRoot, "MoldedDough", "发霉面团");
        }

        if (kitchenBreadReadyImage == null)
        {
            kitchenBreadReadyImage = FindFirstImageChild(indoorRoot, "Bread", "KitchenBread", "面包");
        }

        if (cellarEmptyBasketImage == null)
        {
            cellarEmptyBasketImage = FindFirstImageChild(indoorRoot, "EmptyBasket", "空篮子");
        }

        if (cellarBreadBasketImage == null)
        {
            cellarBreadBasketImage = FindFirstImageChild(indoorRoot, "Basket", "BreadBasket", "装面包篮子");
        }

        if (cellarGirlImage == null)
        {
            cellarGirlImage = FindFirstImageChild(indoorRoot, "LittleGirl", "CellarGirl", "小女孩");
        }
    }

    private Image FindFirstImageChild(Transform parent, params string[] names)
    {
        for (int i = 0; i < names.Length; i++)
        {
            Transform child = FindDeepChild(parent, names[i]);
            if (child != null)
            {
                Image image = child.GetComponent<Image>();
                if (image != null)
                {
                    return image;
                }
            }
        }

        return null;
    }

    private void AutoBindIndoorDropZonesFromHierarchy()
    {
        if (indoorRoot == null)
        {
            return;
        }

        if (indoorPrepBoardDropZoneRect == null)
        {
            indoorPrepBoardDropZoneRect = FindFirstRectChild(indoorRoot, "DefaultPrepBoardDropZone", "PrepBoardDropZone", "案板投放区");
        }

        if (indoorFireboxDropZoneRect == null)
        {
            indoorFireboxDropZoneRect = FindFirstRectChild(indoorRoot, "FireboxDropZone", "炉膛投放区");
        }

        if (indoorOvenDropZoneRect == null)
        {
            indoorOvenDropZoneRect = FindFirstRectChild(indoorRoot, "DefaultOvenDropZone", "OvenDropZone", "烤炉投放区");
        }

        if (indoorKitchenDoorDropZoneRect == null)
        {
            indoorKitchenDoorDropZoneRect = FindFirstRectChild(indoorRoot, "DefaultKitchenDoorExitDropZone", "DoorDropZone (1)", "DoorDropZone", "KitchenDoorExit", "厨房门口投放区");
        }
    }

    private RectTransform FindFirstRectChild(Transform parent, params string[] names)
    {
        for (int i = 0; i < names.Length; i++)
        {
            Transform child = FindDeepChild(parent, names[i]);
            if (child != null)
            {
                return child as RectTransform;
            }
        }

        return null;
    }

    private void DeactivateLegacyIndoorControls()
    {
        string[] legacyNames =
        {
            "IndoorPlaceFlour",
            "IndoorAddWater",
            "IndoorKneadDough",
            "IndoorLoadOven",
            "TakeBreadIntoCellar",
            "IndoorMoveBreadDoor",
            "KitchenProcessMarker",
            "IndoorProcessMarker",
            "KitchenIndoorProcessMarker"
        };

        for (int i = 0; i < legacyNames.Length; i++)
        {
            Transform child = FindDeepChild(indoorRoot, legacyNames[i]);
            if (child != null)
            {
                child.gameObject.SetActive(false);
            }
        }
    }

    private void SetKitchenOnlyNamedObjects(bool active)
    {
        string[] kitchenOnlyNames =
        {
            "DefaultPrepBoardImage",
            "DefaultPrepBoardDropZone",
            "DefaultPrepBoardButton",
            "DefaultOvenImage",
            "DefaultOvenDropZone",
            "DefaultKitchenDoorExitDropZone",
            "KitchenRawDough",
            "KitchenFermentedDough",
            "KitchenMoldedDough",
            "KitchenBreadReady",
            "IndoorPrepBoardButton",
            "PrepBoard",
            "Firebox",
            "Oven",
            "KitchenDoorExit",
            "案板",
            "炉膛",
            "烤炉",
            "厨房门口"
        };

        for (int i = 0; i < kitchenOnlyNames.Length; i++)
        {
            SetDeepChildrenActiveByName(indoorRoot, kitchenOnlyNames[i], active);
        }

        DeactivateLegacyIndoorControls();
    }

    private void HideCellarOnlyNamedObjectsInKitchen()
    {
        string[] cellarOnlyNames =
        {
            "CellarGirlMarker",
            "CellarBasket",
            "CellarEmptyBasket",
            "CellarBreadBasket"
        };

        for (int i = 0; i < cellarOnlyNames.Length; i++)
        {
            SetDeepChildrenActiveByName(indoorRoot, cellarOnlyNames[i], false);
        }
    }

    private void HideLegacyCellarDeliveryObjects()
    {
        string[] legacyNames =
        {
            "CellarDelivery",
            "IndoorCellarDropZone"
        };

        for (int i = 0; i < legacyNames.Length; i++)
        {
            SetDeepChildrenActiveByName(indoorRoot, legacyNames[i], false);
        }
    }

    private void SetDeepChildrenActiveByName(Transform parent, string childName, bool active)
    {
        if (parent == null)
        {
            return;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName || child.name.Contains(childName))
            {
                child.gameObject.SetActive(active);
            }

            SetDeepChildrenActiveByName(child, childName, active);
        }
    }

    private Transform FindDeepChild(Transform parent, string childName)
    {
        if (parent == null)
        {
            return null;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName)
            {
                return child;
            }

            Transform nested = FindDeepChild(child, childName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private void RegisterRegionView(RegionID region, RegionBinding binding, params Button[] actionButtons)
    {
        if (binding == null)
        {
            Debug.LogError("BorderMillWhiteboxController: RegionBinding missing for " + region + ".", this);
            return;
        }

        RegionView view = new RegionView();
        view.Region = region;
        view.Root = binding.Root;
        view.Background = binding.Background != null ? binding.Background : (binding.Root != null ? binding.Root.GetComponent<Image>() : null);
        view.SpringBackground = binding.SpringBackground;
        view.WarBackground = binding.WarBackground;
        view.AutumnBackground = binding.AutumnBackground;
        view.Title = binding.Title;
        view.State = binding.State;
        view.Hint = binding.Hint;
        view.TokenLayer = binding.TokenLayer;
        view.TimeSlider = binding.TimeSlider;
        view.TimeSliderLabel = binding.TimeSliderLabel;

        if (view.TimeSlider != null)
        {
            view.TimeSlider.wholeNumbers = true;
            view.TimeSlider.minValue = 0f;
            view.TimeSlider.maxValue = 2f;
            view.TimeSlider.onValueChanged.RemoveAllListeners();
            view.TimeSlider.onValueChanged.AddListener(delegate(float value)
            {
                SetRegionTime(region, SliderValueToTime(value));
            });
        }

        if (actionButtons != null)
        {
            for (int i = 0; i < actionButtons.Length; i++)
            {
                if (actionButtons[i] != null)
                {
                    view.ActionButtons.Add(actionButtons[i]);
                }
            }
        }

        regionViews[region] = view;
    }

    private void BindRoadNodeButton(WhiteboxCartNodeId id, Button button)
    {
        if (!cartNodes.ContainsKey(id))
        {
            if (button != null && useCustomCartPoints)
            {
                button.gameObject.SetActive(false);
            }

            return;
        }

        cartNodes[id].Button = button;
        if (button == null)
        {
            Debug.LogWarning("BorderMillWhiteboxController: Road node button missing: " + id + ".", this);
            return;
        }

        button.onClick.RemoveAllListeners();
        WhiteboxCartNodeId capturedId = id;
        button.onClick.AddListener(delegate { TryMoveCartToNode(capturedId); });
    }

    private void BindCustomCartPointButtons()
    {
        if (!useCustomCartPoints)
        {
            return;
        }

        for (int i = 0; i < cartPoints.Count; i++)
        {
            CartPointBinding binding = cartPoints[i];
            if (binding == null || !binding.Enabled || binding.Button == null || !cartNodes.ContainsKey(binding.Node))
            {
                continue;
            }

            CartNode node = cartNodes[binding.Node];
            node.Button = binding.Button;
            binding.Button.gameObject.SetActive(true);
            binding.Button.onClick.RemoveAllListeners();
            WhiteboxCartNodeId capturedId = binding.Node;
            binding.Button.onClick.AddListener(delegate { TryMoveCartToNode(capturedId); });
        }
    }

    private void CaptureCartNodePositionsFromButtons()
    {
        foreach (CartNode node in cartNodes.Values)
        {
            if (node.Button == null)
            {
                continue;
            }

            RectTransform buttonRect = node.Button.GetComponent<RectTransform>();
            if (buttonRect == null)
            {
                continue;
            }

            Vector2 normalizedPosition;
            if (TryGetNormalizedBoardPosition(buttonRect, out normalizedPosition))
            {
                node.BoardPosition = normalizedPosition;
            }
        }
    }

    private bool TryGetNormalizedBoardPosition(RectTransform rect, out Vector2 normalizedPosition)
    {
        normalizedPosition = Vector2.zero;
        RectTransform reference = boardOverlay != null ? boardOverlay : rect.parent as RectTransform;
        if (reference == null)
        {
            return false;
        }

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(null, rect.position);
        Vector2 localPoint;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(reference, screenPoint, null, out localPoint))
        {
            return false;
        }

        Rect referenceRect = reference.rect;
        if (Mathf.Approximately(referenceRect.width, 0f) || Mathf.Approximately(referenceRect.height, 0f))
        {
            return false;
        }

        normalizedPosition = new Vector2(
            Mathf.InverseLerp(referenceRect.xMin, referenceRect.xMax, localPoint.x),
            Mathf.InverseLerp(referenceRect.yMin, referenceRect.yMax, localPoint.y));
        return true;
    }

    private void BindButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            image.raycastTarget = true;
        }

        button.interactable = true;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    private WhiteboxDropZone SetupDropZone(RectTransform rect, WhiteboxDropZoneType zoneType, RegionID region)
    {
        if (rect == null)
        {
            return null;
        }

        Image image = rect.GetComponent<Image>();
        if (image == null)
        {
            image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.01f);
        }

        image.raycastTarget = true;

        WhiteboxDropZone zone = rect.GetComponent<WhiteboxDropZone>();
        if (zone == null)
        {
            zone = rect.gameObject.AddComponent<WhiteboxDropZone>();
        }

        zone.Initialize(this, zoneType, region);
        return zone;
    }

    private void InitializeState()
    {
        regionTimes[RegionID.Mill] = TimeState.Spring;
        regionTimes[RegionID.Field] = TimeState.Spring;
        regionTimes[RegionID.Kitchen] = TimeState.Spring;
        regionTimes[RegionID.Cellar] = TimeState.Spring;

        cartNodeId = WhiteboxCartNodeId.MillRoad;
        if (!cartNodes.ContainsKey(cartNodeId))
        {
            cartNodeId = GetFirstAvailableCartNode();
        }

        cartCargo = CartCargo.Empty;
        cartDirection = CartDirection.Right;
        currentIndoorRoom = WhiteboxIndoorRoom.None;

        kitchenProcess = KitchenProcessState.Empty;
        flourAtKitchenDoor = false;
        waterAtKitchenDoor = false;
        breadAtCellarEntrance = false;
        wheatHarvested = false;
        waterCollected = false;
        rocketTriggered = false;
        ovenLit = false;
        bakeRoutine = null;
        breadDestroyed = false;
        girlFed = false;
        timeLocked = false;
        cartMoving = false;
        hasActiveMoveSegment = false;

        if (TimeManager.Instance != null)
        {
            foreach (KeyValuePair<RegionID, TimeState> pair in regionTimes)
            {
                TimeManager.Instance.SetRegionTime(pair.Key, pair.Value);
            }
        }
    }

    private void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        InputSystemUIInputModule inputModule = eventSystemObject.AddComponent<InputSystemUIInputModule>();
        inputModule.AssignDefaultActions();
#else
        eventSystemObject.AddComponent<StandaloneInputModule>();
#endif
    }

    private void BuildRoadGraph()
    {
        cartNodes.Clear();
        roadEdges.Clear();

        if (useCustomCartPoints && cartPoints.Count > 0)
        {
            BuildCustomRoadGraph();
            return;
        }

        AddCartNode(WhiteboxCartNodeId.MillRoad, "磨坊路口", RegionID.Mill, new Vector2(0.35f, 0.63f), false, CartDirection.Right);
        AddCartNode(WhiteboxCartNodeId.MillStone, "风车石磨", RegionID.Mill, new Vector2(0.22f, 0.66f), false, CartDirection.Right);
        AddCartNode(WhiteboxCartNodeId.FieldRoad, "麦田路口", RegionID.Field, new Vector2(0.64f, 0.62f), false, CartDirection.Right);
        AddCartNode(WhiteboxCartNodeId.FieldHarvest, "可收割麦田", RegionID.Field, new Vector2(0.79f, 0.66f), false, CartDirection.Right);
        AddCartNode(WhiteboxCartNodeId.KitchenDoor, "厨房门口", RegionID.Kitchen, new Vector2(0.31f, 0.23f), false, CartDirection.Right);
        AddCartNode(WhiteboxCartNodeId.CellarWell, "地窖外路点", RegionID.Cellar, new Vector2(0.73f, 0.42f), false, CartDirection.Right);
        AddCartNode(WhiteboxCartNodeId.CellarDoor, "地窖入口", RegionID.Cellar, new Vector2(0.61f, 0.31f), false, CartDirection.Right);

        AddRoadEdge(WhiteboxCartNodeId.MillRoad, WhiteboxCartNodeId.MillStone, RegionID.Mill);
        AddRoadEdge(WhiteboxCartNodeId.MillRoad, WhiteboxCartNodeId.FieldRoad, RegionID.Mill, RegionID.Field);
        AddRoadEdge(WhiteboxCartNodeId.FieldRoad, WhiteboxCartNodeId.FieldHarvest, RegionID.Field);
        AddRoadEdge(WhiteboxCartNodeId.MillRoad, WhiteboxCartNodeId.KitchenDoor, RegionID.Mill, RegionID.Kitchen);
        AddRoadEdge(WhiteboxCartNodeId.FieldRoad, WhiteboxCartNodeId.CellarWell, RegionID.Field, RegionID.Cellar);
        AddRoadEdge(WhiteboxCartNodeId.CellarWell, WhiteboxCartNodeId.CellarDoor, RegionID.Cellar);
        AddRoadEdge(WhiteboxCartNodeId.KitchenDoor, WhiteboxCartNodeId.CellarDoor, RegionID.Kitchen, RegionID.Cellar);
    }

    private void BuildCustomRoadGraph()
    {
        for (int i = 0; i < cartPoints.Count; i++)
        {
            CartPointBinding binding = cartPoints[i];
            if (binding == null || !binding.Enabled)
            {
                continue;
            }

            string nodeName = string.IsNullOrEmpty(binding.Name) ? GetDefaultCartNodeName(binding.Node) : binding.Name;
            AddCartNode(binding.Node, nodeName, binding.Region, binding.BoardPosition, binding.UseFacingOverride, binding.Facing);
        }

        for (int i = 0; i < cartRoads.Count; i++)
        {
            CartRoadBinding binding = cartRoads[i];
            if (binding == null || !binding.Enabled || !cartNodes.ContainsKey(binding.From) || !cartNodes.ContainsKey(binding.To))
            {
                continue;
            }

            AddRoadEdge(binding.From, binding.To, binding.RequiredSpringRegions != null ? binding.RequiredSpringRegions.ToArray() : new RegionID[0]);
        }

        if (cartNodes.Count == 0)
        {
            AddCartNode(WhiteboxCartNodeId.MillRoad, "磨坊路口", RegionID.Mill, new Vector2(0.35f, 0.63f), false, CartDirection.Right);
        }
    }

    private string GetDefaultCartNodeName(WhiteboxCartNodeId id)
    {
        if (id == WhiteboxCartNodeId.MillRoad) return "磨坊路口";
        if (id == WhiteboxCartNodeId.MillStone) return "风车石磨";
        if (id == WhiteboxCartNodeId.FieldRoad) return "麦田路口";
        if (id == WhiteboxCartNodeId.FieldHarvest) return "可收割麦田";
        if (id == WhiteboxCartNodeId.KitchenDoor) return "厨房门口";
        if (id == WhiteboxCartNodeId.CellarWell) return "地窖外路点";
        if (id == WhiteboxCartNodeId.CellarDoor) return "地窖入口";
        return id.ToString();
    }

    private void AddCartNode(WhiteboxCartNodeId id, string name, RegionID region, Vector2 boardPosition, bool useFacingOverride, CartDirection facingOverride)
    {
        if (cartNodes.ContainsKey(id))
        {
            return;
        }

        CartNode node = new CartNode();
        node.Id = id;
        node.Name = name;
        node.Region = region;
        node.BoardPosition = boardPosition;
        node.UseFacingOverride = useFacingOverride;
        node.FacingOverride = facingOverride;
        cartNodes.Add(id, node);
    }

    private void AddRoadEdge(WhiteboxCartNodeId a, WhiteboxCartNodeId b, params RegionID[] requiredSpringRegions)
    {
        RoadEdge edge = new RoadEdge();
        edge.A = a;
        edge.B = b;
        edge.RequiredSpringRegions = requiredSpringRegions;
        roadEdges.Add(edge);
    }

    private WhiteboxCartNodeId GetFirstAvailableCartNode()
    {
        foreach (WhiteboxCartNodeId nodeId in cartNodes.Keys)
        {
            return nodeId;
        }

        return WhiteboxCartNodeId.MillRoad;
    }

    private void SetRegionTime(RegionID region, TimeState time)
    {
        if (timeLocked)
        {
            AddLog("结局后时间控制权被收回。");
            RefreshAll();
            RefreshRegionRainFx();
            return;
        }

        if (regionTimes[region] == time)
        {
            RefreshAll();
            RefreshRegionRainFx();
            return;
        }

        regionTimes[region] = time;

        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.SetRegionTime(region, time);
        }

        ApplyKitchenTimeConsequence(region, time);
        AddLog(GetRegionName(region) + "切换到" + GetTimeName(time) + "。");
        MaybeAutoMillWheat();
        CheckRocketEvent();

        RefreshAll();
        StartSceneTransitionFade(region);
        RefreshRegionRainFx();
        RefreshRegionRocketFx();
        RefreshWindmillFx();
    }
    private void RefreshWindmillFx()
    {
        bool shouldPlay =
            regionTimes.ContainsKey(RegionID.Mill) &&
            regionTimes[RegionID.Mill] == TimeState.Spring;

        if (shouldPlay)
        {
            StartWindmillFx();
        }
        else
        {
            StopWindmillFx();
        }
    }

    private void StartWindmillFx()
    {
        if (windmillImage == null || windmillFrames == null || windmillFrames.Count == 0)
        {
            return;
        }

        if (windmillRoutine != null)
        {
            return;
        }

        windmillRoutine = StartCoroutine(PlayWindmillLoop());
    }

    private void StopWindmillFx()
    {
        if (windmillRoutine != null)
        {
            StopCoroutine(windmillRoutine);
            windmillRoutine = null;
        }

        if (windmillImage != null)
        {
            Sprite firstFrame = GetFirstValidSprite(windmillFrames);

            if (firstFrame != null)
            {
                windmillImage.sprite = firstFrame;
                windmillImage.color = Color.white;
            }
        }
    }

    private IEnumerator PlayWindmillLoop()
    {
        if (windmillImage == null || windmillFrames == null || windmillFrames.Count == 0)
        {
            yield break;
        }

        Sprite firstFrame = GetFirstValidSprite(windmillFrames);
        if (firstFrame == null)
        {
            yield break;
        }

        windmillImage.gameObject.SetActive(true);
        windmillImage.raycastTarget = false;
        windmillImage.color = Color.white;
        windmillImage.preserveAspect = true;

        float safeFrameSeconds = Mathf.Max(0.03f, windmillFrameSeconds);
        int frameIndex = 0;

        while (regionTimes.ContainsKey(RegionID.Mill) &&
               regionTimes[RegionID.Mill] == TimeState.Spring)
        {
            Sprite frame = windmillFrames[frameIndex];

            if (frame != null)
            {
                windmillImage.sprite = frame;
            }

            frameIndex++;
            if (frameIndex >= windmillFrames.Count)
            {
                frameIndex = 0;
            }

            yield return new WaitForSeconds(safeFrameSeconds);
        }

        windmillRoutine = null;
    }

    private void CreateRegionRocketImages()
    {
        ClearRegionRocketImages();

        foreach (KeyValuePair<RegionID, RegionView> pair in regionViews)
        {
            RegionID region = pair.Key;
            RegionView view = pair.Value;

            if (view == null || view.Root == null)
            {
                continue;
            }

            GameObject clipObject = new GameObject("RocketClip_" + region, typeof(RectTransform), typeof(RectMask2D));
            clipObject.transform.SetParent(view.Root, false);

            RectTransform clipRect = clipObject.GetComponent<RectTransform>();
            Stretch(clipRect);

            RectMask2D mask = clipObject.GetComponent<RectMask2D>();
            mask.padding = Vector4.zero;
            mask.softness = Vector2Int.zero;

            GameObject rocketObject = new GameObject("RocketFX_" + region, typeof(RectTransform), typeof(Image));
            rocketObject.transform.SetParent(clipRect, false);

            RectTransform rocketRect = rocketObject.GetComponent<RectTransform>();
            rocketRect.anchorMin = new Vector2(0.5f, 0.5f);
            rocketRect.anchorMax = new Vector2(0.5f, 0.5f);
            rocketRect.pivot = new Vector2(0.5f, 0.5f);
            rocketRect.sizeDelta = rocketSize;
            rocketRect.anchoredPosition = Vector2.zero;
            rocketRect.localRotation = Quaternion.Euler(0f, 0f, -135f);

            Image rocketImage = rocketObject.GetComponent<Image>();
            rocketImage.raycastTarget = false;
            rocketImage.preserveAspect = true;
            rocketImage.sprite = null;
            rocketImage.color = new Color(1f, 1f, 1f, 0f);

            rocketObject.SetActive(false);

            clipObject.transform.SetAsLastSibling();
            rocketObject.transform.SetAsLastSibling();

            regionRocketImages[region] = rocketImage;
        }
    }
    private void ClearRegionRocketImages()
    {
        foreach (KeyValuePair<RegionID, Coroutine> pair in regionRocketRoutines)
        {
            if (pair.Value != null)
            {
                StopCoroutine(pair.Value);
            }
        }

        regionRocketRoutines.Clear();

        foreach (KeyValuePair<RegionID, Image> pair in regionRocketImages)
        {
            if (pair.Value != null)
            {
                Transform clipRoot = pair.Value.transform.parent;

                if (clipRoot != null && clipRoot.name.StartsWith("RocketClip_"))
                {
                    Destroy(clipRoot.gameObject);
                }
                else
                {
                    Destroy(pair.Value.gameObject);
                }
            }
        }

        regionRocketImages.Clear();
    }
    private void RefreshRegionRocketFx()
    {
        foreach (KeyValuePair<RegionID, Image> pair in regionRocketImages)
        {
            RegionID region = pair.Key;

            bool shouldPlayRocket =
                regionTimes.ContainsKey(region) &&
                regionTimes[region] == TimeState.War;

            if (shouldPlayRocket)
            {
                StartRegionRocket(region);
            }
            else
            {
                StopRegionRocket(region);
            }
        }
    }
    private void StartRegionRocket(RegionID region)
    {
        Image image;
        if (!regionRocketImages.TryGetValue(region, out image) || image == null)
        {
            return;
        }

        bool hasRocketArt = rocketSprite != null || GetFirstValidSprite(rocketFrames) != null;

        if (!hasRocketArt)
        {
            AddLog("火箭动画没有设置：请绑定 Rocket Sprite 或 Rocket Frames。");
            return;
        }

        Coroutine existing;
        if (regionRocketRoutines.TryGetValue(region, out existing) && existing != null)
        {
            return;
        }

        regionRocketRoutines[region] = StartCoroutine(PlayRegionRocketLoop(region, image));
    }
    private void StopRegionRocket(RegionID region)
    {
        Coroutine existing;
        if (regionRocketRoutines.TryGetValue(region, out existing) && existing != null)
        {
            StopCoroutine(existing);
        }

        regionRocketRoutines[region] = null;

        Image image;
        if (regionRocketImages.TryGetValue(region, out image) && image != null)
        {
            image.sprite = null;
            image.color = new Color(1f, 1f, 1f, 0f);
            image.gameObject.SetActive(false);
        }
    }
    private IEnumerator PlayRegionRocketLoop(RegionID region, Image image)
    {
        if (image == null)
        {
            yield break;
        }

        Sprite firstFrame = GetFirstValidSprite(rocketFrames);

        if (firstFrame == null)
        {
            firstFrame = rocketSprite;
        }

        if (firstFrame == null)
        {
            yield break;
        }

        RectTransform rocketRect = image.rectTransform;
        RectTransform clipRect = rocketRect.parent as RectTransform;

        if (clipRect == null)
        {
            yield break;
        }

        float safeMoveDuration = Mathf.Max(0.05f, rocketMoveDuration);
        float safeInterval = Mathf.Max(0.1f, rocketInterval);
        float safeFrameSeconds = Mathf.Max(0.03f, rocketFrameSeconds);

        while (regionTimes.ContainsKey(region) && regionTimes[region] == TimeState.War)
        {
            image.sprite = firstFrame;
            image.color = Color.white;
            image.raycastTarget = false;
            image.preserveAspect = true;
            image.gameObject.SetActive(true);
            rocketRect.SetAsLastSibling();

            rocketRect.sizeDelta = rocketSize;
            rocketRect.localRotation = Quaternion.Euler(0f, 0f, rocketRotationZ);

            Rect rect = clipRect.rect;

            Vector2 startPosition = new Vector2(
                rect.xMax + rocketSize.x,
                rect.yMax + rocketSize.y
            );

            Vector2 endPosition = new Vector2(
                rect.xMin - rocketSize.x,
                rect.yMin - rocketSize.y
            );

            float moveTimer = 0f;
            float frameTimer = 0f;
            int frameIndex = 0;

            while (moveTimer < safeMoveDuration &&
                   regionTimes.ContainsKey(region) &&
                   regionTimes[region] == TimeState.War)
            {
                moveTimer += Time.deltaTime;
                frameTimer += Time.deltaTime;

                float t = Mathf.Clamp01(moveTimer / safeMoveDuration);
                rocketRect.anchoredPosition = Vector2.Lerp(startPosition, endPosition, t);

                if (rocketFrames != null && rocketFrames.Count > 0 && frameTimer >= safeFrameSeconds)
                {
                    frameTimer = 0f;

                    Sprite frame = rocketFrames[frameIndex];
                    if (frame != null)
                    {
                        image.sprite = frame;
                    }

                    frameIndex++;
                    if (frameIndex >= rocketFrames.Count)
                    {
                        frameIndex = 0;
                    }
                }

                yield return null;
            }

            image.gameObject.SetActive(false);
            image.color = new Color(1f, 1f, 1f, 0f);

            yield return new WaitForSeconds(safeInterval);
        }

        image.sprite = null;
        image.color = new Color(1f, 1f, 1f, 0f);
        image.gameObject.SetActive(false);
        regionRocketRoutines[region] = null;
    }
   
    private void CreateRegionRainImages()
    {
        ClearRegionRainImages();

        foreach (KeyValuePair<RegionID, RegionView> pair in regionViews)
        {
            RegionID region = pair.Key;
            RegionView view = pair.Value;

            if (view == null || view.Root == null)
            {
                continue;
            }

            // 1. 先创建一个裁剪容器，大小等于当前格子
            GameObject clipObject = new GameObject("RainClip_" + region, typeof(RectTransform), typeof(RectMask2D));
            clipObject.transform.SetParent(view.Root, false);

            RectTransform clipRect = clipObject.GetComponent<RectTransform>();
            Stretch(clipRect);

            RectMask2D mask = clipObject.GetComponent<RectMask2D>();
            mask.padding = Vector4.zero;
            mask.softness = Vector2Int.zero;

            // 2. 再在裁剪容器里面创建真正播放雨帧的 Image
            GameObject rainObject = new GameObject("RainFX_" + region, typeof(RectTransform), typeof(Image));
            rainObject.transform.SetParent(clipRect, false);

            RectTransform rainRect = rainObject.GetComponent<RectTransform>();
            Stretch(rainRect);

            Image rainImage = rainObject.GetComponent<Image>();
            rainImage.raycastTarget = false;
            rainImage.preserveAspect = false;
            rainImage.sprite = null;
            rainImage.color = new Color(1f, 1f, 1f, 0f);

            rainObject.SetActive(false);

            // 3. 放到当前格子的最上层，但仍然只会被裁剪在这个格子内
            clipObject.transform.SetAsLastSibling();
            rainObject.transform.SetAsLastSibling();

            regionRainImages[region] = rainImage;
        }
    }

    private void ClearRegionRainImages()
    {
        foreach (KeyValuePair<RegionID, Coroutine> pair in regionRainRoutines)
        {
            if (pair.Value != null)
            {
                StopCoroutine(pair.Value);
            }
        }

        regionRainRoutines.Clear();

        foreach (KeyValuePair<RegionID, Image> pair in regionRainImages)
        {
            if (pair.Value != null)
            {
                Transform clipRoot = pair.Value.transform.parent;

                if (clipRoot != null && clipRoot.name.StartsWith("RainClip_"))
                {
                    Destroy(clipRoot.gameObject);
                }
                else
                {
                    Destroy(pair.Value.gameObject);
                }
            }
        }

        regionRainImages.Clear();
    }

    private void RefreshRegionRainFx()
    {
        foreach (KeyValuePair<RegionID, Image> pair in regionRainImages)
        {
            RegionID region = pair.Key;

            bool shouldRain =
                regionTimes.ContainsKey(region) &&
                regionTimes[region] == TimeState.Autumn;

            if (shouldRain)
            {
                StartRegionRain(region);
            }
            else
            {
                StopRegionRain(region);
            }
        }
    }

    private void StartRegionRain(RegionID region)
    {
        if (rainFrames == null || rainFrames.Count == 0)
        {
            AddLog("雨动画没有设置：请把雨帧拖进 Rain Frames。");
            return;
        }

        Image image;
        if (!regionRainImages.TryGetValue(region, out image) || image == null)
        {
            return;
        }

        Coroutine existing;
        if (regionRainRoutines.TryGetValue(region, out existing) && existing != null)
        {
            return;
        }

        regionRainRoutines[region] = StartCoroutine(PlayRegionRainLoop(region, image));
    }

    private void StopRegionRain(RegionID region)
    {
        Coroutine existing;
        if (regionRainRoutines.TryGetValue(region, out existing) && existing != null)
        {
            StopCoroutine(existing);
        }

        regionRainRoutines[region] = null;

        Image image;
        if (regionRainImages.TryGetValue(region, out image) && image != null)
        {
            image.sprite = null;
            image.color = new Color(1f, 1f, 1f, 0f);
            image.gameObject.SetActive(false);
        }
    }

    private IEnumerator PlayRegionRainLoop(RegionID region, Image image)
    {
        if (image == null || rainFrames == null || rainFrames.Count == 0)
        {
            yield break;
        }

        Sprite firstFrame = GetFirstValidSprite(rainFrames);
        if (firstFrame == null)
        {
            yield break;
        }

        image.sprite = firstFrame;
        image.color = Color.white;
        image.raycastTarget = false;
        image.gameObject.SetActive(true);
        image.rectTransform.SetAsLastSibling();

        float safeFrameSeconds = Mathf.Max(0.03f, rainFrameSeconds);
        int frameIndex = 0;

        while (regionTimes.ContainsKey(region) && regionTimes[region] == TimeState.Autumn)
        {
            Sprite frame = rainFrames[frameIndex];

            if (frame != null)
            {
                image.sprite = frame;
            }

            frameIndex++;
            if (frameIndex >= rainFrames.Count)
            {
                frameIndex = 0;
            }

            yield return new WaitForSeconds(safeFrameSeconds);
        }

        image.sprite = null;
        image.color = new Color(1f, 1f, 1f, 0f);
        image.gameObject.SetActive(false);
        regionRainRoutines[region] = null;
    }

    private Sprite GetFirstValidSprite(List<Sprite> sprites)
    {
        if (sprites == null)
        {
            return null;
        }

        for (int i = 0; i < sprites.Count; i++)
        {
            if (sprites[i] != null)
            {
                return sprites[i];
            }
        }

        return null;
    }
    private void PlayRainAnimator(RegionID region)
    {
        if (rainFxRoot == null || rainAnimator == null)
        {
            AddLog("雨动画没有绑定：请把 RainFX 和 Animator 拖到脚本上。");
            return;
        }

        MoveRainFxToRegion(region);

        if (rainAnimatorRoutine != null)
        {
            StopCoroutine(rainAnimatorRoutine);
        }

        rainAnimatorRoutine = StartCoroutine(PlayRainAnimatorRoutine());
    }

    private IEnumerator PlayRainAnimatorRoutine()
    {
        rainFxRoot.gameObject.SetActive(true);
        rainFxRoot.SetAsLastSibling();

        rainAnimator.ResetTrigger("Play");
        rainAnimator.Play("Idle", 0, 0f);
        rainAnimator.Update(0f);
        rainAnimator.SetTrigger("Play");

        yield return new WaitForSeconds(rainFxDuration);

        rainFxRoot.gameObject.SetActive(false);
        rainAnimatorRoutine = null;
    }
    private void MoveRainFxToRegion(RegionID region)
    {
        if (rainFxRoot == null)
        {
            return;
        }

        RectTransform parent = null;

        if (currentIndoorRoom != WhiteboxIndoorRoom.None)
        {
            RegionID indoorRegion = currentIndoorRoom == WhiteboxIndoorRoom.Kitchen ? RegionID.Kitchen : RegionID.Cellar;

            if (indoorRegion == region && indoorRoot != null)
            {
                parent = indoorRoot;
            }
        }

        if (parent == null)
        {
            RegionView view;
            if (regionViews.TryGetValue(region, out view) && view.Root != null)
            {
                parent = view.Root;
            }
        }

        if (parent == null)
        {
            parent = root;
        }

        rainFxRoot.SetParent(parent, false);
        Stretch(rainFxRoot);
        rainFxRoot.SetAsLastSibling();
    }
    private void PlaySeasonChangeFx(RegionID region, TimeState time)
    {
        if (time == TimeState.Autumn)
        {
            PlayRainFx(region);
        }
        else if (time == TimeState.War)
        {
            PlayRocketFx(region);
        }
    }

    private void PlayRainFx(RegionID region)
    {
        if (rainFxImage == null || rainFrames == null || rainFrames.Count == 0)
        {
            AddLog("雨动画没有设置：请绑定 Rain Fx Image 和 Rain Frames。");
            return;
        }

        MoveFxToCurrentView(rainFxImage.rectTransform, region);

        if (rainFxRoutine != null)
        {
            StopCoroutine(rainFxRoutine);
        }

        rainFxRoutine = StartCoroutine(PlayFrameFxRoutine(
            rainFxImage,
            rainFrames,
            rainFrameSeconds,
            rainDuration,
            delegate { rainFxRoutine = null; }
        ));
    }

    private void PlayRocketFx(RegionID region)
    {
        if (rocketFxImage == null || rocketFrames == null || rocketFrames.Count == 0)
        {
            AddLog("火箭动画没有设置：请绑定 Rocket Fx Image 和 Rocket Frames。");
            return;
        }

        MoveFxToCurrentView(rocketFxImage.rectTransform, region);

        if (rocketFxRoutine != null)
        {
            StopCoroutine(rocketFxRoutine);
        }

        rocketFxRoutine = StartCoroutine(PlayFrameFxRoutine(
            rocketFxImage,
            rocketFrames,
            rocketFrameSeconds,
            rocketDuration,
            delegate { rocketFxRoutine = null; }
        ));
    }

    private IEnumerator PlayFrameFxRoutine(
    Image image,
    List<Sprite> frames,
    float frameSeconds,
    float duration,
    System.Action onComplete)
    {
        if (image == null || frames == null || frames.Count == 0)
        {
            yield break;
        }

        image.gameObject.SetActive(true);
        image.raycastTarget = false;
        image.color = Color.white;
        image.rectTransform.SetAsLastSibling();

        float safeFrameSeconds = Mathf.Max(0.03f, frameSeconds);
        float timer = 0f;
        int frameIndex = 0;

        while (timer < duration)
        {
            Sprite frame = frames[frameIndex];

            if (frame != null)
            {
                image.sprite = frame;
                image.preserveAspect = true;
            }

            frameIndex++;
            if (frameIndex >= frames.Count)
            {
                frameIndex = 0;
            }

            timer += safeFrameSeconds;
            yield return new WaitForSeconds(safeFrameSeconds);
        }

        image.gameObject.SetActive(false);

        if (onComplete != null)
        {
            onComplete();
        }
    }

    private void MoveFxToCurrentView(RectTransform fxRect, RegionID region)
    {
        if (fxRect == null)
        {
            return;
        }

        RectTransform parent = null;

        if (currentIndoorRoom != WhiteboxIndoorRoom.None)
        {
            RegionID indoorRegion = currentIndoorRoom == WhiteboxIndoorRoom.Kitchen ? RegionID.Kitchen : RegionID.Cellar;

            if (indoorRegion == region && indoorRoot != null)
            {
                parent = indoorRoot;
            }
        }

        if (parent == null)
        {
            RegionView view;
            if (regionViews.TryGetValue(region, out view) && view.Root != null)
            {
                parent = view.Root;
            }
        }

        if (parent == null)
        {
            parent = root;
        }

        fxRect.SetParent(parent, false);
        Stretch(fxRect);
        fxRect.SetAsLastSibling();
    }
    private void ApplyKitchenTimeConsequence(RegionID region, TimeState time)
    {
        if (region != RegionID.Kitchen)
        {
            return;
        }

        if (time == TimeState.Autumn)
        {
            ExtinguishOvenByRain();
        }

        if (kitchenProcess != KitchenProcessState.DoughMixed &&
            kitchenProcess != KitchenProcessState.DoughFermented &&
            kitchenProcess != KitchenProcessState.DoughMolded)
        {
            return;
        }

        KitchenProcessState nextState = KitchenProcessState.DoughMixed;
        if (time == TimeState.War)
        {
            nextState = KitchenProcessState.DoughFermented;
        }
        else if (time == TimeState.Autumn)
        {
            nextState = KitchenProcessState.DoughMolded;
        }

        if (kitchenProcess == nextState)
        {
            return;
        }

        KitchenProcessState previousState = kitchenProcess;
        kitchenProcess = nextState;
        RemoveTokenOfType(WhiteboxTokenType.Dough);

        if (nextState == KitchenProcessState.DoughMixed)
        {
            AddLog(previousState == KitchenProcessState.DoughMolded ? "面团回到春天，恢复为揉好的面团。" : "面团回到春天，仍是揉好的面团。");
        }
        else if (nextState == KitchenProcessState.DoughFermented)
        {
            AddLog(previousState == KitchenProcessState.DoughMolded ? "切回战时，发霉面团恢复成发酵好的面团。" : "战时让面团发酵完成，可以放入烤炉。");
        }
        else
        {
            AddLog("面团在战后秋天发霉，不能放入烤炉。");
        }
    }

    private void ExtinguishOvenByRain()
    {
        if (!ovenLit)
        {
            return;
        }

        ovenLit = false;
        rocketTriggered = false;
        RemoveTokenOfType(WhiteboxTokenType.Arrow);

        if (kitchenProcess == KitchenProcessState.Baking)
        {
            if (bakeRoutine != null)
            {
                StopCoroutine(bakeRoutine);
                bakeRoutine = null;
            }

            kitchenProcess = KitchenProcessState.DoughInOven;
            AddLog("战后雨水把炉膛浇灭，烘烤中断；面团还在烤炉里，需要重新点火。");
            return;
        }

        AddLog("战后雨水把点燃的炉膛浇灭，需要重新用火箭点火。");
    }

    private void SetIndoorTime(TimeState time)
    {
        RegionID region = currentIndoorRoom == WhiteboxIndoorRoom.Kitchen ? RegionID.Kitchen : RegionID.Cellar;
        SetRegionTime(region, time);
    }

    private void TryMoveCartToNode(WhiteboxCartNodeId target)
    {
        if (!cartNodes.ContainsKey(target) || !cartNodes.ContainsKey(cartNodeId))
        {
            AddLog("这个小推车点没有启用。");
            RefreshAll();
            return;
        }

        if (girlFed)
        {
            AddLog("结局已完成，小推车停止操作。");
            return;
        }

        if (currentIndoorRoom != WhiteboxIndoorRoom.None)
        {
            AddLog("先退出室内视角，再移动小推车。");
            return;
        }

        if (cartMoving)
        {
            AddLog("小推车正在移动。");
            return;
        }

        if (target == cartNodeId)
        {
            AddLog("小推车已经在" + cartNodes[target].Name + "。");
            return;
        }

        List<WhiteboxCartNodeId> passablePath = FindPath(cartNodeId, target, true);
        if (passablePath == null)
        {
            List<WhiteboxCartNodeId> structuralPath = FindPath(cartNodeId, target, false);
            string reason = structuralPath != null ? GetFirstBlockedReason(structuralPath) : "没有道路连到该点。";

            AddLog(reason);
            return;
        }

        StartCoroutine(MoveCartAlongPath(passablePath));
    }

    private IEnumerator MoveCartAlongPath(List<WhiteboxCartNodeId> path)
    {
        cartMoving = true;

        for (int i = 1; i < path.Count; i++)
        {
            WhiteboxCartNodeId from = cartNodeId;
            WhiteboxCartNodeId to = path[i];
            UpdateCartDirection(from, to);
            movingFromNode = from;
            movingToNode = to;
            hasActiveMoveSegment = true;
            ApplyCartImage(true);

            Vector2 start = cartNodes[from].BoardPosition;
            Vector2 end = cartNodes[to].BoardPosition;
            float timer = 0f;
            float duration = 0.24f;

            while (timer < duration)
            {
                timer += Time.deltaTime;
                Vector2 position = Vector2.Lerp(start, end, Mathf.Clamp01(timer / duration));

                if (cartVisual != null)
                {
                    if (boardOverlay != null)
                    {
                        PlaceOnBoard(cartVisual, position, cartVisualSize);
                    }
                    else
                    {
                        Place(cartVisual, position, cartVisualSize);
                    }
                }

                yield return null;
            }

            cartNodeId = to;
            MaybeAutoMillWheat();
            hasActiveMoveSegment = false;
            RefreshCart();
        }

        cartMoving = false;
        hasActiveMoveSegment = false;
        AddLog("小推车到达" + cartNodes[cartNodeId].Name + "。");
        RefreshAll();
    }


    private void UpdateCartDirection(WhiteboxCartNodeId from, WhiteboxCartNodeId to)
    {
        Vector2 delta = cartNodes[to].BoardPosition - cartNodes[from].BoardPosition;
        float absX = Mathf.Abs(delta.x);
        float absY = Mathf.Abs(delta.y);
        float diagonalThreshold = Mathf.Min(absX, absY) / Mathf.Max(Mathf.Max(absX, absY), 0.0001f);

        if (diagonalThreshold >= 0.35f)
        {
            if (delta.y < 0f)
            {
                cartDirection = delta.x < 0f ? CartDirection.FrontLeft : CartDirection.FrontRight;
            }
            else
            {
                cartDirection = delta.x < 0f ? CartDirection.BackLeft : CartDirection.BackRight;
            }

            return;
        }

        if (absX >= absY)
        {
            cartDirection = delta.x >= 0f ? CartDirection.Right : CartDirection.Left;
        }
        else
        {
            cartDirection = delta.y >= 0f ? CartDirection.Up : CartDirection.Down;
        }
    }

    private List<WhiteboxCartNodeId> FindPath(WhiteboxCartNodeId start, WhiteboxCartNodeId target, bool requirePassable)
    {
        Queue<WhiteboxCartNodeId> queue = new Queue<WhiteboxCartNodeId>();
        Dictionary<WhiteboxCartNodeId, WhiteboxCartNodeId> previous = new Dictionary<WhiteboxCartNodeId, WhiteboxCartNodeId>();
        HashSet<WhiteboxCartNodeId> visited = new HashSet<WhiteboxCartNodeId>();

        queue.Enqueue(start);
        visited.Add(start);

        while (queue.Count > 0)
        {
            WhiteboxCartNodeId current = queue.Dequeue();
            if (current == target)
            {
                break;
            }

            for (int i = 0; i < roadEdges.Count; i++)
            {
                RoadEdge edge = roadEdges[i];
                if (!EdgeContains(edge, current))
                {
                    continue;
                }

                if (requirePassable && !IsEdgePassable(edge))
                {
                    continue;
                }

                WhiteboxCartNodeId next = GetOtherNode(edge, current);
                if (visited.Contains(next))
                {
                    continue;
                }

                visited.Add(next);
                previous[next] = current;
                queue.Enqueue(next);
            }
        }

        if (!visited.Contains(target))
        {
            return null;
        }

        List<WhiteboxCartNodeId> path = new List<WhiteboxCartNodeId>();
        WhiteboxCartNodeId step = target;
        path.Add(step);

        while (step != start)
        {
            step = previous[step];
            path.Add(step);
        }

        path.Reverse();
        return path;
    }

    private string GetFirstBlockedReason(List<WhiteboxCartNodeId> path)
    {
        for (int i = 1; i < path.Count; i++)
        {
            RoadEdge edge = FindEdge(path[i - 1], path[i]);
            if (edge != null && !IsEdgePassable(edge))
            {
                return GetBlockedReason(edge);
            }
        }

        return "道路暂时不通。";
    }

    private bool IsEdgePassable(RoadEdge edge)
    {
        if (IsSameRegionEdge(edge))
        {
            return true;
        }

        RegionID[] requiredRegions = GetRequiredSpringRegions(edge);
        for (int i = 0; i < requiredRegions.Length; i++)
        {
            if (regionTimes[requiredRegions[i]] != TimeState.Spring)
            {
                return false;
            }
        }

        return true;
    }

    private string GetBlockedReason(RoadEdge edge)
    {
        if (IsSameRegionEdge(edge))
        {
            return "同一格内部道路可通。";
        }

        List<string> blocked = new List<string>();
        RegionID[] requiredRegions = GetRequiredSpringRegions(edge);
        for (int i = 0; i < requiredRegions.Length; i++)
        {
            RegionID region = requiredRegions[i];
            if (regionTimes[region] != TimeState.Spring)
            {
                blocked.Add(GetRegionName(region) + "=" + GetTimeName(regionTimes[region]));
            }
        }

        return "路段 " + cartNodes[edge.A].Name + " → " + cartNodes[edge.B].Name + " 需要相关区域都是春天；当前 " + string.Join("、", blocked.ToArray()) + "。";
    }

    private bool IsSameRegionEdge(RoadEdge edge)
    {
        return cartNodes.ContainsKey(edge.A) &&
            cartNodes.ContainsKey(edge.B) &&
            cartNodes[edge.A].Region == cartNodes[edge.B].Region;
    }

    private RegionID[] GetRequiredSpringRegions(RoadEdge edge)
    {
        if (edge.RequiredSpringRegions != null && edge.RequiredSpringRegions.Length > 0)
        {
            return edge.RequiredSpringRegions;
        }

        List<RegionID> regions = new List<RegionID>();
        AddRequiredRegion(regions, cartNodes[edge.A].Region);
        AddRequiredRegion(regions, cartNodes[edge.B].Region);
        return regions.ToArray();
    }

    private void AddRequiredRegion(List<RegionID> regions, RegionID region)
    {
        if (!regions.Contains(region))
        {
            regions.Add(region);
        }
    }

    private RoadEdge FindEdge(WhiteboxCartNodeId a, WhiteboxCartNodeId b)
    {
        for (int i = 0; i < roadEdges.Count; i++)
        {
            RoadEdge edge = roadEdges[i];
            if ((edge.A == a && edge.B == b) || (edge.A == b && edge.B == a))
            {
                return edge;
            }
        }

        return null;
    }

    private bool EdgeContains(RoadEdge edge, WhiteboxCartNodeId node)
    {
        return edge.A == node || edge.B == node;
    }

    private WhiteboxCartNodeId GetOtherNode(RoadEdge edge, WhiteboxCartNodeId node)
    {
        return edge.A == node ? edge.B : edge.A;
    }

    private void DestroyBreadOnRoad(string reason)
    {
        cartCargo = CartCargo.Empty;
        breadDestroyed = true;
        kitchenProcess = KitchenProcessState.BreadAtDoor;
        SpawnBreadTokenIfMissing(RegionID.Kitchen, regionViews[RegionID.Kitchen].TokenLayer, new Vector2(0.52f, 0.36f), "面包\n拖到推车");
        AddLog(reason + " 厨房保留一份面包便于继续测试。");
        RefreshAll();
    }

    private void SpawnWheatToken()
    {
        if (wheatHarvested)
        {
            AddLog("麦田已经收割过，不能重复收麦。");
            return;
        }

        if (regionTimes[RegionID.Field] != TimeState.Autumn)
        {
            AddLog("麦田要切到秋天才有成熟麦子。");
            return;
        }

        if (cartNodeId != WhiteboxCartNodeId.FieldHarvest)
        {
            AddLog("先在春天把小推车推到“可收割麦田”点，再切秋天收割。");
            return;
        }

        if (cartCargo != CartCargo.Empty)
        {
            AddLog("小推车已有货物。");
            return;
        }

        cartCargo = CartCargo.Wheat;
        wheatHarvested = true;
        AddLog("麦子收割后直接装上小推车。");
        RefreshAll();
    }

    private void MaybeAutoMillWheat()
    {
        if (cartNodeId != WhiteboxCartNodeId.MillStone || cartCargo != CartCargo.Wheat)
        {
            return;
        }

        if (regionTimes[RegionID.Mill] != TimeState.Spring)
        {
            return;
        }

        cartCargo = CartCargo.Flour;
        AddLog("春天风车启动，麦子放进磨坊后自动变成面粉并回到车上。");
    }

    private void CollectWater()
    {
        if (waterCollected)
        {
            AddLog("地窖入口的清水已经取过，不能重复取水。");
            return;
        }

        if (!IsCartAtCellarDoor())
        {
            AddLog("先把小推车推到“地窖入口 / CellarDoor”点，再取水。");
            return;
        }

        if (regionTimes[RegionID.Cellar] != TimeState.Autumn)
        {
            AddLog("右下角需要切到秋天，地窖入口才有积水可以取。");
            return;
        }

        if (cartCargo != CartCargo.Empty)
        {
            AddLog("小推车已有货物。");
            return;
        }

        cartCargo = CartCargo.Water;
        waterCollected = true;
        AddLog("右下角秋天，在地窖入口取到清水。小推车切换为带水状态。");
        RefreshAll();
    }

    private bool IsCartAtCellarDoor()
    {
        if (cartNodeId == WhiteboxCartNodeId.CellarDoor)
        {
            return true;
        }

        CartNode node;
        if (!cartNodes.TryGetValue(cartNodeId, out node) || string.IsNullOrEmpty(node.Name))
        {
            return false;
        }

        string normalized = node.Name.Replace(" ", "").ToLowerInvariant();
        return normalized.Contains("cellardoor") || node.Name.Contains("地窖入口");
    }

    private bool CanCollectWaterNow()
    {
        return !girlFed &&
               currentIndoorRoom == WhiteboxIndoorRoom.None &&
               !waterCollected &&
               IsCartAtCellarDoor() &&
               regionTimes[RegionID.Cellar] == TimeState.Autumn &&
               cartCargo == CartCargo.Empty;
    }

    private void PlaceFlourOnBoard()
    {
        if (currentIndoorRoom != WhiteboxIndoorRoom.Kitchen)
        {
            AddLog("放面粉需要进入厨房内部。");
            return;
        }

        if (regionTimes[RegionID.Kitchen] != TimeState.Spring)
        {
            AddLog("厨房内部需要切到春天，案板才干净。");
            return;
        }

        if (!flourAtKitchenDoor)
        {
            AddLog("先把小推车上的面粉拖到厨房门口。");
            return;
        }

        if (kitchenProcess == KitchenProcessState.FlourPlaced || kitchenProcess == KitchenProcessState.WaterAdded)
        {
            AddLog("案板上已经有面粉。");
            return;
        }

        if (kitchenProcess != KitchenProcessState.Empty && kitchenProcess != KitchenProcessState.DoughMolded)
        {
            AddLog("当前厨房流程还没结束，暂时不能重新放面粉。");
            return;
        }

        kitchenProcess = KitchenProcessState.FlourPlaced;
        flourAtKitchenDoor = false;
        RemoveTokenOfType(WhiteboxTokenType.Flour);
        AddLog("面粉放到干净案板上。下一步加入清水。");
        RefreshAll();
    }

    private void AddWaterToFlour()
    {
        if (currentIndoorRoom != WhiteboxIndoorRoom.Kitchen)
        {
            AddLog("加水需要进入厨房内部。");
            return;
        }

        if (regionTimes[RegionID.Kitchen] != TimeState.Spring)
        {
            AddLog("厨房内部需要切到春天，案板才干净。");
            return;
        }

        if (kitchenProcess == KitchenProcessState.WaterAdded)
        {
            AddLog("面粉里已经加入清水。");
            return;
        }

        if (kitchenProcess != KitchenProcessState.FlourPlaced)
        {
            AddLog("顺序不对：先在案板上放入面粉。");
            return;
        }

        if (!waterAtKitchenDoor)
        {
            AddLog("先把小推车上的清水拖到厨房门口。");
            return;
        }

        kitchenProcess = KitchenProcessState.WaterAdded;
        waterAtKitchenDoor = false;
        RemoveTokenOfType(WhiteboxTokenType.Water);
        AddLog("清水加入面粉，案板图保持面粉状态。下一步点击揉面。");
        RefreshAll();
    }

    private void KneadDough()
    {
        if (currentIndoorRoom != WhiteboxIndoorRoom.Kitchen)
        {
            AddLog("揉面需要进入厨房内部。");
            return;
        }

        if (regionTimes[RegionID.Kitchen] != TimeState.Spring)
        {
            AddLog("厨房内部需要切到春天，案板才干净。");
            return;
        }

        if (kitchenProcess == KitchenProcessState.DoughMixed)
        {
            AddLog("面团已经揉好，切到战时会发酵完成；切到战后会发霉。");
            return;
        }

        if (kitchenProcess != KitchenProcessState.WaterAdded)
        {
            AddLog("顺序不对：需要先放面粉，再加入清水。");
            return;
        }

        kitchenProcess = KitchenProcessState.DoughMixed;
        RemoveTokenOfType(WhiteboxTokenType.Flour);
        RemoveTokenOfType(WhiteboxTokenType.Water);
        RemoveTokenOfType(WhiteboxTokenType.Dough);
        AddLog("面粉和水揉成面团。切到战时会变成发酵好的面团；战后秋天会发霉。");
        RefreshAll();
    }

    private void LoadDoughIntoOven()
    {
        if (currentIndoorRoom != WhiteboxIndoorRoom.Kitchen)
        {
            AddLog("放入烤箱需要进入厨房内部。");
            return;
        }

        if (kitchenProcess == KitchenProcessState.DoughMolded)
        {
            AddLog("面团现在发霉，切回春天恢复为揉好的面团，切回战时会变成发酵好的面团。");
            return;
        }

        if (kitchenProcess == KitchenProcessState.Empty || kitchenProcess == KitchenProcessState.FlourPlaced || kitchenProcess == KitchenProcessState.WaterAdded)
        {
            AddLog("还没有揉出面团。");
            return;
        }

        if (kitchenProcess == KitchenProcessState.DoughInOven || kitchenProcess == KitchenProcessState.Baking)
        {
            AddLog("面团已经在烤箱里。");
            return;
        }

        if (kitchenProcess == KitchenProcessState.DoughMixed)
        {
            AddLog("面团还没有发酵完成，先把厨房切到战时。");
            return;
        }

        if (kitchenProcess != KitchenProcessState.DoughFermented)
        {
            AddLog("当前状态不能放入烤箱。");
            return;
        }

        kitchenProcess = KitchenProcessState.DoughInOven;
        RemoveTokenOfType(WhiteboxTokenType.Dough);
        AddLog(ovenLit ? "发酵好的面团放入已点燃烤箱。" : "发酵好的面团放入烤箱。烤炉点燃后会变成面包。");
        if (ovenLit)
        {
            StartBaking();
        }

        RefreshAll();
    }

    private void OpenKitchen()
    {
        OpenIndoor(WhiteboxIndoorRoom.Kitchen);
    }

    private void OpenCellar()
    {
        if (regionTimes[RegionID.Cellar] != TimeState.Spring)
        {
            AddLog("从室外进入地窖需要右下区域保持春天可通行状态。");
            return;
        }

        OpenIndoor(WhiteboxIndoorRoom.Cellar);
    }

    private void OpenIndoor(WhiteboxIndoorRoom room)
    {
        currentIndoorRoom = room;
        RefreshAll();
        indoorRoot.gameObject.SetActive(true);
        indoorRoot.SetAsLastSibling();
        StartIndoorFadeIn(0f, 0.28f);
        CheckRocketEvent();
        RefreshAll();
    }

    private void CloseIndoor()
    {
        if (girlFed && currentIndoorRoom == WhiteboxIndoorRoom.Cellar)
        {
            AddLog("小女孩正在吃面包。点击小女孩，继续结局镜头。");
            RefreshAll();
            return;
        }

        CloseIndoorImmediately();
    }

    private void CloseIndoorImmediately()
    {
        if (indoorFadeRoutine != null)
        {
            StopCoroutine(indoorFadeRoutine);
            indoorFadeRoutine = null;
        }

        currentIndoorRoom = WhiteboxIndoorRoom.None;
        RefreshKitchenDoughAndBreadImages(false);
        DeactivateCellarBasketDragToken();
        RemoveTokenOfType(WhiteboxTokenType.Arrow);
        if (indoorRoot != null)
        {
            indoorRoot.gameObject.SetActive(false);
        }

        CanvasGroup group = GetIndoorCanvasGroup();
        if (group != null)
        {
            group.alpha = 1f;
        }

        RefreshAll();
    }

    private IEnumerator CloseIndoorRoutine()
    {
        CanvasGroup group = GetIndoorCanvasGroup();
        if (group != null)
        {
            yield return FadeCanvasGroup(group, group.alpha, 0f, 0.2f);
        }

        currentIndoorRoom = WhiteboxIndoorRoom.None;
        RefreshKitchenDoughAndBreadImages(false);
        DeactivateCellarBasketDragToken();

        if (indoorRoot != null)
        {
            indoorRoot.gameObject.SetActive(false);
        }

        if (group != null)
        {
            group.alpha = 1f;
        }

        indoorFadeRoutine = null;
        RefreshAll();
    }

    private void StartIndoorFadeIn(float fromAlpha, float duration)
    {
        if (indoorRoot == null)
        {
            return;
        }

        if (indoorFadeRoutine != null)
        {
            StopCoroutine(indoorFadeRoutine);
        }

        indoorCanvasGroup = GetIndoorCanvasGroup();

        indoorFadeRoutine = StartCoroutine(FadeCanvasGroup(indoorCanvasGroup, fromAlpha, 1f, duration));
    }

    private CanvasGroup GetIndoorCanvasGroup()
    {
        if (indoorRoot == null)
        {
            return null;
        }

        indoorCanvasGroup = indoorRoot.GetComponent<CanvasGroup>();
        if (indoorCanvasGroup == null)
        {
            indoorCanvasGroup = indoorRoot.gameObject.AddComponent<CanvasGroup>();
        }

        return indoorCanvasGroup;
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration)
    {
        if (group == null)
        {
            yield break;
        }

        group.alpha = from;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float amount = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
            group.alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, amount));
            yield return null;
        }

        group.alpha = to;
    }

    private void StartSceneTransitionFade(RegionID changedRegion)
    {
        if (currentIndoorRoom != WhiteboxIndoorRoom.None)
        {
            RegionID indoorRegion = currentIndoorRoom == WhiteboxIndoorRoom.Kitchen ? RegionID.Kitchen : RegionID.Cellar;
            if (changedRegion == indoorRegion && indoorRoot != null && indoorRoot.gameObject.activeInHierarchy)
            {
                StartIndoorFadeIn(0.35f, 0.22f);
            }

            return;
        }

        RegionView view;
        if (!regionViews.TryGetValue(changedRegion, out view) || view.Root == null)
        {
            return;
        }

        Coroutine existing;
        if (regionFadeRoutines.TryGetValue(changedRegion, out existing) && existing != null)
        {
            StopCoroutine(existing);
        }

        CanvasGroup group = view.Root.GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = view.Root.gameObject.AddComponent<CanvasGroup>();
        }

        regionFadeRoutines[changedRegion] = StartCoroutine(RegionFadeRoutine(changedRegion, group));
    }

    private IEnumerator RegionFadeRoutine(RegionID region, CanvasGroup group)
    {
        yield return FadeCanvasGroup(group, 0.45f, 1f, 0.24f);
        regionFadeRoutines.Remove(region);
    }

    private void CheckRocketEvent()
    {
        if (ovenLit)
        {
            return;
        }

        if (!CanTriggerRocketEvent())
        {
            RemoveTokenOfType(WhiteboxTokenType.Arrow);
            return;
        }

        if (!rocketTriggered)
        {
            rocketTriggered = true;
            AddLog("A-2 战时、A-1 春天、A-3 战时同时成立：燃烧火箭钉在厨房窗框上。");
        }

        if (currentIndoorRoom == WhiteboxIndoorRoom.Kitchen)
        {
            SpawnArrowInKitchenIfNeeded();
        }
    }

    private bool CanTriggerRocketEvent()
    {
        return regionTimes[RegionID.Field] == TimeState.War &&
            regionTimes[RegionID.Mill] == TimeState.Spring &&
            regionTimes[RegionID.Kitchen] == TimeState.War;
    }

    private bool HasActiveKitchenRocket()
    {
        return rocketTriggered && !ovenLit && CanTriggerRocketEvent();
    }

    private void SpawnArrowInKitchenIfNeeded()
    {
        if (!rocketTriggered || ovenLit || !CanTriggerRocketEvent() || FindToken(WhiteboxTokenType.Arrow) != null)
        {
            return;
        }

        RectTransform parent = indoorTokenLayer != null ? indoorTokenLayer : indoorRoot;
        if (parent != null)
        {
            SpawnToken(WhiteboxTokenType.Arrow, RegionID.Kitchen, parent, "战时火箭\n拖到炉膛", new Color(1f, 0.3f, 0.12f, 0.96f), new Vector2(0.34f, 0.64f));
        }
    }


    private void RefreshCellarBasketVisual()
    {
        SyncCellarHierarchyReferences();

        if (currentIndoorRoom != WhiteboxIndoorRoom.Cellar)
        {
            SetCellarBasketObjectsActive(false, false);
            DeactivateCellarBasketDragToken();
            return;
        }

        EnsureCellarBasketVisual();
        bool hasBread = breadAtCellarEntrance && !girlFed;
        bool canDrag = hasBread && regionTimes[RegionID.Cellar] == TimeState.War;

        SetCellarBasketObjectsActive(!hasBread, hasBread);
        if (cellarEmptyBasketImage != null)
        {
            cellarEmptyBasketImage.raycastTarget = false;
        }

        if (cellarBreadBasketImage != null)
        {
            cellarBreadBasketImage.raycastTarget = canDrag;
        }

        if (canDrag)
        {
            ActivateCellarBasketDragToken();
        }
        else
        {
            DeactivateCellarBasketDragToken();
        }
    }

    private void EnsureCellarBasketVisual()
    {
        SyncCellarHierarchyReferences();
    }

    private void SetCellarBasketObjectsActive(bool emptyActive, bool breadActive)
    {
        if (cellarEmptyBasketVisual != null)
        {
            cellarEmptyBasketVisual.gameObject.SetActive(emptyActive);
        }

        if (cellarBreadBasketVisual != null)
        {
            cellarBreadBasketVisual.gameObject.SetActive(breadActive);
        }
    }

    private void ActivateCellarBasketDragToken()
    {
        RectTransform basketVisual = GetDraggableCellarBasketVisual();
        if (basketVisual == null)
        {
            return;
        }

        CanvasGroup group = basketVisual.GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = basketVisual.gameObject.AddComponent<CanvasGroup>();
        }

        group.blocksRaycasts = true;
        group.interactable = true;

        WhiteboxDraggableToken token = basketVisual.GetComponent<WhiteboxDraggableToken>();
        if (token == null)
        {
            token = basketVisual.gameObject.AddComponent<WhiteboxDraggableToken>();
        }

        token.Initialize(this, WhiteboxTokenType.Bread, RegionID.Cellar, false);
        token.enabled = true;
        if (!activeTokens.Contains(token))
        {
            activeTokens.Add(token);
        }
    }

    private void DeactivateCellarBasketDragToken()
    {
        RectTransform basketVisual = GetDraggableCellarBasketVisual();
        if (basketVisual == null)
        {
            return;
        }

        WhiteboxDraggableToken token = basketVisual.GetComponent<WhiteboxDraggableToken>();
        if (token != null)
        {
            activeTokens.Remove(token);
            token.DisablePersistentToken();
        }

        CanvasGroup group = basketVisual.GetComponent<CanvasGroup>();
        if (group != null)
        {
            group.blocksRaycasts = false;
        }
    }

    private RectTransform GetDraggableCellarBasketVisual()
    {
        return cellarBreadBasketVisual;
    }

    private void RefreshKitchenIndoorTokens()
    {
        RemoveIndoorRuntimeTokenOfType(WhiteboxTokenType.Flour);
        RemoveIndoorRuntimeTokenOfType(WhiteboxTokenType.Water);
        RemoveIndoorRuntimeTokenOfType(WhiteboxTokenType.Dough);

        if (currentIndoorRoom == WhiteboxIndoorRoom.Kitchen)
        {
            RefreshKitchenIngredientImages(true);
        }
    }


    private void StartBaking()
    {
        if (kitchenProcess == KitchenProcessState.Baking)
        {
            return;
        }

        bool wasAlreadyLit = ovenLit;
        ovenLit = true;
        if (kitchenProcess != KitchenProcessState.DoughInOven)
        {
            AddLog(wasAlreadyLit ? "炉膛保持点燃。" : "火箭点燃炉膛，厨房从冷色变暖。");
            RefreshAll();
            return;
        }

        kitchenProcess = KitchenProcessState.BreadReady;
        AddLog(wasAlreadyLit ? "发酵好的面团放进已点燃烤炉，变成面包。" : "火箭点燃炉膛，炉里的发酵面团变成面包。");
        bakeRoutine = null;
        RefreshAll();
    }

    private IEnumerator BakeRoutine()
    {
        yield return new WaitForSeconds(1.2f);
        if (kitchenProcess == KitchenProcessState.Baking)
        {
            kitchenProcess = KitchenProcessState.BreadReady;
            AddLog("面包在厨房内部出炉。先把面包移到门口，再由小推车装走。");
            RefreshAll();
        }

        bakeRoutine = null;
    }

    private bool MoveBreadToKitchenDoor(WhiteboxDraggableToken token, bool showLog = true)
    {
        if (currentIndoorRoom != WhiteboxIndoorRoom.Kitchen)
        {
            if (showLog)
            {
                AddLog("需要在厨房内部把面包拖到门口。");
            }

            return false;
        }

        if (kitchenProcess == KitchenProcessState.BreadAtDoor)
        {
            if (showLog)
            {
                AddLog("面包已经在厨房门口。");
            }

            return false;
        }

        if (kitchenProcess != KitchenProcessState.BreadReady)
        {
            if (showLog)
            {
                AddLog("面包还没有出炉。");
            }

            return false;
        }

        if (token == null || token.TokenType != WhiteboxTokenType.Bread || token.SourceRegion != RegionID.Kitchen)
        {
            if (showLog)
            {
                AddLog("把出炉面包图片拖到厨房门口投放区，才能送出厨房。");
            }

            return false;
        }

        kitchenProcess = KitchenProcessState.BreadAtDoor;
        ConsumeToken(token);
        SpawnBreadTokenIfMissing(RegionID.Kitchen, GetOutdoorTokenParent(RegionID.Kitchen), new Vector2(0.52f, 0.36f), "门口面包\n拖到推车");
        AddLog("面包移到厨房门口。把小推车推到门口后拖上车。");
        RefreshAll();
        return true;
    }

    public bool TryDropToken(WhiteboxDraggableToken token, PointerEventData eventData)
    {
        if (token == null)
        {
            return false;
        }

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        bool foundZone = false;
        for (int i = 0; i < results.Count; i++)
        {
            WhiteboxDropZone zone = results[i].gameObject.GetComponentInParent<WhiteboxDropZone>();
            if (zone != null && zone.enabled)
            {
                foundZone = true;
                if (HandleTokenDrop(token, zone, false))
                {
                    return true;
                }
            }
        }

        if (TryDropBreadToCartByPointer(token, eventData))
        {
            return true;
        }

        if (CanUseCellarBreadFallback(token))
        {
            return DropTokenToCellar(token, true);
        }

        if (foundZone)
        {
            HandleTokenDrop(token, GetBestDropZoneForLog(token), true);
        }
        else
        {
            AddLog("没有放到有效投放区。");
        }

        return false;
    }

    private WhiteboxDropZone GetBestDropZoneForLog(WhiteboxDraggableToken token)
    {
        if (currentIndoorRoom == WhiteboxIndoorRoom.Kitchen)
        {
            if (token.TokenType == WhiteboxTokenType.Arrow)
            {
                return indoorFireboxDropZone != null ? indoorFireboxDropZone : indoorOvenDropZone;
            }

            if (token.TokenType == WhiteboxTokenType.Dough)
            {
                return indoorOvenDropZone != null ? indoorOvenDropZone : indoorFireboxDropZone;
            }

            if (token.TokenType == WhiteboxTokenType.Bread)
            {
                return indoorKitchenDoorDropZone;
            }
        }

        if (currentIndoorRoom == WhiteboxIndoorRoom.Cellar &&
            token.TokenType == WhiteboxTokenType.Bread &&
            breadAtCellarEntrance)
        {
            return GetCellarGirlDropZone();
        }

        return null;
    }

    private WhiteboxDropZone GetCellarGirlDropZone()
    {
        WhiteboxDropZone zone = cellarGirlVisual != null ? cellarGirlVisual.GetComponent<WhiteboxDropZone>() : null;
        return zone != null && zone.enabled ? zone : null;
    }

    private bool CanUseCellarBreadFallback(WhiteboxDraggableToken token)
    {
        return token != null &&
            token.TokenType == WhiteboxTokenType.Bread &&
            breadAtCellarEntrance &&
            currentIndoorRoom == WhiteboxIndoorRoom.Cellar &&
            regionTimes[RegionID.Cellar] == TimeState.War;
    }

    private bool TryDropBreadToCartByPointer(WhiteboxDraggableToken token, PointerEventData eventData)
    {
        if (!CanLoadKitchenBreadOntoCart(token))
        {
            return false;
        }

        if (IsPointerInsideRect(cartVisual, eventData) || IsPointerInsideRect(kitchenDoorDropZoneRect, eventData))
        {
            return LoadKitchenBreadOntoCart(token);
        }

        return false;
    }

    private bool CanLoadKitchenBreadOntoCart(WhiteboxDraggableToken token)
    {
        return token != null &&
               token.TokenType == WhiteboxTokenType.Bread &&
               token.SourceRegion == RegionID.Kitchen &&
               currentIndoorRoom == WhiteboxIndoorRoom.None &&
               cartCargo == CartCargo.Empty &&
               IsCartAtKitchenDoor() &&
               kitchenProcess == KitchenProcessState.BreadAtDoor;
    }

    private bool LoadKitchenBreadOntoCart(WhiteboxDraggableToken token)
    {
        cartCargo = CartCargo.Bread;
        kitchenProcess = KitchenProcessState.Empty;
        ConsumeToken(token);
        AddLog("面包装车。小推车切换为托盘/面包状态。");
        RefreshAll();
        return true;
    }

    private bool IsCartAtKitchenDoor()
    {
        if (cartNodeId == WhiteboxCartNodeId.KitchenDoor)
        {
            return true;
        }

        CartNode node;
        if (!cartNodes.TryGetValue(cartNodeId, out node) || string.IsNullOrEmpty(node.Name))
        {
            return false;
        }

        string normalized = node.Name.Replace(" ", "").ToLowerInvariant();
        return normalized.Contains("kitchendoor") || node.Name.Contains("厨房门");
    }

    private bool IsPointerInsideRect(RectTransform rect, PointerEventData eventData)
    {
        if (rect == null || eventData == null)
        {
            return false;
        }

        return RectTransformUtility.RectangleContainsScreenPoint(rect, eventData.position, eventData.pressEventCamera);
    }

    private bool HandleTokenDrop(WhiteboxDraggableToken token, WhiteboxDropZone zone, bool showLog)
    {
        if (zone == null)
        {
            if (showLog)
            {
                AddLog("这个位置不能接收当前物品。");
            }

            return false;
        }

        if (zone.ZoneType == WhiteboxDropZoneType.Cart)
        {
            return DropTokenToCart(token, showLog);
        }

        if (zone.ZoneType == WhiteboxDropZoneType.KitchenDoor)
        {
            return DropTokenToKitchenDoor(token, showLog);
        }

        if (zone.ZoneType == WhiteboxDropZoneType.CellarEntrance)
        {
            return DropTokenToCellarEntrance(token, showLog);
        }

        if (zone.ZoneType == WhiteboxDropZoneType.PrepBoard)
        {
            return DropTokenToPrepBoard(token, showLog);
        }

        if (zone.ZoneType == WhiteboxDropZoneType.Firebox)
        {
            return DropTokenToFirebox(token, showLog);
        }

        if (zone.ZoneType == WhiteboxDropZoneType.Oven)
        {
            return DropTokenToOven(token, showLog);
        }

        if (zone.ZoneType == WhiteboxDropZoneType.CellarDelivery)
        {
            return DropTokenToCellar(token, showLog);
        }

        return false;
    }

    private bool DropTokenToCart(WhiteboxDraggableToken token, bool showLog = true)
    {
        if (currentIndoorRoom != WhiteboxIndoorRoom.None)
        {
            AddLog("先退出室内视角，再把物品放到小推车。");
            return false;
        }

        if (cartCargo != CartCargo.Empty)
        {
            AddLog("小推车已有货物。");
            return false;
        }

        if (token.TokenType == WhiteboxTokenType.Wheat)
        {
            if (cartNodeId != WhiteboxCartNodeId.FieldHarvest)
            {
                AddLog("小推车要停在可收割麦田点。");
                return false;
            }

            cartCargo = CartCargo.Wheat;
            ConsumeToken(token);
            AddLog("麦子装车。小推车切换为装麦子状态。");
            RefreshAll();
            return true;
        }

        if (token.TokenType == WhiteboxTokenType.Bread)
        {
            if (!CanLoadKitchenBreadOntoCart(token))
            {
                AddLog("面包需要先从厨房内部移到门口，再在厨房门口装车。");
                return false;
            }

            return LoadKitchenBreadOntoCart(token);
        }

        AddLog("这个物品不能装车。");
        return false;
    }

    private bool DropTokenToKitchenDoor(WhiteboxDraggableToken token, bool showLog = true)
    {
        if (currentIndoorRoom == WhiteboxIndoorRoom.Kitchen)
        {
            if (token.TokenType == WhiteboxTokenType.Bread)
            {
                return MoveBreadToKitchenDoor(token, showLog);
            }

            if (showLog)
            {
                AddLog("厨房内部的门口只接收出炉面包。");
            }

            return false;
        }

        if (currentIndoorRoom != WhiteboxIndoorRoom.None)
        {
            AddLog("先退出室内视角，再把货物拖到厨房门口。");
            return false;
        }

        if (cartNodeId != WhiteboxCartNodeId.KitchenDoor)
        {
            AddLog("小推车需要停在厨房门口，才能把货物拖进门口。");
            return false;
        }

        if (regionTimes[RegionID.Kitchen] != TimeState.Spring)
        {
            AddLog("厨房门口需要春天状态，才能安全放入面粉或清水。");
            return false;
        }

        if (token.TokenType == WhiteboxTokenType.Flour && cartCargo == CartCargo.Flour)
        {
            flourAtKitchenDoor = true;
            cartCargo = CartCargo.Empty;
            ConsumeToken(token);
            AddLog("面粉放进厨房门口。进入厨房内部后可卸下面粉。");
            RefreshAll();
            return true;
        }

        if (token.TokenType == WhiteboxTokenType.Water && cartCargo == CartCargo.Water)
        {
            waterAtKitchenDoor = true;
            cartCargo = CartCargo.Empty;
            ConsumeToken(token);
            AddLog("清水放进厨房门口。进入厨房内部后可卸下清水。");
            RefreshAll();
            return true;
        }

        if (token.TokenType == WhiteboxTokenType.Bread &&
            CanLoadKitchenBreadOntoCart(token))
        {
            return LoadKitchenBreadOntoCart(token);
        }

        AddLog("厨房门口只接收面粉和清水。");
        return false;
    }

    private bool DropTokenToCellarEntrance(WhiteboxDraggableToken token, bool showLog = true)
    {
        if (currentIndoorRoom != WhiteboxIndoorRoom.None)
        {
            AddLog("先退出室内视角，再把面包拖到地窖入口。");
            return false;
        }

        if (cartNodeId != WhiteboxCartNodeId.CellarDoor)
        {
            AddLog("小推车需要停在地窖入口，才能投放面包。");
            return false;
        }

        if (regionTimes[RegionID.Cellar] != TimeState.Spring)
        {
            AddLog("地窖入口只有春天打开，面包需要在春天投放到入口。");
            return false;
        }

        if (token.TokenType == WhiteboxTokenType.Bread && cartCargo == CartCargo.Bread)
        {
            breadAtCellarEntrance = true;
            cartCargo = CartCargo.Empty;
            ConsumeToken(token);
            AddLog("面包放到地窖入口。春天进入地窖，内部切到战时后交给小女孩。");
            RefreshAll();
            return true;
        }

        AddLog("地窖入口只接收面包。");
        return false;
    }

    private bool DropTokenToPrepBoard(WhiteboxDraggableToken token, bool showLog = true)
    {
        if (currentIndoorRoom != WhiteboxIndoorRoom.Kitchen)
        {
            AddLog("需要进入厨房内部，把原料拖到案板。");
            return false;
        }

        if (regionTimes[RegionID.Kitchen] != TimeState.Spring)
        {
            AddLog("案板只在春天干净可用。");
            return false;
        }

        if (token.TokenType == WhiteboxTokenType.Flour)
        {
            if (!flourAtKitchenDoor || token.SourceRegion != RegionID.Kitchen)
            {
                AddLog("先把小推车上的面粉拖进厨房门口，再拖到案板。");
                return false;
            }

            if (kitchenProcess != KitchenProcessState.Empty && kitchenProcess != KitchenProcessState.DoughMolded)
            {
                AddLog("案板已有正在处理的材料。");
                return false;
            }

            kitchenProcess = KitchenProcessState.FlourPlaced;
            flourAtKitchenDoor = false;
            ConsumeToken(token);
            AddLog("面粉拖到案板上。下一步把清水拖到案板。");
            RefreshAll();
            return true;
        }

        if (token.TokenType == WhiteboxTokenType.Water)
        {
            if (!waterAtKitchenDoor || token.SourceRegion != RegionID.Kitchen)
            {
                AddLog("先把小推车上的清水拖进厨房门口，再拖到案板。");
                return false;
            }

            if (kitchenProcess != KitchenProcessState.FlourPlaced)
            {
                AddLog("顺序不对：先把面粉拖到案板。");
                return false;
            }

            kitchenProcess = KitchenProcessState.WaterAdded;
            waterAtKitchenDoor = false;
            ConsumeToken(token);
            AddLog("清水加入面粉。案板图保持面粉状态，点击案板开始和面。");
            RefreshAll();
            return true;
        }

        AddLog("案板只接收面粉和清水。");
        return false;
    }

    private bool DropTokenToFirebox(WhiteboxDraggableToken token, bool showLog = true)
    {
        if (currentIndoorRoom != WhiteboxIndoorRoom.Kitchen)
        {
            AddLog("需要进入厨房内部，把火箭拖到炉膛。");
            return false;
        }

        if (token.TokenType == WhiteboxTokenType.Dough)
        {
            return DropTokenToOven(token, showLog);
        }

        if (token.TokenType != WhiteboxTokenType.Arrow)
        {
            AddLog("炉膛需要燃烧火箭。");
            return false;
        }

        if (regionTimes[RegionID.Kitchen] != TimeState.War)
        {
            AddLog("炉膛必须在厨房战时用火箭点燃。");
            return false;
        }

        ConsumeToken(token);
        StartBaking();
        return true;
    }

    private bool DropTokenToOven(WhiteboxDraggableToken token, bool showLog = true)
    {
        if (currentIndoorRoom != WhiteboxIndoorRoom.Kitchen)
        {
            AddLog("需要进入厨房内部，把发酵好的面团拖到烤炉。");
            return false;
        }

        if (token.TokenType == WhiteboxTokenType.Arrow)
        {
            return DropTokenToFirebox(token, showLog);
        }

        if (token.TokenType != WhiteboxTokenType.Dough || token.SourceRegion != RegionID.Kitchen)
        {
            AddLog("烤炉只接收发酵好的面团。");
            return false;
        }

        if (kitchenProcess == KitchenProcessState.DoughMolded)
        {
            AddLog("面团现在发霉，切回战时变成发酵好的面团后再放入烤炉。");
            return false;
        }

        if (kitchenProcess == KitchenProcessState.DoughMixed)
        {
            AddLog("面团还没有发酵完成，先把厨房切到战时。");
            return false;
        }

        if (kitchenProcess != KitchenProcessState.DoughFermented)
        {
            AddLog("需要先在案板上揉好面团，再切到战时发酵。");
            return false;
        }

        kitchenProcess = KitchenProcessState.DoughInOven;
        ConsumeToken(token);
        AddLog(ovenLit ? "发酵好的面团拖进已点燃的烤炉。" : "发酵好的面团拖进烤炉。炉膛点燃后会变成面包。");
        if (ovenLit)
        {
            StartBaking();
        }

        RefreshAll();
        return true;
    }

    private bool DropTokenToCellar(WhiteboxDraggableToken token, bool showLog = true)
    {
        if (currentIndoorRoom != WhiteboxIndoorRoom.Cellar)
        {
            AddLog("需要进入地窖内部交付面包。");
            return false;
        }

        if (regionTimes[RegionID.Cellar] != TimeState.War)
        {
            AddLog("小女孩只会在战时地窖中出现。");
            return false;
        }

        if (token.TokenType != WhiteboxTokenType.Bread || !breadAtCellarEntrance)
        {
            AddLog("需要把装面包的篮子拖给小女孩。");
            return false;
        }

        ConsumeToken(token);
        breadAtCellarEntrance = false;
        FeedGirlAndWaitForClick();
        return true;
    }

    private void FeedGirlAndWaitForClick()
    {
        if (girlFed)
        {
            return;
        }

        girlFed = true;
        timeLocked = true;
        AddLog("小女孩吃到面包。点击小女孩，继续结局镜头。");
        RefreshAll();
    }

    public void OnCellarGirlClicked()
    {
        if (!girlFed || endingRoutine != null)
        {
            return;
        }

        TriggerEnding();
    }

    private void TriggerEnding()
    {
        if (endingRoutine != null)
        {
            return;
        }

        endingRoutine = StartCoroutine(EndingSequence());
    }

    private IEnumerator EndingSequence()
    {
        timeLocked = true;
        AddLog("镜头从小女孩身上慢慢拉远。");
        RefreshAll();

        yield return new WaitForSeconds(0.9f);

        SetAllRegionTimes(TimeState.War);
        AddLog("镜头从地窖拉远，回到战时的室外。");
        RefreshAll();
        yield return PullOutFromIndoor();

        currentIndoorRoom = WhiteboxIndoorRoom.None;
        if (indoorRoot != null)
        {
            indoorRoot.gameObject.SetActive(false);
        }

        RefreshAll();
        yield return new WaitForSeconds(1.2f);

        SetAllRegionTimes(TimeState.Autumn);
        AddLog("时间流逝，室外变成战后。");
        RefreshAll();

        yield return new WaitForSeconds(1.2f);
        AddLog("时间继续流逝到战后多年。");

        EnsureFinalOverlayControls();
        if (finalOverlay == null)
        {
            endingRoutine = null;
            yield break;
        }

        finalOverlay.gameObject.SetActive(true);
        finalOverlay.SetAsLastSibling();
        SetFinalButtonsVisible(false);

        if (finalText != null)
        {
            finalText.text = "战后多年\n\n感谢游玩";
        }

        if (finalCanvasGroup != null)
        {
            finalCanvasGroup.alpha = 0f;
            const float fadeDuration = 1.8f;
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float amount = Mathf.Clamp01(elapsed / fadeDuration);
                finalCanvasGroup.alpha = Mathf.SmoothStep(0f, 1f, amount);
                yield return null;
            }

            finalCanvasGroup.alpha = 1f;
        }

        SetFinalButtonsVisible(true);
        endingRoutine = null;
    }

    private IEnumerator PullOutFromIndoor()
    {
        if (indoorRoot == null)
        {
            yield break;
        }

        CanvasGroup indoorGroup = indoorRoot.GetComponent<CanvasGroup>();
        if (indoorGroup == null)
        {
            indoorGroup = indoorRoot.gameObject.AddComponent<CanvasGroup>();
        }

        Vector3 originalScale = indoorRoot.localScale;
        const float duration = 1.25f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float amount = Mathf.Clamp01(elapsed / duration);
            float smooth = Mathf.SmoothStep(0f, 1f, amount);
            indoorRoot.localScale = Vector3.Lerp(originalScale, originalScale * 0.72f, smooth);
            indoorGroup.alpha = Mathf.Lerp(1f, 0.18f, smooth);
            yield return null;
        }

        indoorRoot.localScale = originalScale;
        indoorGroup.alpha = 1f;
    }

    private void SetAllRegionTimes(TimeState time)
    {
        regionTimes[RegionID.Mill] = time;
        regionTimes[RegionID.Field] = time;
        regionTimes[RegionID.Kitchen] = time;
        regionTimes[RegionID.Cellar] = time;

        if (TimeManager.Instance != null)
        {
            foreach (KeyValuePair<RegionID, TimeState> pair in regionTimes)
            {
                TimeManager.Instance.SetRegionTime(pair.Key, pair.Value);
            }
        }
    }

    private void EnsureFinalOverlayControls()
    {
        if (finalOverlay == null)
        {
            return;
        }

        finalCanvasGroup = finalOverlay.GetComponent<CanvasGroup>();
        if (finalCanvasGroup == null)
        {
            finalCanvasGroup = finalOverlay.gameObject.AddComponent<CanvasGroup>();
        }

        if (finalRestartButton != null)
        {
            BindButton(finalRestartButton, ResetPrototype);
        }

        if (finalQuitButton != null)
        {
            BindButton(finalQuitButton, QuitGame);
        }
    }

    private void SetFinalButtonsVisible(bool visible)
    {
        if (finalRestartButton != null)
        {
            finalRestartButton.gameObject.SetActive(visible);
        }

        if (finalQuitButton != null)
        {
            finalQuitButton.gameObject.SetActive(visible);
        }
    }

    private void QuitGame()
    {
        AddLog("退出游戏。");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }


    private void ResetPrototype()
    {
        StopAllCoroutines();
        endingRoutine = null;

        for (int i = activeTokens.Count - 1; i >= 0; i--)
        {
            if (activeTokens[i] != null)
            {
                DisposeToken(activeTokens[i]);
            }
        }

        activeTokens.Clear();
        cartCargoToken = null;
        logLines.Clear();
        InitializeState();

        if (finalOverlay != null)
        {
            finalOverlay.gameObject.SetActive(false);
        }

        if (finalCanvasGroup != null)
        {
            finalCanvasGroup.alpha = 0f;
        }

        if (indoorRoot != null)
        {
            indoorRoot.gameObject.SetActive(false);
        }

        AddLog("白盒已重置。");
        RefreshAll();
    }

    private WhiteboxDraggableToken SpawnToken(WhiteboxTokenType tokenType, RegionID sourceRegion, RectTransform parent, string label, Color color, Vector2 normalizedPosition)
    {
        if (parent == null)
        {
            Debug.LogWarning("BorderMillWhiteboxController: Cannot spawn token because parent TokenLayer is missing.", this);
            return null;
        }

        RectTransform tokenRect = CreatePanel(tokenType.ToString(), parent, color);
        Place(tokenRect, normalizedPosition, new Vector2(114f, 64f));
        ApplyTokenSprite(tokenRect.GetComponent<Image>(), tokenType, color);
        HideTextChildren(tokenRect);

        CanvasGroup group = tokenRect.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = true;
        group.interactable = true;

        WhiteboxDraggableToken token = tokenRect.gameObject.AddComponent<WhiteboxDraggableToken>();
        token.Initialize(this, tokenType, sourceRegion);

        activeTokens.Add(token);
        return token;
    }

    private void ApplyTokenSprite(Image image, WhiteboxTokenType tokenType, Color fallbackColor)
    {
        if (image == null)
        {
            return;
        }

        Sprite sprite = GetTokenSprite(tokenType);
        if (sprite != null)
        {
            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = true;
        }
        else
        {
            image.sprite = null;
            image.color = fallbackColor;
        }
    }

    private Sprite GetTokenSprite(WhiteboxTokenType tokenType)
    {
        for (int i = 0; i < tokenSprites.Count; i++)
        {
            TokenSpriteBinding binding = tokenSprites[i];
            if (binding != null && binding.TokenType == tokenType)
            {
                return binding.Sprite;
            }
        }

        return null;
    }

    public Sprite GetDragTokenSprite(WhiteboxTokenType tokenType)
    {
        return GetTokenSprite(tokenType);
    }

    public Color GetDragTokenFallbackColor(WhiteboxTokenType tokenType)
    {
        if (tokenType == WhiteboxTokenType.Wheat) return new Color(0.95f, 0.74f, 0.25f, 0.96f);
        if (tokenType == WhiteboxTokenType.Flour) return new Color(0.98f, 0.93f, 0.78f, 0.96f);
        if (tokenType == WhiteboxTokenType.Water) return new Color(0.52f, 0.75f, 0.95f, 0.96f);
        if (tokenType == WhiteboxTokenType.Arrow) return new Color(1f, 0.3f, 0.12f, 0.96f);
        if (tokenType == WhiteboxTokenType.Dough) return new Color(0.92f, 0.82f, 0.58f, 0.96f);
        if (tokenType == WhiteboxTokenType.Bread) return new Color(0.92f, 0.52f, 0.18f, 0.96f);
        return new Color(1f, 1f, 1f, 0.86f);
    }

    public string GetDragTokenLabel(WhiteboxTokenType tokenType)
    {
        if (tokenType == WhiteboxTokenType.Wheat) return "麦子";
        if (tokenType == WhiteboxTokenType.Flour) return "面粉";
        if (tokenType == WhiteboxTokenType.Water) return "清水";
        if (tokenType == WhiteboxTokenType.Arrow) return "火箭";
        if (tokenType == WhiteboxTokenType.Dough) return "面团";
        if (tokenType == WhiteboxTokenType.Bread) return "面包";
        return tokenType.ToString();
    }

    public bool IsCartCargoVisual(Transform target)
    {
        return cartVisual != null && target == cartVisual;
    }


    private void SpawnBreadTokenIfMissing(RegionID sourceRegion, RectTransform parent, Vector2 position, string label)
    {
        WhiteboxDraggableToken existing = FindToken(WhiteboxTokenType.Bread);
        if (existing != null && existing.SourceRegion == sourceRegion)
        {
            return;
        }

        SpawnToken(WhiteboxTokenType.Bread, sourceRegion, parent, label, new Color(0.92f, 0.52f, 0.18f, 0.96f), position);
    }

    private RectTransform GetOutdoorTokenParent(RegionID region)
    {
        RegionView view;
        if (regionViews.TryGetValue(region, out view) && view.TokenLayer != null)
        {
            return view.TokenLayer;
        }

        if (boardOverlay != null)
        {
            return boardOverlay;
        }

        return root;
    }

    private WhiteboxDraggableToken FindToken(WhiteboxTokenType tokenType)
    {
        for (int i = 0; i < activeTokens.Count; i++)
        {
            if (activeTokens[i] != null && activeTokens[i].TokenType == tokenType)
            {
                return activeTokens[i];
            }
        }

        return null;
    }

    private WhiteboxDraggableToken FindIndoorToken(WhiteboxTokenType tokenType)
    {
        for (int i = 0; i < activeTokens.Count; i++)
        {
            WhiteboxDraggableToken token = activeTokens[i];
            if (token != null && token.TokenType == tokenType && indoorRoot != null && token.transform.IsChildOf(indoorRoot))
            {
                return token;
            }
        }

        return null;
    }

    private void RemoveIndoorTokenOfType(WhiteboxTokenType tokenType)
    {
        for (int i = activeTokens.Count - 1; i >= 0; i--)
        {
            WhiteboxDraggableToken token = activeTokens[i];
            if (token != null && token.TokenType == tokenType && indoorRoot != null && token.transform.IsChildOf(indoorRoot))
            {
                activeTokens.RemoveAt(i);
                DisposeToken(token);
            }
        }
    }

    private void RemoveIndoorRuntimeTokenOfType(WhiteboxTokenType tokenType)
    {
        for (int i = activeTokens.Count - 1; i >= 0; i--)
        {
            WhiteboxDraggableToken token = activeTokens[i];
            if (token != null &&
                token.DestroyOnConsume &&
                token.TokenType == tokenType &&
                indoorRoot != null &&
                token.transform.IsChildOf(indoorRoot))
            {
                activeTokens.RemoveAt(i);
                DisposeToken(token);
            }
        }
    }

    private void RemoveTokenOfType(WhiteboxTokenType tokenType)
    {
        for (int i = activeTokens.Count - 1; i >= 0; i--)
        {
            WhiteboxDraggableToken token = activeTokens[i];
            if (token != null && token.TokenType == tokenType)
            {
                activeTokens.RemoveAt(i);
                DisposeToken(token);
            }
        }
    }

    private void ConsumeToken(WhiteboxDraggableToken token)
    {
        activeTokens.Remove(token);
        if (token == cartCargoToken)
        {
            cartCargoToken = null;
        }

        DisposeToken(token);
    }

    private void DisposeToken(WhiteboxDraggableToken token)
    {
        if (token == null)
        {
            return;
        }

        token.MarkConsumed();
        if (token.DestroyOnConsume)
        {
            Destroy(token.gameObject);
        }
        else
        {
            token.DisablePersistentToken();
        }
    }

    private void RefreshAll()
    {
        foreach (RegionView view in regionViews.Values)
        {
            RefreshRegion(view);
        }

        RefreshCart();
        RefreshRoadNodes();
        RefreshOutdoorDropZoneRaycasts();
        RefreshHud();
        RefreshActionButtons();
        RefreshIndoor();
    }

    private void RefreshRegion(RegionView view)
    {
        TimeState time = regionTimes[view.Region];

        if (view.Background != null)
        {
            Sprite background = GetRegionBackgroundSprite(view, time);
            if (HasRegionBackgroundSprites(view))
            {
                view.Background.sprite = background;
                if (background != null)
                {
                    view.Background.color = Color.white;
                    view.Background.preserveAspect = true;
                }
            }
        }

        if (view.Title != null)
        {
            view.Title.text = GetRegionTitle(view.Region);
        }

        if (view.State != null)
        {
            view.State.text = GetTimeName(time);
        }

        if (view.Hint != null)
        {
            view.Hint.text = GetRegionHint(view.Region, time);
        }

        RefreshTimeSlider(view.TimeSlider, view.TimeSliderLabel, time);
    }

    private Sprite GetRegionBackgroundSprite(RegionView view, TimeState time)
    {
        if (view.Region == RegionID.Kitchen && HasActiveKitchenRocket() && kitchenRocketBackgroundSprite != null)
        {
            return kitchenRocketBackgroundSprite;
        }

        if (time == TimeState.Spring) return view.SpringBackground;
        if (time == TimeState.War) return view.WarBackground;
        return view.AutumnBackground;
    }

    private bool HasRegionBackgroundSprites(RegionView view)
    {
        return view.SpringBackground != null ||
               view.WarBackground != null ||
               view.AutumnBackground != null ||
               (view.Region == RegionID.Kitchen && kitchenRocketBackgroundSprite != null);
    }

    private void SetActiveIfNotNull(Component component, bool active)
    {
        if (component != null)
        {
            component.gameObject.SetActive(active);
        }
    }

    private void SetButtonActive(Button button, bool active)
    {
        if (button != null)
        {
            button.gameObject.SetActive(active);
        }
    }

    private void SetButtonInteractable(Button button, bool interactable)
    {
        if (button != null)
        {
            button.interactable = interactable;
        }
    }

    private void RefreshTimeSlider(Slider slider, Text label, TimeState time)
    {
        if (slider == null)
        {
            return;
        }

        slider.SetValueWithoutNotify(TimeToSliderValue(time));
        slider.interactable = !timeLocked;

        Image handle = slider.handleRect != null ? slider.handleRect.GetComponent<Image>() : null;
        if (handle != null)
        {
            handle.color = GetTimeHandleColor(time);
        }

        if (label != null)
        {
            label.text = "春        战        秋";
            label.color = time == TimeState.War ? ink : muted;
        }
    }

    private void RefreshCart()
    {
        if (!cartNodes.ContainsKey(cartNodeId))
        {
            cartNodeId = GetFirstAvailableCartNode();
        }

        if (cartDropZone != null)
        {
            cartDropZone.Initialize(this, WhiteboxDropZoneType.Cart, GetCartRegion());
        }

        if (cartVisual != null)
        {
            HideTextChildren(cartVisual);
            if (boardOverlay != null)
            {
                PlaceOnBoard(cartVisual, cartNodes[cartNodeId].BoardPosition, cartVisualSize);
            }
            else
            {
                Place(cartVisual, cartNodes[cartNodeId].BoardPosition, cartVisualSize);
            }

            cartVisual.SetAsLastSibling();
        }

        ApplyCartImage(cartMoving);

        if (cartText != null)
        {
            cartText.text = "";
        }

        SyncCartCargoToken();
    }

    private void ApplyCartImage(bool moving)
    {
        Image image = ResolveCartImage();
        if (image == null)
        {
            return;
        }

        Sprite sprite = GetCartSprite(moving);
        if (sprite != null)
        {
            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = true;
        }
        else
        {
            image.sprite = null;
            image.color = GetCartColor();
        }
    }

    private Image ResolveCartImage()
    {
        if (cartImage != null)
        {
            return cartImage;
        }

        if (cartVisual == null)
        {
            return null;
        }

        cartImage = cartVisual.GetComponent<Image>();
        if (cartImage == null)
        {
            cartImage = cartVisual.GetComponentInChildren<Image>(true);
        }

        return cartImage;
    }

    private Sprite GetCartSprite(bool moving)
    {
        if (moving && hasActiveMoveSegment)
        {
            Sprite moveSprite = FindCartMoveSprite(movingFromNode, movingToNode, cartCargo);
            if (moveSprite != null)
            {
                return moveSprite;
            }

            Sprite movingDirectionSprite = FindCartDirectionSprite(cartCargo, cartDirection);
            if (movingDirectionSprite != null)
            {
                return movingDirectionSprite;
            }

            return GetCargoCartSprite(cartCargo);
        }

        Sprite pointSprite = FindCartPointSprite(cartNodeId, cartCargo, GetCartPointDirection());
        if (pointSprite != null)
        {
            return pointSprite;
        }

        Sprite directionSprite = FindCartDirectionSprite(cartCargo, GetCartPointDirection());
        if (directionSprite != null)
        {
            return directionSprite;
        }

        return GetCargoCartSprite(cartCargo);
    }

    private CartDirection GetCartPointDirection()
    {
        CartNode node;
        if (cartNodes.TryGetValue(cartNodeId, out node) && node.UseFacingOverride)
        {
            return node.FacingOverride;
        }

        return cartDirection;
    }

    private Sprite FindCartPointSprite(WhiteboxCartNodeId node, CartCargo cargo, CartDirection direction)
    {
        for (int i = 0; i < cartPointSprites.Count; i++)
        {
            CartNodeSpriteBinding binding = cartPointSprites[i];
            if (binding != null && binding.Node == node && binding.Cargo == cargo && binding.Direction == direction && binding.Sprite != null)
            {
                return binding.Sprite;
            }
        }

        for (int i = 0; i < cartPointSprites.Count; i++)
        {
            CartNodeSpriteBinding binding = cartPointSprites[i];
            if (binding != null && binding.Node == node && binding.Cargo == cargo && binding.Sprite != null)
            {
                return binding.Sprite;
            }
        }

        return null;
    }

    private Sprite FindCartDirectionSprite(CartCargo cargo, CartDirection direction)
    {
        for (int i = 0; i < cartDirectionSprites.Count; i++)
        {
            CartDirectionSpriteBinding binding = cartDirectionSprites[i];
            if (binding != null && binding.Cargo == cargo && binding.Direction == direction && binding.Sprite != null)
            {
                return binding.Sprite;
            }
        }

        if (cargo != CartCargo.Empty)
        {
            return null;
        }

        for (int i = 0; i < cartDirectionSprites.Count; i++)
        {
            CartDirectionSpriteBinding binding = cartDirectionSprites[i];
            if (binding != null && binding.Cargo == CartCargo.Empty && binding.Direction == direction && binding.Sprite != null)
            {
                return binding.Sprite;
            }
        }

        return null;
    }

    private Sprite FindCartMoveSprite(WhiteboxCartNodeId from, WhiteboxCartNodeId to, CartCargo cargo)
    {
        for (int i = 0; i < cartMoveSprites.Count; i++)
        {
            CartMoveSpriteBinding binding = cartMoveSprites[i];
            if (binding != null && binding.From == from && binding.To == to && binding.Cargo == cargo && binding.Sprite != null)
            {
                return binding.Sprite;
            }
        }

        if (cargo != CartCargo.Empty)
        {
            return null;
        }

        for (int i = 0; i < cartMoveSprites.Count; i++)
        {
            CartMoveSpriteBinding binding = cartMoveSprites[i];
            if (binding != null && binding.From == from && binding.To == to && binding.Sprite != null)
            {
                return binding.Sprite;
            }
        }

        return null;
    }

    private Sprite GetCargoCartSprite(CartCargo cargo)
    {
        if (cargo == CartCargo.Wheat) return wheatCartSprite != null ? wheatCartSprite : emptyCartSprite;
        if (cargo == CartCargo.Flour) return flourCartSprite != null ? flourCartSprite : emptyCartSprite;
        if (cargo == CartCargo.Water) return waterCartSprite != null ? waterCartSprite : emptyCartSprite;
        if (cargo == CartCargo.Bread) return breadCartSprite != null ? breadCartSprite : emptyCartSprite;
        return emptyCartSprite;
    }


    private void SyncCartCargoToken()
    {
        WhiteboxTokenType expectedType;

        if (cartVisual == null || !TryGetCartCargoTokenInfo(out expectedType))
        {
            RemoveCartCargoToken();
            return;
        }

        CanvasGroup group = cartVisual.GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = cartVisual.gameObject.AddComponent<CanvasGroup>();
        }

        group.blocksRaycasts = true;
        group.interactable = true;

        cartCargoToken = cartVisual.GetComponent<WhiteboxDraggableToken>();
        if (cartCargoToken == null)
        {
            cartCargoToken = cartVisual.gameObject.AddComponent<WhiteboxDraggableToken>();
        }

        cartCargoToken.Initialize(this, expectedType, GetCartRegion(), false);
        cartCargoToken.enabled = true;
        if (!activeTokens.Contains(cartCargoToken))
        {
            activeTokens.Add(cartCargoToken);
        }
    }

    private bool TryGetCartCargoTokenInfo(out WhiteboxTokenType tokenType)
    {
        tokenType = WhiteboxTokenType.Wheat;

        if (cartCargo == CartCargo.Flour)
        {
            tokenType = WhiteboxTokenType.Flour;
            return true;
        }

        if (cartCargo == CartCargo.Wheat)
        {
            tokenType = WhiteboxTokenType.Wheat;
            return true;
        }

        if (cartCargo == CartCargo.Water)
        {
            tokenType = WhiteboxTokenType.Water;
            return true;
        }

        if (cartCargo == CartCargo.Bread)
        {
            tokenType = WhiteboxTokenType.Bread;
            return true;
        }

        return false;
    }

    private void RemoveCartCargoToken()
    {
        if (cartCargoToken == null)
        {
            return;
        }

        activeTokens.Remove(cartCargoToken);
        if (cartCargoToken.transform == cartVisual)
        {
            cartCargoToken.DisablePersistentToken();
            EnsureCartDropZoneRaycastEnabled();
        }
        else
        {
            Destroy(cartCargoToken.gameObject);
        }

        cartCargoToken = null;
    }

    private void EnsureCartDropZoneRaycastEnabled()
    {
        if (cartVisual == null)
        {
            return;
        }

        CanvasGroup group = cartVisual.GetComponent<CanvasGroup>();
        if (group != null)
        {
            group.blocksRaycasts = true;
            group.interactable = true;
        }

        Image image = cartImage != null ? cartImage : cartVisual.GetComponent<Image>();
        if (image != null)
        {
            image.raycastTarget = true;
        }
    }

    private void RefreshRoadNodes()
    {
        foreach (CartNode node in cartNodes.Values)
        {
            if (node.Button == null)
            {
                continue;
            }

            node.Button.interactable = !cartMoving && currentIndoorRoom == WhiteboxIndoorRoom.None && !girlFed;
        }
    }

    private void UpdateCartPointBlink()
    {
        if (!blinkReachableCartPoints || !cartNodes.ContainsKey(cartNodeId))
        {
            ResetCartPointBlinkAlpha();
            return;
        }

        bool canShowHint = currentIndoorRoom == WhiteboxIndoorRoom.None && !cartMoving && !girlFed;
        float speed = Mathf.Max(0.1f, reachablePointBlinkSpeed);
        float minAlpha = Mathf.Clamp01(reachablePointMinAlpha);
        float pulse = (Mathf.Sin(Time.unscaledTime * speed) + 1f) * 0.5f;
        float blinkAlpha = Mathf.Lerp(minAlpha, 1f, pulse);

        foreach (CartNode node in cartNodes.Values)
        {
            CanvasGroup group = GetCartPointCanvasGroup(node);
            if (group == null)
            {
                continue;
            }

            bool reachable = canShowHint &&
                node.Id != cartNodeId &&
                FindPath(cartNodeId, node.Id, true) != null;

            group.alpha = reachable ? node.ButtonBaseAlpha * blinkAlpha : node.ButtonBaseAlpha;
        }
    }

    private void ResetCartPointBlinkAlpha()
    {
        foreach (CartNode node in cartNodes.Values)
        {
            CanvasGroup group = GetCartPointCanvasGroup(node);
            if (group != null)
            {
                group.alpha = node.ButtonBaseAlpha;
            }
        }
    }

    private CanvasGroup GetCartPointCanvasGroup(CartNode node)
    {
        if (node == null || node.Button == null)
        {
            return null;
        }

        if (node.ButtonCanvasGroup == null)
        {
            node.ButtonCanvasGroup = node.Button.GetComponent<CanvasGroup>();
            if (node.ButtonCanvasGroup == null)
            {
                node.ButtonCanvasGroup = node.Button.gameObject.AddComponent<CanvasGroup>();
            }

            node.ButtonBaseAlpha = node.ButtonCanvasGroup.alpha;
        }

        return node.ButtonCanvasGroup;
    }


    private void RefreshHud()
    {
        if (statusText != null)
        {
            statusText.text =
                "位置：" + (cartNodes.ContainsKey(cartNodeId) ? cartNodes[cartNodeId].Name : "未设置") +
                " / " + GetCargoName(cartCargo) +
                "    厨房：" + GetKitchenStatus();
        }

        if (checklistText != null)
        {
            checklistText.text =
                "进度 " +
                Check(cartCargo == CartCargo.Wheat || cartCargo == CartCargo.Flour || HasKitchenFlourProgress() || cartCargo == CartCargo.Bread || girlFed) + "收麦 " +
                Check(cartCargo == CartCargo.Flour || HasKitchenFlourProgress() || cartCargo == CartCargo.Bread || girlFed) + "磨粉 " +
                Check(HasKitchenWaterProgress() || cartCargo == CartCargo.Bread || girlFed) + "取水 " +
                Check(HasKitchenOvenProgress() || cartCargo == CartCargo.Bread || girlFed) + "入炉 " +
                Check(ovenLit || HasKitchenBreadProgress() || cartCargo == CartCargo.Bread || girlFed) + "点火 " +
                Check(HasKitchenBreadProgress() || cartCargo == CartCargo.Bread || girlFed) + "出炉 " +
                Check(girlFed) + "交付";
        }

        if (logText != null)
        {
            logText.text = logLines.Count > 0 ? "反馈：" + logLines[0] : "";
        }
    }


    private void RefreshActionButtons()
    {
        foreach (RegionView view in regionViews.Values)
        {
            for (int i = 0; i < view.ActionButtons.Count; i++)
            {
                view.ActionButtons[i].interactable = !girlFed && currentIndoorRoom == WhiteboxIndoorRoom.None;
            }
        }

        if (harvestWheatButton != null && wheatHarvested)
        {
            harvestWheatButton.interactable = false;
        }

        if (collectWaterButton != null)
        {
            collectWaterButton.interactable = CanCollectWaterNow();
        }
    }

    private void RefreshOutdoorDropZoneRaycasts()
    {
        SetDropZoneRaycastEnabled(kitchenDoorDropZoneRect, kitchenDoorDropZone, currentIndoorRoom == WhiteboxIndoorRoom.None);
        SetDropZoneRaycastEnabled(
            cellarEntranceDropZoneRect,
            cellarEntranceDropZone,
            currentIndoorRoom == WhiteboxIndoorRoom.None && regionTimes[RegionID.Cellar] == TimeState.Spring);
    }

    private void SetDropZoneRaycastEnabled(RectTransform rect, WhiteboxDropZone zone, bool enabled)
    {
        if (zone != null)
        {
            zone.enabled = enabled;
        }

        if (rect == null)
        {
            return;
        }

        CanvasGroup group = rect.GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = rect.gameObject.AddComponent<CanvasGroup>();
        }

        group.blocksRaycasts = enabled;
        group.interactable = enabled;

        Image image = rect.GetComponent<Image>();
        if (image != null)
        {
            image.raycastTarget = enabled;
        }
    }

    private void RefreshIndoor()
    {
        if (indoorRoot == null || currentIndoorRoom == WhiteboxIndoorRoom.None)
        {
            return;
        }

        bool kitchen = currentIndoorRoom == WhiteboxIndoorRoom.Kitchen;
        RegionID region = kitchen ? RegionID.Kitchen : RegionID.Cellar;
        TimeState time = regionTimes[region];
        DeactivateLegacyIndoorControls();
        RefreshIndoorRoomRoots(kitchen);
        if (kitchenIndoorRoot == null)
        {
            SetKitchenOnlyNamedObjects(kitchen);
        }

        HideLegacyCellarDeliveryObjects();

        if (indoorTitle != null)
        {
            indoorTitle.text = kitchen ? "厨房内部 C" : "地窖内部 B";
        }

        RefreshIndoorBackground(kitchen, time);
        RefreshIndoorSeasonFilter(kitchen, time);
        BringIndoorControlsToFront();

        if (indoorHint != null)
        {
            if (kitchen)
            {
                indoorHint.text = GetKitchenIndoorHint(time);
            }
            else
            {
                indoorHint.text = time == TimeState.War
                    ? (girlFed ? "战时地窖：小女孩已经吃到面包。" : "战时地窖：小女孩出现。把装面包的篮子拖给她。")
                    : "先从春天进入地窖；进入后切到战时，小女孩才会出现。";
            }
        }

        SetActiveIfNotNull(indoorFireboxDropZone, kitchen);
        SetActiveIfNotNull(indoorPrepBoardDropZone, kitchen);
        SetActiveIfNotNull(indoorOvenDropZone, kitchen);
        SetActiveIfNotNull(indoorKitchenDoorDropZone, kitchen);
        if (kitchen && cellarIndoorRoot == null)
        {
            HideCellarOnlyNamedObjectsInKitchen();
        }

        RefreshIndoorMarkers(kitchen, time);
        RefreshCellarBasketVisual();
        RefreshKitchenWindowPatch(kitchen, time);
        RefreshKitchenObjectImages(kitchen);

        SetButtonActive(closeIndoorButton, !girlFed);
        SetButtonActive(indoorPrepBoardButton, kitchen);

        SetButtonInteractable(indoorPrepBoardButton, kitchen && time == TimeState.Spring && kitchenProcess == KitchenProcessState.WaterAdded);

        RefreshTimeSlider(indoorTimeSlider, indoorTimeSliderLabel, time);

        if (kitchen)
        {
            RefreshKitchenIndoorTokens();
            if (time == TimeState.War)
            {
                SpawnArrowInKitchenIfNeeded();
            }
            else
            {
                RemoveTokenOfType(WhiteboxTokenType.Arrow);
            }
        }
        else
        {
            RemoveTokenOfType(WhiteboxTokenType.Arrow);
            RefreshKitchenIndoorTokens();
        }
    }

    private void RefreshIndoorBackground(bool kitchen, TimeState time)
    {
        RefreshIndoorRoomRoots(kitchen);

        if (kitchenIndoorBackgroundImage != null)
        {
            kitchenIndoorBackgroundImage.gameObject.SetActive(kitchen);
            kitchenIndoorBackgroundImage.raycastTarget = false;
        }

        if (cellarIndoorBackgroundImage != null)
        {
            cellarIndoorBackgroundImage.gameObject.SetActive(!kitchen);
            cellarIndoorBackgroundImage.raycastTarget = false;
        }
    }

    private void RefreshIndoorRoomRoots(bool kitchen)
    {
        if (kitchenIndoorRoot != null)
        {
            kitchenIndoorRoot.gameObject.SetActive(kitchen);
        }

        if (cellarIndoorRoot != null)
        {
            cellarIndoorRoot.gameObject.SetActive(!kitchen);
        }
    }

    private void BringIndoorControlsToFront()
    {
        if (indoorTokenLayer != null)
        {
            indoorTokenLayer.SetAsLastSibling();
        }

        BringTransformToFront(indoorTitle != null ? indoorTitle.transform : null);
        BringTransformToFront(indoorHint != null ? indoorHint.transform : null);
        BringTransformToFront(indoorTimeSlider != null ? indoorTimeSlider.transform.parent : null);
        BringTransformToFront(closeIndoorButton != null ? closeIndoorButton.transform : null);
    }

    private void BringTransformToFront(Transform target)
    {
        if (target != null)
        {
            target.SetAsLastSibling();
        }
    }

    private void RefreshIndoorSeasonFilter(bool kitchen, TimeState time)
    {
        Image kitchenFilter = kitchenIndoorSeasonFilterImage;
        Image cellarFilter = cellarIndoorSeasonFilterImage;

        if (kitchenFilter != null)
        {
            kitchenFilter.gameObject.SetActive(kitchen);
            if (kitchen)
            {
                kitchenFilter.color = GetIndoorSeasonFilterColor(time);
                kitchenFilter.raycastTarget = false;
            }
        }

        if (cellarFilter != null)
        {
            cellarFilter.gameObject.SetActive(!kitchen);
            if (!kitchen)
            {
                cellarFilter.color = GetIndoorSeasonFilterColor(time);
                cellarFilter.raycastTarget = false;
            }
        }
    }

    private Color GetIndoorSeasonFilterColor(TimeState time)
    {
        if (time == TimeState.Spring) return indoorSpringFilterColor;
        if (time == TimeState.War) return indoorWarFilterColor;
        return indoorAutumnFilterColor;
    }

    private void RefreshKitchenObjectImages(bool kitchen)
    {
        SetImageActive(indoorPrepBoardImage, kitchen);
        SetImageActive(indoorOvenImage, kitchen);
        RefreshKitchenFireImage(kitchen);
        RefreshKitchenIngredientImages(kitchen);
        RefreshKitchenDoughAndBreadImages(kitchen);

        if (!kitchen)
        {
            return;
        }

        ApplyOptionalImage(indoorPrepBoardImage, GetPrepBoardSprite());
        ApplyOptionalImage(indoorOvenImage, GetOvenSprite());
    }

    private void RefreshKitchenIngredientImages(bool kitchen)
    {
        bool showFlour = kitchen && flourAtKitchenDoor;
        bool showWater = kitchen && waterAtKitchenDoor;

        SetKitchenPersistentObject(kitchenDoorFlourImage, WhiteboxTokenType.Flour, showFlour, showFlour);
        SetKitchenPersistentObject(kitchenDoorWaterImage, WhiteboxTokenType.Water, showWater, showWater);
    }

    private void RefreshKitchenDoughAndBreadImages(bool kitchen)
    {
        bool showRawDough = kitchen && kitchenProcess == KitchenProcessState.DoughMixed;
        bool showFermentedDough = kitchen && kitchenProcess == KitchenProcessState.DoughFermented;
        bool showMoldedDough = kitchen && kitchenProcess == KitchenProcessState.DoughMolded;
        bool showBreadReady = kitchen && kitchenProcess == KitchenProcessState.BreadReady;

        SetKitchenPersistentObject(kitchenRawDoughImage, WhiteboxTokenType.Dough, showRawDough, false);
        SetKitchenPersistentObject(kitchenFermentedDoughImage, WhiteboxTokenType.Dough, showFermentedDough, showFermentedDough);
        SetKitchenPersistentObject(kitchenMoldedDoughImage, WhiteboxTokenType.Dough, showMoldedDough, false);
        SetKitchenPersistentObject(kitchenBreadReadyImage, WhiteboxTokenType.Bread, showBreadReady, true);
    }

    private void SetKitchenPersistentObject(Image image, WhiteboxTokenType tokenType, bool visible, bool draggable)
    {
        if (image == null)
        {
            return;
        }

        HideTextChildren(image.rectTransform);
        image.gameObject.SetActive(visible);
        image.raycastTarget = visible && draggable;

        if (visible)
        {
            Sprite sprite = image.sprite != null ? image.sprite : GetTokenSprite(tokenType);

            if (sprite != null)
            {
                image.sprite = sprite;
                image.color = Color.white;
                image.preserveAspect = true;
            }
            else
            {
                image.sprite = null;
                image.color = GetDragTokenFallbackColor(tokenType);
            }

            image.rectTransform.SetAsLastSibling();
        }

        if (!visible || !draggable)
        {
            DeactivatePersistentDragToken(image.rectTransform);
            return;
        }

        CanvasGroup group = image.GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = image.gameObject.AddComponent<CanvasGroup>();
        }

        group.alpha = 1f;
        group.blocksRaycasts = true;
        group.interactable = true;

        WhiteboxDraggableToken token = image.GetComponent<WhiteboxDraggableToken>();
        if (token == null)
        {
            token = image.gameObject.AddComponent<WhiteboxDraggableToken>();
        }

        token.Initialize(this, tokenType, RegionID.Kitchen, false);
        token.enabled = true;

        if (!activeTokens.Contains(token))
        {
            activeTokens.Add(token);
        }
    }

    private void DeactivatePersistentDragToken(RectTransform visual)
    {
        if (visual == null)
        {
            return;
        }

        WhiteboxDraggableToken token = visual.GetComponent<WhiteboxDraggableToken>();
        if (token != null)
        {
            activeTokens.Remove(token);
            token.DisablePersistentToken();
        }

        CanvasGroup group = visual.GetComponent<CanvasGroup>();
        if (group != null)
        {
            group.blocksRaycasts = false;
        }
    }

    private void RefreshKitchenFireImage(bool kitchen)
    {
        SetImageActive(kitchenFireImage, kitchen && ovenLit);
        if (kitchenFireImage != null && kitchen && ovenLit && kitchenFireLitSprite != null)
        {
            kitchenFireImage.sprite = kitchenFireLitSprite;
            kitchenFireImage.color = Color.white;
            kitchenFireImage.preserveAspect = true;
        }
    }

    private void RefreshKitchenWindowPatch(bool kitchen, TimeState time)
    {
        if (kitchenWindowPatchImage == null)
        {
            return;
        }

        kitchenWindowPatchImage.gameObject.SetActive(kitchen);
        if (!kitchen)
        {
            return;
        }

        Sprite sprite = kitchenWindowSpringSprite;
        Color fallback = spring;
        if (time == TimeState.War)
        {
            sprite = kitchenWindowWarSprite;
            fallback = war;
        }
        else if (time == TimeState.Autumn)
        {
            sprite = kitchenWindowAutumnSprite;
            fallback = autumn;
        }

        if (sprite != null)
        {
            kitchenWindowPatchImage.sprite = sprite;
            kitchenWindowPatchImage.color = Color.white;
            kitchenWindowPatchImage.preserveAspect = true;
        }
        else
        {
            kitchenWindowPatchImage.sprite = null;
            kitchenWindowPatchImage.color = fallback;
        }
    }

    private void SetImageActive(Image image, bool active)
    {
        if (image != null)
        {
            image.gameObject.SetActive(active);
        }
    }

    private Sprite GetPrepBoardSprite()
    {
        if (kitchenProcess == KitchenProcessState.FlourPlaced) return prepBoardFlourSprite;
        if (kitchenProcess == KitchenProcessState.WaterAdded) return prepBoardFlourSprite;
        if (kitchenProcess == KitchenProcessState.DoughMixed) return prepBoardDoughSprite;
        if (kitchenProcess == KitchenProcessState.DoughFermented) return prepBoardFermentedDoughSprite != null ? prepBoardFermentedDoughSprite : prepBoardDoughSprite;
        if (kitchenProcess == KitchenProcessState.DoughMolded) return prepBoardMoldedDoughSprite != null ? prepBoardMoldedDoughSprite : prepBoardDoughSprite;
        return prepBoardEmptySprite;
    }

    private Sprite GetOvenSprite()
    {
        if (kitchenProcess == KitchenProcessState.BreadReady || kitchenProcess == KitchenProcessState.BreadAtDoor) return ovenBreadSprite;
        if (kitchenProcess == KitchenProcessState.Baking) return ovenBakingSprite != null ? ovenBakingSprite : ovenDoughSprite;
        if (kitchenProcess == KitchenProcessState.DoughInOven) return ovenDoughSprite;
        return null;
    }

    private void ApplyOptionalImage(Image image, Sprite sprite)
    {
        if (image == null || sprite == null)
        {
            if (image != null)
            {
                image.sprite = null;
            }
            return;
        }

        image.sprite = sprite;
        image.color = Color.white;
        image.preserveAspect = true;
    }


    private void RefreshIndoorMarkers(bool kitchen, TimeState time)
    {
        HideKitchenProcessVisual();
        RefreshCellarGirlVisual(!kitchen && time == TimeState.War);
    }

    private void HideKitchenProcessVisual()
    {
        SetDeepChildrenActiveByName(indoorRoot, "KitchenProcessMarker", false);
        SetDeepChildrenActiveByName(indoorRoot, "IndoorProcessMarker", false);
        SetDeepChildrenActiveByName(indoorRoot, "KitchenIndoorProcessMarker", false);
    }

    private void RefreshCellarGirlVisual(bool visible)
    {
        SyncCellarHierarchyReferences();

        if (!visible)
        {
            if (cellarGirlVisual != null)
            {
                cellarGirlVisual.gameObject.SetActive(false);
            }

            return;
        }

        EnsureCellarGirlVisual();

        if (cellarGirlVisual == null || cellarGirlImage == null)
        {
            return;
        }

        Image image = cellarGirlVisual.GetComponent<Image>();
        if (image != null)
        {
            cellarGirlImage = image;
            ApplyCellarGirlFrame();
            image.raycastTarget = true;
        }

        WhiteboxDropZone girlDropZone = cellarGirlVisual.GetComponent<WhiteboxDropZone>();
        if (girlDropZone == null)
        {
            girlDropZone = cellarGirlVisual.gameObject.AddComponent<WhiteboxDropZone>();
        }

        girlDropZone.Initialize(this, WhiteboxDropZoneType.CellarDelivery, RegionID.Cellar);
        girlDropZone.enabled = !girlFed;

        cellarGirlVisual.gameObject.SetActive(true);
        cellarGirlVisual.SetAsLastSibling();
        if (cellarGirlText != null)
        {
            cellarGirlText.text = girlFed ? "小女孩\n吃面包" : "小女孩\n发抖";
            cellarGirlText.raycastTarget = false;
            cellarGirlText.gameObject.SetActive(!HasCellarGirlFrames(girlFed));
        }

        WhiteboxCellarGirlClickTarget clickTarget = cellarGirlVisual.GetComponent<WhiteboxCellarGirlClickTarget>();
        if (clickTarget == null)
        {
            clickTarget = cellarGirlVisual.gameObject.AddComponent<WhiteboxCellarGirlClickTarget>();
        }

        clickTarget.Initialize(this);

        Button girlButton = cellarGirlVisual.GetComponent<Button>();
        if (girlButton == null)
        {
            girlButton = cellarGirlVisual.gameObject.AddComponent<Button>();
            girlButton.transition = Selectable.Transition.None;
        }

        girlButton.onClick.RemoveAllListeners();
        girlButton.interactable = girlFed && endingRoutine == null;
        if (girlButton.interactable)
        {
            girlButton.onClick.AddListener(OnCellarGirlClicked);
        }
    }

    private void EnsureCellarGirlVisual()
    {
        SyncCellarHierarchyReferences();
    }

    private bool HasCellarGirlFrames(bool eating)
    {
        List<Sprite> frames = eating ? cellarGirlEatingSprites : cellarGirlWaitingSprites;
        if (frames == null)
        {
            return false;
        }

        for (int i = 0; i < frames.Count; i++)
        {
            if (frames[i] != null)
            {
                return true;
            }
        }

        return false;
    }

    private void UpdateCellarGirlAnimation()
    {
        if (currentIndoorRoom != WhiteboxIndoorRoom.Cellar ||
            cellarGirlVisual == null ||
            !cellarGirlVisual.gameObject.activeInHierarchy)
        {
            return;
        }

        ApplyCellarGirlFrame();
    }

    private void ApplyCellarGirlFrame()
    {
        if (cellarGirlImage == null)
        {
            return;
        }

        List<Sprite> frames = girlFed ? cellarGirlEatingSprites : cellarGirlWaitingSprites;
        if (frames != null && frames.Count > 0)
        {
            float frameSeconds = Mathf.Max(0.05f, cellarGirlAnimationFrameSeconds);
            int index = Mathf.FloorToInt(Time.time / frameSeconds) % frames.Count;
            Sprite sprite = frames[index];
            if (sprite == null)
            {
                for (int i = 0; i < frames.Count; i++)
                {
                    if (frames[i] != null)
                    {
                        sprite = frames[i];
                        break;
                    }
                }
            }

            if (sprite != null)
            {
                cellarGirlImage.sprite = sprite;
                cellarGirlImage.color = Color.white;
                cellarGirlImage.preserveAspect = true;
                return;
            }
        }

        cellarGirlImage.sprite = null;
        cellarGirlImage.color = girlFed ? new Color(1f, 0.68f, 0.34f, 0.94f) : new Color(0.72f, 0.78f, 0.92f, 0.94f);
    }

    private string GetKitchenIndoorHint(TimeState time)
    {
        string fireHint;
        if (ovenLit)
        {
            fireHint = "炉膛已点燃；如果厨房切到战后，雨水会把火浇灭。";
        }
        else if (rocketTriggered)
        {
            fireHint = CanTriggerRocketEvent() ? "窗户上有火箭，现在可以拖到炉膛点火。" : "火箭需要 A-2 战时、A-1 春天、A-3 战时同时成立才会出现。";
        }
        else
        {
            fireHint = "点火事件：A-2 切到战时、A-1 保持春天、A-3 切到战时，火箭才会钉到窗户。";
        }

        return "时间：" + GetTimeName(time) + "。案板只在春天可用；清水加入后案板图不变，点击案板变成揉好面团；切到战时变成发酵好的面团，战后发霉。\n" + fireHint;
    }


    private bool HasKitchenFlourProgress()
    {
        return kitchenProcess == KitchenProcessState.FlourPlaced ||
               kitchenProcess == KitchenProcessState.WaterAdded ||
               kitchenProcess == KitchenProcessState.DoughMixed ||
               kitchenProcess == KitchenProcessState.DoughFermented ||
               kitchenProcess == KitchenProcessState.DoughInOven ||
               kitchenProcess == KitchenProcessState.Baking ||
               kitchenProcess == KitchenProcessState.BreadReady ||
               kitchenProcess == KitchenProcessState.BreadAtDoor;
    }

    private bool HasKitchenWaterProgress()
    {
        return kitchenProcess == KitchenProcessState.WaterAdded ||
               kitchenProcess == KitchenProcessState.DoughMixed ||
               kitchenProcess == KitchenProcessState.DoughFermented ||
               kitchenProcess == KitchenProcessState.DoughInOven ||
               kitchenProcess == KitchenProcessState.Baking ||
               kitchenProcess == KitchenProcessState.BreadReady ||
               kitchenProcess == KitchenProcessState.BreadAtDoor;
    }

    private bool HasKitchenOvenProgress()
    {
        return kitchenProcess == KitchenProcessState.DoughInOven ||
               kitchenProcess == KitchenProcessState.Baking ||
               kitchenProcess == KitchenProcessState.BreadReady ||
               kitchenProcess == KitchenProcessState.BreadAtDoor;
    }

    private bool HasKitchenBreadProgress()
    {
        return kitchenProcess == KitchenProcessState.BreadReady ||
               kitchenProcess == KitchenProcessState.BreadAtDoor;
    }

    private string GetKitchenStatus()
    {
        List<string> parts = new List<string>();
        if (flourAtKitchenDoor) parts.Add("门口面粉");
        if (waterAtKitchenDoor) parts.Add("门口水");

        if (kitchenProcess == KitchenProcessState.FlourPlaced)
        {
            parts.Add("面粉");
        }
        else if (kitchenProcess == KitchenProcessState.WaterAdded)
        {
            parts.Add("面粉");
            parts.Add("已加水");
        }
        else if (kitchenProcess == KitchenProcessState.DoughMixed)
        {
            parts.Add("揉好面团");
        }
        else if (kitchenProcess == KitchenProcessState.DoughFermented)
        {
            parts.Add("发酵面团");
        }
        else if (kitchenProcess == KitchenProcessState.DoughMolded)
        {
            parts.Add("发霉面团");
        }
        else if (kitchenProcess == KitchenProcessState.DoughInOven)
        {
            parts.Add("入炉");
        }
        else if (kitchenProcess == KitchenProcessState.Baking)
        {
            parts.Add("烘烤中");
        }
        else if (kitchenProcess == KitchenProcessState.BreadReady)
        {
            parts.Add("厨房面包");
        }
        else if (kitchenProcess == KitchenProcessState.BreadAtDoor)
        {
            parts.Add("门口面包");
        }

        if (ovenLit) parts.Add("有火");
        if (breadDestroyed) parts.Add("曾失败");
        return parts.Count == 0 ? "空" : string.Join(" / ", parts.ToArray());
    }

    private string Check(bool value)
    {
        return value ? "[x]" : "[ ]";
    }

    private void AddLog(string line)
    {
        logLines.Insert(0, line);
        while (logLines.Count > 7)
        {
            logLines.RemoveAt(logLines.Count - 1);
        }

        if (logText != null)
        {
            logText.text = logLines.Count > 0 ? "反馈：" + logLines[0] : "";
        }
    }

    private RegionID GetCartRegion()
    {
        return cartNodes.ContainsKey(cartNodeId) ? cartNodes[cartNodeId].Region : RegionID.Mill;
    }

    private string GetRegionTitle(RegionID region)
    {
        if (region == RegionID.Mill) return "A-1 磨坊 / 风车";
        if (region == RegionID.Field) return "A-2 麦田 / 道路";
        if (region == RegionID.Kitchen) return "A-3 厨房 / 烤炉";
        return "A-4 废墟 / 地窖";
    }

    private string GetRegionHint(RegionID region, TimeState time)
    {
        if (region == RegionID.Mill)
        {
            if (time == TimeState.Spring) return "春天：磨坊路、风车石磨可通行。";
            if (time == TimeState.War) return "战时：风车停转，道路危险。";
            return "秋天：道路积水，风车损坏。";
        }

        if (region == RegionID.Field)
        {
            if (time == TimeState.Spring) return "春天：道路可通，先把车推到麦田点。";
            if (time == TimeState.War) return "战时：道路危险，小推车无法跨格通过。";
            return "秋天：麦子成熟，但车不能再移动。";
        }

        if (region == RegionID.Kitchen)
        {
            if (ovenLit) return "炉火已点燃，可以提前完成，等待面团入炉或面包出炉。";
            if (time == TimeState.War) return "战时：火箭可钉在窗框上，进入厨房即可点炉。";
            return "厨房：进入内部后卸面粉、卸清水、揉面入炉。";
        }

        if (girlFed) return "地窖：小女孩正在吃面包。";
        if (time == TimeState.Autumn) return "秋天：井口附近有干净积水。";
        if (time == TimeState.War) return "战时：进入内部后小女孩出现。";
        return "春天：道路可通，可从室外进入地窖。";
    }

    private string GetRegionName(RegionID region)
    {
        if (region == RegionID.Mill) return "磨坊";
        if (region == RegionID.Field) return "麦田";
        if (region == RegionID.Kitchen) return "厨房";
        if (region == RegionID.Cellar) return "地窖";
        return region.ToString();
    }

    private string GetTimeName(TimeState time)
    {
        if (time == TimeState.Spring) return "春天";
        if (time == TimeState.War) return "战时";
        if (time == TimeState.Autumn) return "秋天";
        return time.ToString();
    }

    private float TimeToSliderValue(TimeState time)
    {
        if (time == TimeState.Spring) return 0f;
        if (time == TimeState.War) return 1f;
        return 2f;
    }

    private TimeState SliderValueToTime(float value)
    {
        int index = Mathf.RoundToInt(value);
        if (index <= 0) return TimeState.Spring;
        if (index == 1) return TimeState.War;
        return TimeState.Autumn;
    }

    private Color GetTimeHandleColor(TimeState time)
    {
        if (time == TimeState.Spring) return new Color(0.54f, 0.83f, 0.56f, 1f);
        if (time == TimeState.War) return new Color(0.22f, 0.27f, 0.39f, 1f);
        return new Color(0.95f, 0.68f, 0.24f, 1f);
    }

    private string GetCargoName(CartCargo cargo)
    {
        if (cargo == CartCargo.Empty) return "空车";
        if (cargo == CartCargo.Wheat) return "麦子车";
        if (cargo == CartCargo.Flour) return "面粉车";
        if (cargo == CartCargo.Water) return "水车";
        if (cargo == CartCargo.Bread) return "面包车";
        return cargo.ToString();
    }

    private string GetCartDirectionIcon()
    {
        CartDirection direction = GetCartPointDirection();
        if (direction == CartDirection.Left) return "←";
        if (direction == CartDirection.Up) return "↑";
        if (direction == CartDirection.Down) return "↓";
        if (direction == CartDirection.FrontLeft) return "↙";
        if (direction == CartDirection.FrontRight) return "↘";
        if (direction == CartDirection.BackLeft) return "↖";
        if (direction == CartDirection.BackRight) return "↗";
        return "→";
    }

    private Color GetCartColor()
    {
        if (cartCargo == CartCargo.Empty) return new Color(1f, 1f, 1f, 0.95f);
        if (cartCargo == CartCargo.Wheat) return new Color(0.96f, 0.76f, 0.26f, 0.95f);
        if (cartCargo == CartCargo.Flour) return new Color(0.98f, 0.93f, 0.78f, 0.95f);
        if (cartCargo == CartCargo.Water) return new Color(0.52f, 0.75f, 0.95f, 0.95f);
        if (cartCargo == CartCargo.Bread) return new Color(0.93f, 0.54f, 0.18f, 0.95f);
        return Color.white;
    }

    private RectTransform CreatePanel(string name, Transform parent, Color color)
    {
        Image image = CreateImage(name, parent, color);
        return image.rectTransform;
    }

    private Image CreateImage(string name, Transform parent, Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        Image image = obj.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = color.a > 0.01f;
        return image;
    }

    private Text CreateText(string name, Transform parent, string text, float size, FontStyle style, Color color, TextAnchor alignment)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        Text label = obj.AddComponent<Text>();
        label.font = uiFont;
        label.text = text;
        label.fontSize = Mathf.RoundToInt(size);
        label.fontStyle = style;
        label.color = color;
        label.alignment = alignment;
        label.raycastTarget = false;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        return label;
    }

    private void HideTextChildren(Transform target)
    {
        if (target == null)
        {
            return;
        }

        Text[] labels = target.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            if (labels[i] != null)
            {
                labels[i].text = "";
                labels[i].gameObject.SetActive(false);
            }
        }
    }

    private void Anchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private void Place(RectTransform rect, Vector2 normalizedPosition, Vector2 size)
    {
        rect.anchorMin = normalizedPosition;
        rect.anchorMax = normalizedPosition;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
    }

    private void PlaceOnBoard(RectTransform rect, Vector2 normalizedPosition, Vector2 size)
    {
        rect.SetParent(boardOverlay, false);
        Place(rect, normalizedPosition, size);
    }

    private void Stretch(RectTransform rect)
    {
        Stretch(rect, Vector2.zero, Vector2.zero);
    }

    private void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }
}

public sealed class WhiteboxDropZone : MonoBehaviour
{
    public WhiteboxDropZoneType ZoneType { get; private set; }
    public RegionID Region { get; private set; }

    private BorderMillWhiteboxController controller;

    public void Initialize(BorderMillWhiteboxController owner, WhiteboxDropZoneType zoneType, RegionID region)
    {
        controller = owner;
        ZoneType = zoneType;
        Region = region;
    }
}

public sealed class WhiteboxCellarGirlClickTarget : MonoBehaviour, IPointerClickHandler
{
    private BorderMillWhiteboxController controller;

    public void Initialize(BorderMillWhiteboxController owner)
    {
        controller = owner;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (controller != null)
        {
            controller.OnCellarGirlClicked();
        }
    }
}

public sealed class WhiteboxDraggableToken : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public WhiteboxTokenType TokenType { get; private set; }
    public RegionID SourceRegion { get; private set; }
    public bool DestroyOnConsume { get; private set; } = true;

    private BorderMillWhiteboxController controller;
    private CanvasGroup canvasGroup;
    private Transform startParent;
    private Vector2 startAnchoredPosition;
    private Vector2 startAnchorMin;
    private Vector2 startAnchorMax;
    private Vector2 startPivot;
    private Vector2 startSizeDelta;
    private Vector3 startLocalScale;
    private int startSiblingIndex;
    private bool consumed;
    private RectTransform dragTarget;
    private GameObject dragProxy;

    public void Initialize(BorderMillWhiteboxController owner, WhiteboxTokenType tokenType, RegionID sourceRegion)
    {
        Initialize(owner, tokenType, sourceRegion, true);
    }

    public void Initialize(BorderMillWhiteboxController owner, WhiteboxTokenType tokenType, RegionID sourceRegion, bool destroyOnConsume)
    {
        controller = owner;
        TokenType = tokenType;
        SourceRegion = sourceRegion;
        DestroyOnConsume = destroyOnConsume;
        canvasGroup = GetComponent<CanvasGroup>();
    }

    public void MarkConsumed()
    {
        consumed = true;
    }

    public void DisablePersistentToken()
    {
        consumed = true;
        enabled = false;
        DestroyDragProxy();
        RestoreToStartTransform();

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false;
        }
    }

    private void RestoreToStartTransform()
    {
        if (startParent == null)
        {
            return;
        }

        transform.SetParent(startParent, false);
        RectTransform rect = (RectTransform)transform;
        rect.anchorMin = startAnchorMin;
        rect.anchorMax = startAnchorMax;
        rect.pivot = startPivot;
        rect.sizeDelta = startSizeDelta;
        rect.localScale = startLocalScale;
        rect.anchoredPosition = startAnchoredPosition;
        transform.SetSiblingIndex(startSiblingIndex);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        consumed = false;
        DestroyDragProxy();
        startParent = transform.parent;
        RectTransform rect = (RectTransform)transform;
        startAnchoredPosition = rect.anchoredPosition;
        startAnchorMin = rect.anchorMin;
        startAnchorMax = rect.anchorMax;
        startPivot = rect.pivot;
        startSizeDelta = rect.sizeDelta;
        startLocalScale = rect.localScale;
        startSiblingIndex = transform.GetSiblingIndex();

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false;
        }

        if (!DestroyOnConsume && controller.DragLayer != null)
        {
            dragTarget = CreateDragProxy(rect);
            OnDrag(eventData);
            return;
        }

        if (controller.DragLayer != null)
        {
            transform.SetParent(controller.DragLayer, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = startSizeDelta;
            rect.localScale = Vector3.one;
            transform.SetAsLastSibling();
            dragTarget = rect;
            OnDrag(eventData);
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        RectTransform rect = dragTarget != null ? dragTarget : (RectTransform)transform;
        RectTransform parentRect = rect.parent as RectTransform;
        if (parentRect != null)
        {
            Vector2 localPoint;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, eventData.position, eventData.pressEventCamera, out localPoint))
            {
                rect.anchoredPosition = localPoint;
                return;
            }
        }

        rect.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        bool accepted = controller.TryDropToken(this, eventData);

        DestroyDragProxy();
        dragTarget = null;

        if (accepted || consumed)
        {
            return;
        }

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
        }

        RestoreToStartTransform();
    }

    private RectTransform CreateDragProxy(RectTransform source)
    {
        GameObject proxy = new GameObject(name + "_DragProxy", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        proxy.transform.SetParent(controller.DragLayer, false);
        dragProxy = proxy;

        RectTransform proxyRect = proxy.GetComponent<RectTransform>();
        proxyRect.anchorMin = new Vector2(0.5f, 0.5f);
        proxyRect.anchorMax = new Vector2(0.5f, 0.5f);
        proxyRect.pivot = new Vector2(0.5f, 0.5f);
        proxyRect.sizeDelta = source.sizeDelta;
        proxyRect.localScale = Vector3.one;

        bool cartCargoProxy = controller.IsCartCargoVisual(transform);
        Sprite tokenSprite = controller.GetDragTokenSprite(TokenType);

        Image sourceImage = GetComponent<Image>();
        Image proxyImage = proxy.GetComponent<Image>();
        proxyImage.raycastTarget = false;

        
        if (!cartCargoProxy && sourceImage != null && sourceImage.sprite != null)
        {
            proxyImage.sprite = sourceImage.sprite;
            proxyImage.color = sourceImage.color.a <= 0.01f ? Color.white : sourceImage.color;
            proxyImage.preserveAspect = true;
        }
        else if (tokenSprite != null)
        {
            proxyImage.sprite = tokenSprite;
            proxyImage.color = Color.white;
            proxyImage.preserveAspect = true;
        }
        else
        {
            proxyImage.sprite = null;
            proxyImage.color = controller.GetDragTokenFallbackColor(TokenType);
        }

        CanvasGroup proxyGroup = proxy.GetComponent<CanvasGroup>();
        proxyGroup.alpha = 1f;
        proxyGroup.blocksRaycasts = false;
        proxyGroup.interactable = false;

        proxy.transform.SetAsLastSibling();
        return proxyRect;
    }

    private void DestroyDragProxy()
    {
        if (dragProxy != null)
        {
            Destroy(dragProxy);
            dragProxy = null;
        }
    }
    
}
