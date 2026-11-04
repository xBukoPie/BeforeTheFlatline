using System.Threading.Tasks;
using Godot;

public partial class RoomManager : Node2D
{
    private static string _requestedStartRoom;
    private static string _requestedStartSpawn;
    private static Vector2 _requestedPlayerPosition;
    private static bool _hasRequestedPlayerPosition;

    public static void RequestStartRoomOnNextLoad(string roomPath, string spawn)
    {
        _requestedStartRoom = roomPath;
        _requestedStartSpawn = spawn;
        _requestedPlayerPosition = Vector2.Zero;
        _hasRequestedPlayerPosition = false;
    }

    // Stage an exact player position for the next World scene load.
    public static void RequestPlayerPositionOnNextLoad(Vector2 position)
    {
        _requestedPlayerPosition = position;
        _hasRequestedPlayerPosition = true;
    }

    // Current loaded room resource path and player position for saves.
    public string CurrentRoomPath => _room != null && !string.IsNullOrWhiteSpace(_room.SceneFilePath)
        ? _room.SceneFilePath
        : StartRoom;

    public Vector2 CurrentPlayerPosition => _player?.GlobalPosition ?? Vector2.Zero;

    [Export(PropertyHint.File, "*.tscn")] public string StartRoom { get; set; } = "res://Scenes/Home/Areas/PlayerRoom.tscn";
    [Export] public string StartSpawn { get; set; } = "startSpawn";
    [Export] public bool IsNight { get; set; } = true;
    [Export] public bool AllowNightToggleKey { get; set; } = true;
    [Export] public float FadeTime { get; set; } = 0.32f;
    [Export] public Color DayColor { get; set; } = new Color(0.77f, 0.77f, 0.81f);
    [Export] public Color NightColor { get; set; } = new Color(0.18f, 0.19f, 0.29f);
    [Export] public float DaySunEnergy { get; set; } = 0.12f;

    private Node2D _holder;
    private Node2D _player;
    private Camera2D _camera;
    private CanvasModulate _ambient;
    private DirectionalLight2D _sun;
    private ColorRect _fade;
    private Room _room;
    private bool _busy;
    private float _currentDaylight = 1f;
    private bool _hasLightingSample;

    public override void _Ready()
    {
        EnsureInteractAction();

        _holder = GetNode<Node2D>("RoomHolder");
        _player = GetNode<Node2D>("Player");
        _camera = GetNode<Camera2D>("Player/Camera2D");
        _ambient = GetNode<CanvasModulate>("RoomHolder/AmbientLight");
        _sun = GetNode<DirectionalLight2D>("RoomHolder/Sunlight");
        _fade = GetNode<ColorRect>("UI/Fade");

        _ambient.Visible = true;

        if (SchoolYardGame.DayClock.Instance != null)
        {
            SchoolYardGame.DayClock.Instance.TimeChanged += OnTimeChanged;
            SetNight(SchoolYardGame.DayClock.Instance.IsNight);
        }

        SchoolYardGame.AudioManager.Instance?.PlayWorldMusic(
            SchoolYardGame.DayClock.Instance?.IsNight ?? IsNight);

        string roomPath = string.IsNullOrWhiteSpace(_requestedStartRoom) ? StartRoom : _requestedStartRoom;
        string spawn = string.IsNullOrWhiteSpace(_requestedStartSpawn) ? StartSpawn : _requestedStartSpawn;
        _requestedStartRoom = null;
        _requestedStartSpawn = null;
        if (SchoolYardGame.DayClock.Instance != null)
            SchoolYardGame.DayClock.Instance.Running = true;
        LoadRoom(roomPath, spawn);
    }

    public override void _Process(double delta)
    {
        ApplyTimeOfDayLighting((float)delta);
    }

    private void ApplyTimeOfDayLighting(float delta = 0f)
    {
        if (_ambient == null || _sun == null)
            return;

        float hour = SchoolYardGame.DayClock.Instance?.Hour ?? 8f;
        float targetDaylight = CalculateDaylightBlend(hour);
        if (!_hasLightingSample)
        {
            _currentDaylight = targetDaylight;
            _hasLightingSample = true;
        }
        else if (delta > 0f)
        {
            float blend = 1f - Mathf.Exp(-delta / 4f);
            _currentDaylight = Mathf.Lerp(_currentDaylight, targetDaylight, blend);
        }

        float daylight = _currentDaylight;
        bool schoolHandlesItsOwnDayTint = _room != null &&
            string.Equals(_room.RoomName, "School", System.StringComparison.OrdinalIgnoreCase);

        _ambient.Color = schoolHandlesItsOwnDayTint
            ? Colors.White
            : NightColor.Lerp(DayColor, daylight);

        _sun.Energy = !schoolHandlesItsOwnDayTint && _room != null && _room.Outdoor
            ? DaySunEnergy * daylight
            : 0f;

        _room?.ApplyNightBlend(1f - daylight);
    }

