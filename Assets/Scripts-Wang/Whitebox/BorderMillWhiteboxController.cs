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
    Bread
}

public enum WhiteboxDropZoneType
{
    Cart,
    KitchenDoor,
    CellarEntrance,
    Firebox,
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
        public Image TimeOverlay;
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
        public Button Button;
    }

    private sealed class RoadEdge
    {
        public WhiteboxCartNodeId A;
        public WhiteboxCartNodeId B;
        public RegionID[] RequiredSpringRegions;
    }

    private const string LayoutResourcePath = "Whitebox/border_mill_white_layout";

    private readonly Dictionary<RegionID, TimeState> regionTimes = new Dictionary<RegionID, TimeState>();
    private readonly Dictionary<RegionID, RegionView> regionViews = new Dictionary<RegionID, RegionView>();
    private readonly Dictionary<WhiteboxCartNodeId, CartNode> cartNodes = new Dictionary<WhiteboxCartNodeId, CartNode>();
    private readonly List<RoadEdge> roadEdges = new List<RoadEdge>();
    private readonly List<WhiteboxDraggableToken> activeTokens = new List<WhiteboxDraggableToken>();
    private readonly List<string> logLines = new List<string>();

    [System.Serializable]
    private sealed class RegionBinding
    {
        public RectTransform Root;
        public Image TimeOverlay;
        public Text Title;
        public Text State;
        public Text Hint;
        public RectTransform TokenLayer;
        public Slider TimeSlider;
        public Text TimeSliderLabel;
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

    [Header("Indoor UI")]
    [SerializeField] private RectTransform indoorRoot;
    [SerializeField] private Image indoorBackground;
    [SerializeField] private Text indoorTitle;
    [SerializeField] private Text indoorHint;
    [SerializeField] private Slider indoorTimeSlider;
    [SerializeField] private Text indoorTimeSliderLabel;
    [SerializeField] private RectTransform indoorTokenLayer;
    [SerializeField] private Button closeIndoorButton;
    [SerializeField] private Button indoorPlaceFlourButton;
    [SerializeField] private Button indoorAddWaterButton;
    [SerializeField] private Button indoorKneadDoughButton;
    [SerializeField] private Button indoorLoadOvenButton;
    [SerializeField] private Button indoorMoveBreadToDoorButton;
    [SerializeField] private Button indoorTakeBreadButton;
    [SerializeField] private RectTransform indoorFireboxDropZoneRect;
    [SerializeField] private RectTransform indoorCellarDropZoneRect;

    [Header("HUD / Ending")]
    [SerializeField] private Text statusText;
    [SerializeField] private Text checklistText;
    [SerializeField] private Text logText;
    [SerializeField] private RectTransform finalOverlay;
    [SerializeField] private Text finalText;

    private WhiteboxDropZone cartDropZone;
    private WhiteboxDropZone kitchenDoorDropZone;
    private WhiteboxDropZone cellarEntranceDropZone;
    private WhiteboxDropZone indoorFireboxDropZone;
    private WhiteboxDropZone indoorCellarDropZone;
    private Font uiFont;
    private WhiteboxDraggableToken cartCargoToken;

    private WhiteboxCartNodeId cartNodeId;
    private CartCargo cartCargo;
    private CartDirection cartDirection;
    private WhiteboxIndoorRoom currentIndoorRoom;

    private KitchenProcessState kitchenProcess;
    private bool flourAtKitchenDoor;
    private bool waterAtKitchenDoor;
    private bool breadAtCellarEntrance;
    private bool rocketTriggered;
    private bool ovenLit;
    private bool breadDestroyed;
    private bool girlFed;
    private bool timeLocked;
    private bool cartMoving;

    private readonly Color paper = new Color(0.965f, 0.955f, 0.925f, 1f);
    private readonly Color ink = new Color(0.12f, 0.12f, 0.12f, 1f);
    private readonly Color muted = new Color(0.34f, 0.35f, 0.36f, 1f);
    private readonly Color spring = new Color(0.42f, 0.78f, 0.47f, 0.22f);
    private readonly Color war = new Color(0.1f, 0.14f, 0.26f, 0.28f);
    private readonly Color autumn = new Color(0.95f, 0.66f, 0.18f, 0.26f);
    private readonly Color warm = new Color(1f, 0.42f, 0.12f, 0.34f);

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

        RefreshAll();
        AddLog("初始全区域为春天。按道路点移动，跨格路段需要相关区域保持春天。");
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

        BindButton(harvestWheatButton, SpawnWheatToken);
        BindButton(openKitchenButton, OpenKitchen);
        BindButton(collectWaterButton, CollectWater);
        BindButton(openCellarButton, OpenCellar);
        BindButton(closeIndoorButton, CloseIndoor);
        BindButton(indoorPlaceFlourButton, PlaceFlourOnBoard);
        BindButton(indoorAddWaterButton, AddWaterToFlour);
        BindButton(indoorKneadDoughButton, KneadDough);
        BindButton(indoorLoadOvenButton, LoadDoughIntoOven);
        BindButton(indoorMoveBreadToDoorButton, MoveBreadToKitchenDoor);
        BindButton(indoorTakeBreadButton, delegate { AddLog("面包已在地窖入口，切到战时后拖给小女孩。"); });

        kitchenDoorDropZone = SetupDropZone(kitchenDoorDropZoneRect, WhiteboxDropZoneType.KitchenDoor, RegionID.Kitchen);
        cellarEntranceDropZone = SetupDropZone(cellarEntranceDropZoneRect, WhiteboxDropZoneType.CellarEntrance, RegionID.Cellar);
        indoorFireboxDropZone = SetupDropZone(indoorFireboxDropZoneRect, WhiteboxDropZoneType.Firebox, RegionID.Kitchen);
        indoorCellarDropZone = SetupDropZone(indoorCellarDropZoneRect, WhiteboxDropZoneType.CellarDelivery, RegionID.Cellar);

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
        view.TimeOverlay = binding.TimeOverlay;
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

    private void BindButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    private WhiteboxDropZone SetupDropZone(RectTransform rect, WhiteboxDropZoneType zoneType, RegionID region)
    {
        if (rect == null)
        {
            return null;
        }

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
        cartCargo = CartCargo.Empty;
        cartDirection = CartDirection.Right;
        currentIndoorRoom = WhiteboxIndoorRoom.None;

        kitchenProcess = KitchenProcessState.Empty;
        flourAtKitchenDoor = false;
        waterAtKitchenDoor = false;
        breadAtCellarEntrance = false;
        rocketTriggered = false;
        ovenLit = false;
        breadDestroyed = false;
        girlFed = false;
        timeLocked = false;
        cartMoving = false;

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

    private void BuildInterface()
    {
        GameObject canvasObject = new GameObject("Border Mill Image Whitebox Canvas");
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();

        root = canvasObject.GetComponent<RectTransform>();
        Stretch(root);

        Image background = CreateImage("PaperBackground", root, paper);
        Stretch(background.rectTransform);

        BuildSceneBoard(root);
        BuildInSceneHud(sceneBoard);
        BuildIndoorOverlay(sceneBoard);
        BuildFinalOverlay(root);

        dragLayer = CreatePanel("DragLayer", root, Color.clear);
        Stretch(dragLayer);
        dragLayer.SetAsLastSibling();
    }

    private void BuildSceneBoard(RectTransform parent)
    {
        GameObject boardObject = new GameObject("WhiteDrawingBoard");
        boardObject.transform.SetParent(parent, false);
        sceneBoard = boardObject.AddComponent<RectTransform>();
        sceneBoard.anchorMin = new Vector2(0.5f, 0.5f);
        sceneBoard.anchorMax = new Vector2(0.5f, 0.5f);
        sceneBoard.pivot = new Vector2(0.5f, 0.5f);
        sceneBoard.anchoredPosition = Vector2.zero;
        sceneBoard.sizeDelta = new Vector2(1080f, 1080f);

        RawImage drawing = boardObject.AddComponent<RawImage>();
        drawing.color = Color.white;
        drawing.texture = Resources.Load<Texture2D>(LayoutResourcePath);
        drawing.raycastTarget = false;

        boardOverlay = CreatePanel("BoardOverlay", sceneBoard, Color.clear);
        Stretch(boardOverlay);

        CreateRegion(RegionID.Mill, "A-1 磨坊", new Vector2(0f, 0.5f), new Vector2(0.5f, 1f));
        CreateRegion(RegionID.Field, "A-2 麦田", new Vector2(0.5f, 0.5f), new Vector2(1f, 1f));
        CreateRegion(RegionID.Kitchen, "A-3 厨房", new Vector2(0f, 0f), new Vector2(0.5f, 0.5f));
        CreateRegion(RegionID.Cellar, "A-4 地窖", new Vector2(0.5f, 0f), new Vector2(1f, 0.5f));

        BuildRoadGraph();
        BuildRoadNodeButtons();
        BuildEntranceDropZones();
        BuildCartVisual();
    }

    private void CreateRegion(RegionID region, string title, Vector2 min, Vector2 max)
    {
        RectTransform regionRoot = CreatePanel(title, boardOverlay, Color.clear);
        Anchor(regionRoot, min, max, Vector2.zero, Vector2.zero);
        regionRoot.GetComponent<Image>().raycastTarget = false;

        RegionView view = new RegionView();
        view.Region = region;
        view.Root = regionRoot;
        view.TimeOverlay = CreateImage("TimeOverlay", regionRoot, Color.clear);
        Stretch(view.TimeOverlay.rectTransform);
        view.TimeOverlay.raycastTarget = false;

        view.Title = CreateText("Title", regionRoot, title, 19, FontStyle.Bold, ink, TextAnchor.UpperLeft);
        Anchor(view.Title.rectTransform, new Vector2(0.035f, 0.89f), new Vector2(0.42f, 0.975f), Vector2.zero, Vector2.zero);

        view.State = CreateText("State", regionRoot, "", 17, FontStyle.Bold, ink, TextAnchor.UpperRight);
        Anchor(view.State.rectTransform, new Vector2(0.6f, 0.9f), new Vector2(0.965f, 0.975f), Vector2.zero, Vector2.zero);

        RectTransform timeRow = CreatePanel("TimeBar", regionRoot, new Color(1f, 1f, 1f, 0.72f));
        Anchor(timeRow, new Vector2(0.035f, 0.815f), new Vector2(0.41f, 0.875f), Vector2.zero, Vector2.zero);

        view.TimeSlider = CreateTimeSlider("TimeSlider", timeRow, delegate(float value)
        {
            SetRegionTime(region, SliderValueToTime(value));
        });
        Anchor(view.TimeSlider.GetComponent<RectTransform>(), new Vector2(0.08f, 0.38f), new Vector2(0.92f, 0.88f), Vector2.zero, Vector2.zero);

        view.TimeSliderLabel = CreateText("TimeSliderLabel", timeRow, "春        战        秋", 11, FontStyle.Bold, muted, TextAnchor.LowerCenter);
        Anchor(view.TimeSliderLabel.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.42f), Vector2.zero, Vector2.zero);

        RectTransform hintBox = CreatePanel("HintBox", regionRoot, new Color(1f, 1f, 1f, 0.72f));
        Anchor(hintBox, new Vector2(0.035f, 0.025f), new Vector2(0.965f, 0.13f), Vector2.zero, Vector2.zero);
        view.Hint = CreateText("Hint", hintBox, "", 14, FontStyle.Normal, muted, TextAnchor.MiddleLeft);
        Stretch(view.Hint.rectTransform, new Vector2(8f, 2f), new Vector2(-8f, -2f));
        view.Hint.horizontalOverflow = HorizontalWrapMode.Wrap;

        view.TokenLayer = CreatePanel("TokenLayer", regionRoot, Color.clear);
        Stretch(view.TokenLayer);
        view.TokenLayer.GetComponent<Image>().raycastTarget = false;

        BuildRegionActions(view);
        regionViews.Add(region, view);
    }

    private void BuildRegionActions(RegionView view)
    {
        if (view.Region == RegionID.Mill)
        {
            return;
        }
        else if (view.Region == RegionID.Field)
        {
            view.ActionButtons.Add(CreateActionButton(view.Root, "收割麦子", new Vector2(0.58f, 0.42f), new Vector2(126f, 40f), SpawnWheatToken));
        }
        else if (view.Region == RegionID.Kitchen)
        {
            view.ActionButtons.Add(CreateActionButton(view.Root, "进厨房", new Vector2(0.75f, 0.42f), new Vector2(100f, 40f), OpenKitchen));
        }
        else if (view.Region == RegionID.Cellar)
        {
            view.ActionButtons.Add(CreateActionButton(view.Root, "取清水", new Vector2(0.66f, 0.54f), new Vector2(100f, 40f), CollectWater));
            view.ActionButtons.Add(CreateActionButton(view.Root, "进地窖", new Vector2(0.45f, 0.38f), new Vector2(100f, 40f), OpenCellar));
        }
    }

    private Button CreateActionButton(RectTransform parent, string label, Vector2 normalizedPosition, Vector2 size, UnityEngine.Events.UnityAction action)
    {
        Button button = CreateButton(label, parent, label, 14, action);
        Place(button.GetComponent<RectTransform>(), normalizedPosition, size);
        return button;
    }

    private void BuildRoadGraph()
    {
        cartNodes.Clear();
        roadEdges.Clear();

        AddCartNode(WhiteboxCartNodeId.MillRoad, "磨坊路口", RegionID.Mill, new Vector2(0.35f, 0.63f));
        AddCartNode(WhiteboxCartNodeId.MillStone, "风车石磨", RegionID.Mill, new Vector2(0.22f, 0.66f));
        AddCartNode(WhiteboxCartNodeId.FieldRoad, "麦田路口", RegionID.Field, new Vector2(0.64f, 0.62f));
        AddCartNode(WhiteboxCartNodeId.FieldHarvest, "可收割麦田", RegionID.Field, new Vector2(0.79f, 0.66f));
        AddCartNode(WhiteboxCartNodeId.KitchenDoor, "厨房门口", RegionID.Kitchen, new Vector2(0.31f, 0.23f));
        AddCartNode(WhiteboxCartNodeId.CellarWell, "地窖外路点", RegionID.Cellar, new Vector2(0.73f, 0.42f));
        AddCartNode(WhiteboxCartNodeId.CellarDoor, "地窖入口", RegionID.Cellar, new Vector2(0.61f, 0.31f));

        AddRoadEdge(WhiteboxCartNodeId.MillRoad, WhiteboxCartNodeId.MillStone, RegionID.Mill);
        AddRoadEdge(WhiteboxCartNodeId.MillRoad, WhiteboxCartNodeId.FieldRoad, RegionID.Mill, RegionID.Field);
        AddRoadEdge(WhiteboxCartNodeId.FieldRoad, WhiteboxCartNodeId.FieldHarvest, RegionID.Field);
        AddRoadEdge(WhiteboxCartNodeId.MillRoad, WhiteboxCartNodeId.KitchenDoor, RegionID.Mill, RegionID.Kitchen);
        AddRoadEdge(WhiteboxCartNodeId.FieldRoad, WhiteboxCartNodeId.CellarWell, RegionID.Field, RegionID.Cellar);
        AddRoadEdge(WhiteboxCartNodeId.CellarWell, WhiteboxCartNodeId.CellarDoor, RegionID.Cellar);
        AddRoadEdge(WhiteboxCartNodeId.KitchenDoor, WhiteboxCartNodeId.CellarDoor, RegionID.Kitchen, RegionID.Cellar);
    }

    private void AddCartNode(WhiteboxCartNodeId id, string name, RegionID region, Vector2 boardPosition)
    {
        CartNode node = new CartNode();
        node.Id = id;
        node.Name = name;
        node.Region = region;
        node.BoardPosition = boardPosition;
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

    private void BuildRoadNodeButtons()
    {
        foreach (CartNode node in cartNodes.Values)
        {
            Button button = CreateButton("Point_" + node.Id, boardOverlay, "●\n" + node.Name, 12, delegate { TryMoveCartToNode(node.Id); });
            node.Button = button;
            PlaceOnBoard(button.GetComponent<RectTransform>(), node.BoardPosition, new Vector2(84f, 48f));
            Image image = button.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.62f);
        }
    }

    private void BuildEntranceDropZones()
    {
        kitchenDoorDropZone = CreateDropZone(boardOverlay, WhiteboxDropZoneType.KitchenDoor, RegionID.Kitchen, "厨房门口\n投放");
        PlaceOnBoard(kitchenDoorDropZone.GetComponent<RectTransform>(), new Vector2(0.37f, 0.26f), new Vector2(118f, 58f));

        cellarEntranceDropZone = CreateDropZone(boardOverlay, WhiteboxDropZoneType.CellarEntrance, RegionID.Cellar, "地窖入口\n投放");
        PlaceOnBoard(cellarEntranceDropZone.GetComponent<RectTransform>(), new Vector2(0.58f, 0.31f), new Vector2(124f, 62f));

        foreach (CartNode node in cartNodes.Values)
        {
            node.Button.transform.SetAsLastSibling();
        }
    }

    private void BuildCartVisual()
    {
        cartVisual = CreatePanel("Cart", boardOverlay, new Color(1f, 1f, 1f, 0.92f));
        cartVisual.sizeDelta = new Vector2(100f, 62f);

        cartImage = cartVisual.GetComponent<Image>();
        cartDropZone = cartVisual.gameObject.AddComponent<WhiteboxDropZone>();
        cartDropZone.Initialize(this, WhiteboxDropZoneType.Cart, GetCartRegion());

        cartText = CreateText("CartText", cartVisual, "", 14, FontStyle.Bold, ink, TextAnchor.MiddleCenter);
        Stretch(cartText.rectTransform, new Vector2(4f, 2f), new Vector2(-4f, -2f));
    }

    private void BuildInSceneHud(RectTransform parent)
    {
        RectTransform hud = CreatePanel("InSceneHud", parent, new Color(1f, 1f, 1f, 0.78f));
        Anchor(hud, new Vector2(0.14f, 0.01f), new Vector2(0.86f, 0.078f), Vector2.zero, Vector2.zero);

        statusText = CreateText("Status", hud, "", 14, FontStyle.Bold, ink, TextAnchor.UpperLeft);
        Anchor(statusText.rectTransform, new Vector2(0.025f, 0.5f), new Vector2(0.975f, 0.95f), Vector2.zero, Vector2.zero);
        statusText.horizontalOverflow = HorizontalWrapMode.Wrap;

        checklistText = CreateText("Checklist", hud, "", 13, FontStyle.Normal, ink, TextAnchor.UpperLeft);
        Anchor(checklistText.rectTransform, new Vector2(0.025f, 0.22f), new Vector2(0.975f, 0.55f), Vector2.zero, Vector2.zero);
        checklistText.horizontalOverflow = HorizontalWrapMode.Wrap;

        logText = CreateText("Log", hud, "", 13, FontStyle.Normal, muted, TextAnchor.LowerLeft);
        Anchor(logText.rectTransform, new Vector2(0.025f, 0.02f), new Vector2(0.975f, 0.3f), Vector2.zero, Vector2.zero);
        logText.horizontalOverflow = HorizontalWrapMode.Wrap;
    }

    private void BuildIndoorOverlay(RectTransform parent)
    {
        indoorRoot = CreatePanel("IndoorOverlay", parent, new Color(0f, 0f, 0f, 0.76f));
        Anchor(indoorRoot, new Vector2(0.12f, 0.12f), new Vector2(0.88f, 0.88f), Vector2.zero, Vector2.zero);
        indoorRoot.gameObject.SetActive(false);

        indoorBackground = CreateImage("InteriorBackground", indoorRoot, new Color(0.82f, 0.82f, 0.78f, 1f));
        Stretch(indoorBackground.rectTransform, new Vector2(14f, 14f), new Vector2(-14f, -14f));

        indoorTitle = CreateText("IndoorTitle", indoorRoot, "", 28, FontStyle.Bold, ink, TextAnchor.UpperLeft);
        Anchor(indoorTitle.rectTransform, new Vector2(0.05f, 0.86f), new Vector2(0.62f, 0.96f), Vector2.zero, Vector2.zero);

        indoorHint = CreateText("IndoorHint", indoorRoot, "", 18, FontStyle.Normal, muted, TextAnchor.UpperLeft);
        Anchor(indoorHint.rectTransform, new Vector2(0.05f, 0.75f), new Vector2(0.95f, 0.85f), Vector2.zero, Vector2.zero);
        indoorHint.horizontalOverflow = HorizontalWrapMode.Wrap;

        RectTransform timeRow = CreatePanel("IndoorTimeBar", indoorRoot, new Color(1f, 1f, 1f, 0.72f));
        Anchor(timeRow, new Vector2(0.62f, 0.88f), new Vector2(0.86f, 0.95f), Vector2.zero, Vector2.zero);

        indoorTimeSlider = CreateTimeSlider("IndoorTimeSlider", timeRow, delegate(float value)
        {
            SetIndoorTime(SliderValueToTime(value));
        });
        Anchor(indoorTimeSlider.GetComponent<RectTransform>(), new Vector2(0.08f, 0.38f), new Vector2(0.92f, 0.88f), Vector2.zero, Vector2.zero);

        indoorTimeSliderLabel = CreateText("IndoorTimeSliderLabel", timeRow, "春        战        秋", 11, FontStyle.Bold, muted, TextAnchor.LowerCenter);
        Anchor(indoorTimeSliderLabel.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.42f), Vector2.zero, Vector2.zero);

        Button close = CreateButton("CloseIndoor", indoorRoot, "返回", 16, CloseIndoor);
        Anchor(close.GetComponent<RectTransform>(), new Vector2(0.87f, 0.885f), new Vector2(0.95f, 0.95f), Vector2.zero, Vector2.zero);

        indoorFireboxDropZone = CreateDropZone(indoorRoot, WhiteboxDropZoneType.Firebox, RegionID.Kitchen, "炉膛\n拖入火箭");
        Place(indoorFireboxDropZone.GetComponent<RectTransform>(), new Vector2(0.72f, 0.32f), new Vector2(140f, 72f));

        indoorCellarDropZone = CreateDropZone(indoorRoot, WhiteboxDropZoneType.CellarDelivery, RegionID.Cellar, "小女孩\n交付面包");
        Place(indoorCellarDropZone.GetComponent<RectTransform>(), new Vector2(0.62f, 0.36f), new Vector2(150f, 78f));

        indoorPlaceFlourButton = CreateButton("IndoorPlaceFlour", indoorRoot, "案板放面粉", 15, PlaceFlourOnBoard);
        Place(indoorPlaceFlourButton.GetComponent<RectTransform>(), new Vector2(0.18f, 0.44f), new Vector2(128f, 42f));

        indoorAddWaterButton = CreateButton("IndoorAddWater", indoorRoot, "加入清水", 15, AddWaterToFlour);
        Place(indoorAddWaterButton.GetComponent<RectTransform>(), new Vector2(0.36f, 0.44f), new Vector2(118f, 42f));

        indoorKneadDoughButton = CreateButton("IndoorKneadDough", indoorRoot, "揉成面团", 15, KneadDough);
        Place(indoorKneadDoughButton.GetComponent<RectTransform>(), new Vector2(0.54f, 0.44f), new Vector2(118f, 42f));

        indoorLoadOvenButton = CreateButton("IndoorLoadOven", indoorRoot, "放入烤箱", 15, LoadDoughIntoOven);
        Place(indoorLoadOvenButton.GetComponent<RectTransform>(), new Vector2(0.72f, 0.44f), new Vector2(118f, 42f));

        indoorMoveBreadToDoorButton = CreateButton("IndoorMoveBreadDoor", indoorRoot, "面包移到门口", 15, MoveBreadToKitchenDoor);
        Place(indoorMoveBreadToDoorButton.GetComponent<RectTransform>(), new Vector2(0.54f, 0.34f), new Vector2(150f, 42f));

        indoorTakeBreadButton = CreateButton("TakeBreadIntoCellar", indoorRoot, "面包已在入口", 16, delegate { AddLog("面包已在地窖入口，切到战时后拖给小女孩。"); });
        Place(indoorTakeBreadButton.GetComponent<RectTransform>(), new Vector2(0.34f, 0.36f), new Vector2(150f, 44f));

        indoorTokenLayer = CreatePanel("IndoorTokenLayer", indoorRoot, Color.clear);
        Stretch(indoorTokenLayer);
        indoorTokenLayer.GetComponent<Image>().raycastTarget = false;
    }

    private void BuildFinalOverlay(RectTransform parent)
    {
        finalOverlay = CreatePanel("FinalOverlay", parent, new Color(0f, 0f, 0f, 0.84f));
        Stretch(finalOverlay);
        finalOverlay.gameObject.SetActive(false);

        finalText = CreateText("FinalText", finalOverlay, "", 34, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        Anchor(finalText.rectTransform, new Vector2(0.16f, 0.34f), new Vector2(0.84f, 0.66f), Vector2.zero, Vector2.zero);
        finalText.horizontalOverflow = HorizontalWrapMode.Wrap;
    }

    private void SetRegionTime(RegionID region, TimeState time)
    {
        if (timeLocked)
        {
            AddLog("结局后时间控制权被收回。");
            RefreshAll();
            return;
        }

        if (regionTimes[region] == time)
        {
            RefreshAll();
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
    }

    private void ApplyKitchenTimeConsequence(RegionID region, TimeState time)
    {
        if (region != RegionID.Kitchen)
        {
            return;
        }

        if (kitchenProcess != KitchenProcessState.DoughMixed && kitchenProcess != KitchenProcessState.DoughFermented)
        {
            return;
        }

        if (time == TimeState.War && kitchenProcess == KitchenProcessState.DoughMixed)
        {
            kitchenProcess = KitchenProcessState.DoughFermented;
            AddLog("面团在战时完成发酵，可以放入烤箱。");
        }
        else if (time == TimeState.Autumn)
        {
            kitchenProcess = KitchenProcessState.DoughMolded;
            AddLog("面团在战后秋天发霉，无法使用，需要重新准备。");
        }
    }

    private void SetIndoorTime(TimeState time)
    {
        RegionID region = currentIndoorRoom == WhiteboxIndoorRoom.Kitchen ? RegionID.Kitchen : RegionID.Cellar;
        SetRegionTime(region, time);
    }

    private void TryMoveCartToNode(WhiteboxCartNodeId target)
    {
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

            if (cartCargo == CartCargo.Bread && structuralPath != null)
            {
                DestroyBreadOnRoad(reason + " 面包运输失败。");
                return;
            }

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
                        PlaceOnBoard(cartVisual, position, new Vector2(104f, 64f));
                    }
                    else
                    {
                        Place(cartVisual, position, new Vector2(104f, 64f));
                    }
                }

                yield return null;
            }

            cartNodeId = to;
            MaybeAutoMillWheat();
            RefreshCart();
        }

        cartMoving = false;
        AddLog("小推车到达" + cartNodes[cartNodeId].Name + "。");
        RefreshAll();
    }


    private void UpdateCartDirection(WhiteboxCartNodeId from, WhiteboxCartNodeId to)
    {
        Vector2 delta = cartNodes[to].BoardPosition - cartNodes[from].BoardPosition;
        if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
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
        if (edge.RequiredSpringRegions.Length <= 1)
        {
            return true;
        }

        for (int i = 0; i < edge.RequiredSpringRegions.Length; i++)
        {
            if (regionTimes[edge.RequiredSpringRegions[i]] != TimeState.Spring)
            {
                return false;
            }
        }

        return true;
    }

    private string GetBlockedReason(RoadEdge edge)
    {
        List<string> blocked = new List<string>();
        for (int i = 0; i < edge.RequiredSpringRegions.Length; i++)
        {
            RegionID region = edge.RequiredSpringRegions[i];
            if (regionTimes[region] != TimeState.Spring)
            {
                blocked.Add(GetRegionName(region) + "=" + GetTimeName(regionTimes[region]));
            }
        }

        return "路段 " + cartNodes[edge.A].Name + " → " + cartNodes[edge.B].Name + " 需要相关区域都是春天；当前 " + string.Join("、", blocked.ToArray()) + "。";
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

        if (FindToken(WhiteboxTokenType.Wheat) != null)
        {
            AddLog("麦子已经在图上，拖到小推车即可。");
            return;
        }

        SpawnToken(WhiteboxTokenType.Wheat, RegionID.Field, regionViews[RegionID.Field].TokenLayer, "麦子\n拖到推车", new Color(0.95f, 0.74f, 0.25f, 0.96f), new Vector2(0.58f, 0.48f));
        AddLog("麦子生成在右上秋天麦田。");
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
        if (cartNodeId != WhiteboxCartNodeId.CellarDoor)
        {
            AddLog("先把小推车推到“地窖入口”点；秋天下雨，入口处才有积水。");
            return;
        }

        if (regionTimes[RegionID.Cellar] != TimeState.Autumn)
        {
            AddLog("干净积水只在右下战后秋天出现。");
            return;
        }

        if (cartCargo != CartCargo.Empty)
        {
            AddLog("小推车已有货物。");
            return;
        }

        cartCargo = CartCargo.Water;
        AddLog("从地窖入口的干净积水取水。小推车切换为带水状态。");
        RefreshAll();
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
        AddLog("清水加入面粉。下一步揉面。");
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

        if (kitchenProcess == KitchenProcessState.DoughMixed || kitchenProcess == KitchenProcessState.DoughFermented)
        {
            AddLog("生面团已经揉好。切到战时让它发酵。");
            return;
        }

        if (kitchenProcess != KitchenProcessState.WaterAdded)
        {
            AddLog("顺序不对：需要先放面粉，再加入清水。");
            return;
        }

        kitchenProcess = KitchenProcessState.DoughMixed;
        AddLog("面粉和水揉成生面团。切到战时完成发酵；切到秋天会发霉。");
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
            AddLog("面团已经发霉，不能放入烤箱。请重新准备材料。");
            return;
        }

        if (kitchenProcess == KitchenProcessState.Empty || kitchenProcess == KitchenProcessState.FlourPlaced || kitchenProcess == KitchenProcessState.WaterAdded)
        {
            AddLog("还没有揉出面团。");
            return;
        }

        if (kitchenProcess == KitchenProcessState.DoughMixed)
        {
            AddLog("面团还没发酵。把厨房切到战时完成发酵。");
            return;
        }

        if (kitchenProcess == KitchenProcessState.DoughInOven || kitchenProcess == KitchenProcessState.Baking)
        {
            AddLog("面团已经在烤箱里。");
            return;
        }

        if (kitchenProcess != KitchenProcessState.DoughFermented)
        {
            AddLog("当前状态不能放入烤箱。");
            return;
        }

        kitchenProcess = KitchenProcessState.DoughInOven;
        AddLog(ovenLit ? "发酵面团放入已点燃烤箱，开始烘烤。" : "发酵面团放入烤箱。烤炉点燃后会开始烘烤。");
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
        indoorRoot.gameObject.SetActive(true);
        indoorRoot.SetAsLastSibling();
        CheckRocketEvent();
        RefreshAll();
    }

    private void CloseIndoor()
    {
        currentIndoorRoom = WhiteboxIndoorRoom.None;
        indoorRoot.gameObject.SetActive(false);
        RefreshAll();
    }

    private void CheckRocketEvent()
    {
        if (rocketTriggered || ovenLit)
        {
            return;
        }

        if (regionTimes[RegionID.Field] == TimeState.War &&
            regionTimes[RegionID.Mill] == TimeState.Spring &&
            regionTimes[RegionID.Kitchen] == TimeState.War)
        {
            rocketTriggered = true;
            AddLog("火箭事件触发：战时火箭被春天强风带偏，钉在厨房窗框上。");

            if (currentIndoorRoom == WhiteboxIndoorRoom.Kitchen)
            {
                SpawnArrowInKitchenIfNeeded();
            }
        }
    }

    private void SpawnArrowInKitchenIfNeeded()
    {
        if (!rocketTriggered || ovenLit || FindToken(WhiteboxTokenType.Arrow) != null)
        {
            return;
        }

        if (indoorTokenLayer != null)
        {
            SpawnToken(WhiteboxTokenType.Arrow, RegionID.Kitchen, indoorTokenLayer, "窗上火箭\n拖到炉膛", new Color(1f, 0.3f, 0.12f, 0.96f), new Vector2(0.34f, 0.64f));
        }
    }


    private void SpawnBreadInCellarIfNeeded()
    {
        if (!breadAtCellarEntrance || FindToken(WhiteboxTokenType.Bread) != null)
        {
            return;
        }

        if (indoorTokenLayer != null)
        {
            SpawnToken(WhiteboxTokenType.Bread, RegionID.Cellar, indoorTokenLayer, "入口面包\n拖给女孩", new Color(0.92f, 0.52f, 0.18f, 0.96f), new Vector2(0.36f, 0.48f));
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

        kitchenProcess = KitchenProcessState.Baking;
        AddLog(wasAlreadyLit ? "炉膛已经点燃，面团开始烘烤。" : "火箭点燃炉膛，面包开始烘烤。");
        StartCoroutine(BakeRoutine());
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
    }

    private void MoveBreadToKitchenDoor()
    {
        if (currentIndoorRoom != WhiteboxIndoorRoom.Kitchen)
        {
            AddLog("需要在厨房内部把面包移到门口。");
            return;
        }

        if (kitchenProcess == KitchenProcessState.BreadAtDoor)
        {
            AddLog("面包已经在厨房门口。");
            return;
        }

        if (kitchenProcess != KitchenProcessState.BreadReady)
        {
            AddLog("面包还没有出炉。");
            return;
        }

        kitchenProcess = KitchenProcessState.BreadAtDoor;
        SpawnBreadTokenIfMissing(RegionID.Kitchen, regionViews[RegionID.Kitchen].TokenLayer, new Vector2(0.52f, 0.36f), "门口面包\n拖到推车");
        AddLog("面包移到厨房门口。把小推车推到门口后拖上车。");
        RefreshAll();
    }

    public bool TryDropToken(WhiteboxDraggableToken token, PointerEventData eventData)
    {
        if (token == null)
        {
            return false;
        }

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        for (int i = 0; i < results.Count; i++)
        {
            WhiteboxDropZone zone = results[i].gameObject.GetComponentInParent<WhiteboxDropZone>();
            if (zone != null)
            {
                return HandleTokenDrop(token, zone);
            }
        }

        AddLog("没有放到有效投放区。");
        return false;
    }

    private bool HandleTokenDrop(WhiteboxDraggableToken token, WhiteboxDropZone zone)
    {
        if (zone.ZoneType == WhiteboxDropZoneType.Cart)
        {
            return DropTokenToCart(token);
        }

        if (zone.ZoneType == WhiteboxDropZoneType.KitchenDoor)
        {
            return DropTokenToKitchenDoor(token);
        }

        if (zone.ZoneType == WhiteboxDropZoneType.CellarEntrance)
        {
            return DropTokenToCellarEntrance(token);
        }

        if (zone.ZoneType == WhiteboxDropZoneType.Firebox)
        {
            return DropTokenToFirebox(token);
        }

        if (zone.ZoneType == WhiteboxDropZoneType.CellarDelivery)
        {
            return DropTokenToCellar(token);
        }

        return false;
    }

    private bool DropTokenToCart(WhiteboxDraggableToken token)
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
            if (cartNodeId != WhiteboxCartNodeId.KitchenDoor || token.SourceRegion != RegionID.Kitchen || kitchenProcess != KitchenProcessState.BreadAtDoor)
            {
                AddLog("面包需要先从厨房内部移到门口，再在厨房门口装车。");
                return false;
            }

            cartCargo = CartCargo.Bread;
            kitchenProcess = KitchenProcessState.Empty;
            ConsumeToken(token);
            AddLog("面包装车。小推车切换为托盘/面包状态。");
            RefreshAll();
            return true;
        }

        AddLog("这个物品不能装车。");
        return false;
    }

    private bool DropTokenToKitchenDoor(WhiteboxDraggableToken token)
    {
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

        AddLog("厨房门口只接收面粉和清水。");
        return false;
    }

    private bool DropTokenToCellarEntrance(WhiteboxDraggableToken token)
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

    private bool DropTokenToFirebox(WhiteboxDraggableToken token)
    {
        if (currentIndoorRoom != WhiteboxIndoorRoom.Kitchen)
        {
            AddLog("需要进入厨房内部，把火箭拖到炉膛。");
            return false;
        }

        if (token.TokenType != WhiteboxTokenType.Arrow)
        {
            AddLog("炉膛需要燃烧火箭。");
            return false;
        }

        if (regionTimes[RegionID.Kitchen] != TimeState.Spring)
        {
            AddLog("取到战时火箭后，要把厨房切回春天才能安全点燃炉膛。");
            return false;
        }

        ConsumeToken(token);
        StartBaking();
        return true;
    }

    private bool DropTokenToCellar(WhiteboxDraggableToken token)
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

        if (token.TokenType != WhiteboxTokenType.Bread || token.SourceRegion != RegionID.Cellar)
        {
            AddLog("需要把带入地窖的热面包拖给小女孩。");
            return false;
        }

        ConsumeToken(token);
        breadAtCellarEntrance = false;
        TriggerEnding();
        return true;
    }

    private void TriggerEnding()
    {
        girlFed = true;
        timeLocked = true;
        AddLog("面包送达，时间控制权消失。");
        RefreshAll();

        if (finalOverlay != null)
        {
            finalOverlay.gameObject.SetActive(true);
            finalOverlay.SetAsLastSibling();
        }

        if (finalText != null)
        {
            finalText.text = "小女孩双手捧住热面包，咬下一口。\n\n外面的战争没有停止，但这一个夜晚被留下了。";
        }
    }


    private void ResetPrototype()
    {
        StopAllCoroutines();

        for (int i = activeTokens.Count - 1; i >= 0; i--)
        {
            if (activeTokens[i] != null)
            {
                Destroy(activeTokens[i].gameObject);
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

        Text text = CreateText("Label", tokenRect, label, 13, FontStyle.Bold, ink, TextAnchor.MiddleCenter);
        Stretch(text.rectTransform, new Vector2(4f, 2f), new Vector2(-4f, -2f));

        CanvasGroup group = tokenRect.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = true;

        WhiteboxDraggableToken token = tokenRect.gameObject.AddComponent<WhiteboxDraggableToken>();
        token.Initialize(this, tokenType, sourceRegion);

        activeTokens.Add(token);
        return token;
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

    private void ConsumeToken(WhiteboxDraggableToken token)
    {
        activeTokens.Remove(token);
        if (token == cartCargoToken)
        {
            cartCargoToken = null;
        }
        token.MarkConsumed();
        Destroy(token.gameObject);
    }

    private void RefreshAll()
    {
        foreach (RegionView view in regionViews.Values)
        {
            RefreshRegion(view);
        }

        RefreshCart();
        RefreshRoadNodes();
        RefreshHud();
        RefreshActionButtons();
        RefreshIndoor();
    }

    private void RefreshRegion(RegionView view)
    {
        TimeState time = regionTimes[view.Region];

        if (view.TimeOverlay != null)
        {
            view.TimeOverlay.color = GetTimeOverlay(view.Region, time);
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


    private Color GetTimeOverlay(RegionID region, TimeState time)
    {
        if (region == RegionID.Kitchen && ovenLit)
        {
            return warm;
        }

        if (region == RegionID.Cellar && girlFed)
        {
            return new Color(1f, 0.48f, 0.18f, 0.28f);
        }

        if (time == TimeState.Spring) return spring;
        if (time == TimeState.War) return war;
        return autumn;
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
        if (cartDropZone != null)
        {
            cartDropZone.Initialize(this, WhiteboxDropZoneType.Cart, GetCartRegion());
        }

        if (cartVisual != null)
        {
            if (boardOverlay != null)
            {
                PlaceOnBoard(cartVisual, cartNodes[cartNodeId].BoardPosition, new Vector2(104f, 64f));
            }
            else
            {
                Place(cartVisual, cartNodes[cartNodeId].BoardPosition, new Vector2(104f, 64f));
            }

            cartVisual.SetAsLastSibling();
        }

        if (cartImage != null)
        {
            cartImage.color = GetCartColor();
        }

        if (cartText != null)
        {
            cartText.text = GetCartDirectionIcon() + " 小推车\n" + GetCargoName(cartCargo);
        }

        SyncCartCargoToken();
    }


    private void SyncCartCargoToken()
    {
        WhiteboxTokenType expectedType;
        string label;

        if (cartVisual == null || !TryGetCartCargoTokenInfo(out expectedType, out label))
        {
            RemoveCartCargoToken();
            return;
        }

        if (cartCargoToken != null && cartCargoToken.TokenType == expectedType)
        {
            if (cartCargoToken.transform.parent != cartVisual)
            {
                cartCargoToken.transform.SetParent(cartVisual, false);
            }

            Place((RectTransform)cartCargoToken.transform, new Vector2(0.5f, 0.5f), new Vector2(84f, 42f));
            return;
        }

        RemoveCartCargoToken();
        cartCargoToken = SpawnToken(expectedType, GetCartRegion(), cartVisual, label, GetCartColor(), new Vector2(0.5f, 0.5f));
        Place((RectTransform)cartCargoToken.transform, new Vector2(0.5f, 0.5f), new Vector2(84f, 42f));
    }

    private bool TryGetCartCargoTokenInfo(out WhiteboxTokenType tokenType, out string label)
    {
        tokenType = WhiteboxTokenType.Wheat;
        label = "";

        if (cartCargo == CartCargo.Flour)
        {
            tokenType = WhiteboxTokenType.Flour;
            label = "面粉\n拖到门口";
            return true;
        }

        if (cartCargo == CartCargo.Water)
        {
            tokenType = WhiteboxTokenType.Water;
            label = "清水\n拖到门口";
            return true;
        }

        if (cartCargo == CartCargo.Bread)
        {
            tokenType = WhiteboxTokenType.Bread;
            label = "面包\n拖到入口";
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
        Destroy(cartCargoToken.gameObject);
        cartCargoToken = null;
    }

    private void RefreshRoadNodes()
    {
        foreach (CartNode node in cartNodes.Values)
        {
            if (node.Button == null)
            {
                continue;
            }

            Image image = node.Button.GetComponent<Image>();
            if (image != null)
            {
                if (node.Id == cartNodeId)
                {
                    image.color = new Color(0.2f, 0.62f, 1f, 0.86f);
                }
                else if (FindPath(cartNodeId, node.Id, true) != null)
                {
                    image.color = new Color(0.45f, 0.9f, 0.5f, 0.66f);
                }
                else
                {
                    image.color = new Color(1f, 1f, 1f, 0.46f);
                }
            }

            node.Button.interactable = !cartMoving && currentIndoorRoom == WhiteboxIndoorRoom.None && !girlFed;
        }
    }


    private void RefreshHud()
    {
        if (statusText != null)
        {
            statusText.text =
                "位置：" + cartNodes[cartNodeId].Name +
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

        if (indoorTitle != null)
        {
            indoorTitle.text = kitchen ? "厨房内部 C" : "地窖内部 B";
        }

        if (indoorBackground != null)
        {
            indoorBackground.color = kitchen
                ? (ovenLit ? new Color(0.92f, 0.62f, 0.34f, 1f) : new Color(0.7f, 0.72f, 0.76f, 1f))
                : (time == TimeState.War ? new Color(0.28f, 0.31f, 0.38f, 1f) : new Color(0.62f, 0.58f, 0.5f, 1f));
        }

        if (indoorHint != null)
        {
            if (kitchen)
            {
                indoorHint.text = rocketTriggered && !ovenLit
                    ? "窗户上有燃烧火箭：把厨房切到春天后拖到炉膛点火。"
                    : ovenLit ? "炉膛已点燃，会保持点燃状态。案板流程：面粉→清水→揉面→战时发酵→入烤箱。"
                    : "等待火箭事件：磨坊春天、麦田战时、厨房战时。案板流程只在春天进行。";
            }
            else
            {
                indoorHint.text = time == TimeState.War
                    ? "战时地窖：小女孩出现。把热面包拖给她。"
                    : "先从春天进入地窖；进入后切到战时，小女孩才会出现。";
            }
        }

        SetActiveIfNotNull(indoorFireboxDropZone, kitchen);
        SetActiveIfNotNull(indoorCellarDropZone, !kitchen && time == TimeState.War);

        SetButtonActive(indoorPlaceFlourButton, kitchen);
        SetButtonActive(indoorAddWaterButton, kitchen);
        SetButtonActive(indoorKneadDoughButton, kitchen);
        SetButtonActive(indoorLoadOvenButton, kitchen);
        SetButtonActive(indoorMoveBreadToDoorButton, kitchen);

        SetButtonInteractable(indoorPlaceFlourButton, kitchen && time == TimeState.Spring && flourAtKitchenDoor && (kitchenProcess == KitchenProcessState.Empty || kitchenProcess == KitchenProcessState.DoughMolded));
        SetButtonInteractable(indoorAddWaterButton, kitchen && time == TimeState.Spring && kitchenProcess == KitchenProcessState.FlourPlaced && waterAtKitchenDoor);
        SetButtonInteractable(indoorKneadDoughButton, kitchen && time == TimeState.Spring && kitchenProcess == KitchenProcessState.WaterAdded);
        SetButtonInteractable(indoorLoadOvenButton, kitchen && kitchenProcess == KitchenProcessState.DoughFermented);
        SetButtonInteractable(indoorMoveBreadToDoorButton, kitchen && kitchenProcess == KitchenProcessState.BreadReady);

        SetButtonActive(indoorTakeBreadButton, !kitchen && breadAtCellarEntrance);
        SetButtonInteractable(indoorTakeBreadButton, false);

        RefreshTimeSlider(indoorTimeSlider, indoorTimeSliderLabel, time);

        if (kitchen)
        {
            SpawnArrowInKitchenIfNeeded();
        }
        else
        {
            SpawnBreadInCellarIfNeeded();
        }
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
            parts.Add("水");
        }
        else if (kitchenProcess == KitchenProcessState.DoughMixed)
        {
            parts.Add("生面团");
        }
        else if (kitchenProcess == KitchenProcessState.DoughFermented)
        {
            parts.Add("发酵面团");
        }
        else if (kitchenProcess == KitchenProcessState.DoughMolded)
        {
            parts.Add("发霉");
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
            if (time == TimeState.War) return "战时：火箭来源，配合磨坊春天和厨房战时触发。";
            return "秋天：麦子成熟，但车不能再移动。";
        }

        if (region == RegionID.Kitchen)
        {
            if (ovenLit) return "炉火已点燃，等待面包出炉。";
            if (time == TimeState.War) return "战时：火箭可钉在窗框上。";
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
        if (cartDirection == CartDirection.Left) return "←";
        if (cartDirection == CartDirection.Up) return "↑";
        if (cartDirection == CartDirection.Down) return "↓";
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

    private Button CreateButton(string name, Transform parent, string text, float size, UnityEngine.Events.UnityAction action)
    {
        RectTransform rect = CreatePanel(name, parent, new Color(1f, 1f, 1f, 0.9f));
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = rect.GetComponent<Image>();
        button.onClick.AddListener(action);

        Text label = CreateText("Text", rect, text, size, FontStyle.Bold, ink, TextAnchor.MiddleCenter);
        Stretch(label.rectTransform, new Vector2(4f, 2f), new Vector2(-4f, -2f));
        return button;
    }

    private Slider CreateTimeSlider(string name, Transform parent, UnityEngine.Events.UnityAction<float> action)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.AddComponent<RectTransform>();

        Image touchArea = obj.AddComponent<Image>();
        touchArea.color = new Color(1f, 1f, 1f, 0.01f);
        touchArea.raycastTarget = true;

        Slider slider = obj.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 2f;
        slider.wholeNumbers = true;
        slider.value = 1f;
        slider.direction = Slider.Direction.LeftToRight;
        slider.onValueChanged.AddListener(action);

        Image background = CreateImage("Background", rect, new Color(0f, 0f, 0f, 0.22f));
        Stretch(background.rectTransform, new Vector2(0f, 7f), new Vector2(0f, -7f));
        background.raycastTarget = true;

        RectTransform fillArea = CreatePanel("Fill Area", rect, Color.clear);
        Stretch(fillArea, new Vector2(6f, 7f), new Vector2(-6f, -7f));

        Image fill = CreateImage("Fill", fillArea, new Color(0.2f, 0.62f, 1f, 0.42f));
        Stretch(fill.rectTransform);
        fill.raycastTarget = false;

        RectTransform handleArea = CreatePanel("Handle Slide Area", rect, Color.clear);
        Stretch(handleArea, new Vector2(8f, 0f), new Vector2(-8f, 0f));

        Image handle = CreateImage("Handle", handleArea, Color.white);
        handle.rectTransform.sizeDelta = new Vector2(18f, 18f);
        handle.raycastTarget = true;

        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        return slider;
    }

    private Button CreateSmallButton(string name, Transform parent, string text, UnityEngine.Events.UnityAction action)
    {
        return CreateButton(name, parent, text, 14, action);
    }

    private WhiteboxDropZone CreateDropZone(Transform parent, WhiteboxDropZoneType type, RegionID region, string label)
    {
        RectTransform rect = CreatePanel(type.ToString(), parent, new Color(1f, 0.5f, 0.12f, 0.72f));
        WhiteboxDropZone zone = rect.gameObject.AddComponent<WhiteboxDropZone>();
        zone.Initialize(this, type, region);

        Text text = CreateText("Label", rect, label, 13, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        Stretch(text.rectTransform, new Vector2(4f, 2f), new Vector2(-4f, -2f));
        return zone;
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

public sealed class WhiteboxDraggableToken : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public WhiteboxTokenType TokenType { get; private set; }
    public RegionID SourceRegion { get; private set; }

    private BorderMillWhiteboxController controller;
    private CanvasGroup canvasGroup;
    private Transform startParent;
    private Vector2 startAnchoredPosition;
    private bool consumed;

    public void Initialize(BorderMillWhiteboxController owner, WhiteboxTokenType tokenType, RegionID sourceRegion)
    {
        controller = owner;
        TokenType = tokenType;
        SourceRegion = sourceRegion;
        canvasGroup = GetComponent<CanvasGroup>();
    }

    public void MarkConsumed()
    {
        consumed = true;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        consumed = false;
        startParent = transform.parent;
        RectTransform rect = (RectTransform)transform;
        startAnchoredPosition = rect.anchoredPosition;

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false;
        }

        transform.SetParent(controller.DragLayer, true);
    }

    public void OnDrag(PointerEventData eventData)
    {
        transform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
        }

        bool accepted = controller.TryDropToken(this, eventData);
        if (accepted || consumed)
        {
            return;
        }

        transform.SetParent(startParent, false);
        ((RectTransform)transform).anchoredPosition = startAnchoredPosition;
    }
}