    private static float CalculateDaylightBlend(float hour)
    {
        // Dawn (05:00-08:00) and dusk (16:30-20:30) are eased over several in-game hours.
        float dawn = Mathf.Clamp((hour - 5f) / 3f, 0f, 1f);
        dawn = dawn * dawn * (3f - 2f * dawn);

        float dusk = Mathf.Clamp((hour - 16.5f) / 4f, 0f, 1f);
        dusk = dusk * dusk * (3f - 2f * dusk);

        return Mathf.Clamp(Mathf.Min(dawn, 1f - dusk), 0f, 1f);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (AllowNightToggleKey && @event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.N })
        {
            if (SchoolYardGame.DayClock.Instance != null)
                SchoolYardGame.DayClock.Instance.SetHourOfDay(SchoolYardGame.DayClock.Instance.IsNight ? 12f : 20f);
            else
                SetNight(!IsNight);
        }
    }

    public override void _ExitTree()
    {
        if (SchoolYardGame.DayClock.Instance != null)
            SchoolYardGame.DayClock.Instance.TimeChanged -= OnTimeChanged;
    }

    private void OnTimeChanged(float hour)
    {
        if (SchoolYardGame.DayClock.Instance != null && IsNight != SchoolYardGame.DayClock.Instance.IsNight)
            SetNight(SchoolYardGame.DayClock.Instance.IsNight);
    }

    public async void GoTo(
        string roomPath,
        string spawn,
        bool showLoadingScreen = false,
        string loadingMessage = "Heading to school...",
        bool useSchoolTravelLoadingScreen = false)
    {
        if (_busy || string.IsNullOrEmpty(roomPath))
            return;

        _busy = true;
        _player.ProcessMode = ProcessModeEnum.Disabled;

        try
        {
            bool loadedWithScreen = false;
            if (showLoadingScreen && SceneTransition.Instance != null)
            {
                loadedWithScreen = await SceneTransition.Instance.RunWithLoadingScreen(
                    () =>
                    {
                        LoadRoom(roomPath, spawn);
                        _camera.ResetSmoothing();
                    },
                    loadingMessage,
                    2.0,
                    useSchoolTravelLoadingScreen);
            }

            if (!loadedWithScreen)
            {
                await FadeTo(1f);
                LoadRoom(roomPath, spawn);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                _camera.ResetSmoothing();
                await FadeTo(0f);
            }
        }
        catch (System.Exception ex)
        {
            GD.PushError($"RoomManager: Error during room transition to '{roomPath}': {ex.Message}");
        }
        finally
        {
            if (GodotObject.IsInstanceValid(_player))
                _player.ProcessMode = ProcessModeEnum.Inherit;
            _busy = false;
        }
    }

    public void SetNight(bool night)
    {
        // The boolean is retained for room and music logic. Visual lighting is updated
        // continuously in _Process so it does not snap at the night threshold.
        IsNight = night;
        ApplyTimeOfDayLighting();
    }

    private void LoadRoom(string path, string spawn)
    {
        var scene = GD.Load<PackedScene>(path);
        if (scene == null)
        {
            GD.PushError($"RoomManager: cannot load room '{path}'.");
            return;
        }

        if (_room != null)
        {
            _holder.RemoveChild(_room);
            _room.QueueFree();
        }

        _room = scene.Instantiate<Room>();
        if (SchoolYardGame.DayClock.Instance != null)
            SchoolYardGame.DayClock.Instance.Running = true;

        _holder.AddChild(_room);
        if (_hasRequestedPlayerPosition)
        {
            _player.GlobalPosition = _requestedPlayerPosition;
            _requestedPlayerPosition = Vector2.Zero;
            _hasRequestedPlayerPosition = false;
        }
        else
        {
            _player.GlobalPosition = _room.GetSpawnPosition(spawn);
        }

        if (_player is CharacterBody2D body)
            body.Velocity = Vector2.Zero;

        ConfigureCamera();
        SetNight(IsNight);
        ApplyTimeOfDayLighting();
        SaveManager.Instance?.RequestAutoSave();
    }

    private void ConfigureCamera()
    {
        Rect2 bounds = _room.GetBounds();
        float zoom = _room.CameraZoom;
        Vector2 view = GetViewportRect().Size / zoom;
        Vector2 size = new(Mathf.Max(bounds.Size.X, view.X), Mathf.Max(bounds.Size.Y, view.Y));
        Rect2 limits = new(bounds.GetCenter() - size / 2f, size);
        _camera.Zoom = new Vector2(zoom, zoom);
        _camera.LimitLeft = Mathf.RoundToInt(limits.Position.X);
        _camera.LimitTop = Mathf.RoundToInt(limits.Position.Y);
        _camera.LimitRight = Mathf.RoundToInt(limits.End.X);
        _camera.LimitBottom = Mathf.RoundToInt(limits.End.Y);
        _camera.ResetSmoothing();
    }

    private async Task FadeTo(float alpha)
    {
        var tween = CreateTween();
        tween.TweenProperty(_fade, "modulate:a", alpha, FadeTime)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        await ToSignal(tween, Tween.SignalName.Finished);
    }

    private static void EnsureInteractAction()
    {
        if (InputMap.HasAction("interact"))
            return;

        InputMap.AddAction("interact");
        InputMap.ActionAddEvent("interact", new InputEventKey { PhysicalKeycode = Key.E });
    }
}
