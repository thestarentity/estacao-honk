using Content.Shared.Silicons.StationAi;
using Content.Client.Power.APC;
using Robust.Shared.Map;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;

namespace Content.Client.Silicons.StationAi;

public sealed partial class StationAiSystem : SharedStationAiSystem
{
    [Dependency] private IOverlayManager _overlayMgr = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private SharedPointLightSystem _lights = default!;

    private StationAiOverlay? _overlay;

    public override void Initialize()
    {
        base.Initialize();
        InitializeAirlock();
        InitializePowerToggle();
        InitializeApc();
        InitializeBorg();
        InitializeAtmos();
        InitializeTurret();
        InitializeStructures();

        SubscribeLocalEvent<StationAiOverlayComponent, LocalPlayerAttachedEvent>(OnAiAttached);
        SubscribeLocalEvent<StationAiOverlayComponent, LocalPlayerDetachedEvent>(OnAiDetached);
        SubscribeLocalEvent<StationAiOverlayComponent, ComponentInit>(OnAiOverlayInit);
        SubscribeLocalEvent<StationAiOverlayComponent, ComponentRemove>(OnAiOverlayRemove);
        SubscribeLocalEvent<StationAiCoreComponent, AppearanceChangeEvent>(OnAppearanceChange);
        SubscribeLocalEvent<StationAiApcControllableComponent, AppearanceChangeEvent>(OnApcAppearanceChange);
    }

    private void OnAiOverlayInit(Entity<StationAiOverlayComponent> ent, ref ComponentInit args)
    {
        var attachedEnt = _player.LocalEntity;

        if (attachedEnt != ent.Owner)
            return;

        AddOverlay();
    }

    private void OnAiOverlayRemove(Entity<StationAiOverlayComponent> ent, ref ComponentRemove args)
    {
        var attachedEnt = _player.LocalEntity;

        if (attachedEnt != ent.Owner)
            return;

        RemoveOverlay();
    }

    private void AddOverlay()
    {
        if (_overlay != null)
            return;

        _overlay = new StationAiOverlay();
        _overlayMgr.AddOverlay(_overlay);
    }

    private void RemoveOverlay()
    {
        if (_overlay == null)
            return;

        _overlayMgr.RemoveOverlay(_overlay);
        _overlay = null;
    }

    private void OnAiAttached(Entity<StationAiOverlayComponent> ent, ref LocalPlayerAttachedEvent args)
    {
        AddOverlay();
    }

    private void OnAiDetached(Entity<StationAiOverlayComponent> ent, ref LocalPlayerDetachedEvent args)
    {
        RemoveOverlay();
    }

    private void OnAppearanceChange(Entity<StationAiCoreComponent> entity, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (_appearance.TryGetData<PrototypeLayerData>(entity.Owner, StationAiVisualLayers.Icon, out var layerData, args.Component))
            _sprite.LayerSetData((entity.Owner, args.Sprite), StationAiVisualLayers.Icon, layerData);

        _sprite.LayerSetVisible((entity.Owner, args.Sprite), StationAiVisualLayers.Icon, layerData != null);
    }

    private void OnApcAppearanceChange(Entity<StationAiApcControllableComponent> ent, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        // Tell visual: só a TELA muda de cor; o corpo da APC fica intacto. A luz é tratada
        // no FrameUpdate, porque o ApcVisualizerSystem padrão sobrescreve a cor da luz a
        // cada mudança de aparência e não dá para ordenar com segurança depois dele.
        var occupied = _appearance.TryGetData<bool>(ent.Owner, StationAiApcVisuals.Occupied, out var ov, args.Component) && ov;
        var hacked   = _appearance.TryGetData<bool>(ent.Owner, StationAiApcVisuals.Hacked,   out var hv, args.Component) && hv;
        var tint = ApcTellTint(occupied, hacked);

        // Nunca tingir o sprite inteiro (resquício do comportamento antigo).
        _sprite.SetColor((ent.Owner, args.Sprite), Color.White);

        // Tinge apenas a camada da tela; sem tell, volta ao branco (deixa a imagem da tela normal).
        if (_sprite.LayerMapTryGet((ent.Owner, args.Sprite), ApcVisualLayers.ChargeState, out var screenLayer, false))
            _sprite.LayerSetColor((ent.Owner, args.Sprite), screenLayer, tint ?? Color.White);
    }

    /// <summary>
    /// Cor do tell da APC: laranja-âmbar se hospedando IA shuntada, vermelho se só hackeada,
    /// null se nenhum dos dois (sem tell).
    /// </summary>
    private static Color? ApcTellTint(bool occupied, bool hacked)
        => occupied ? new Color(1f, 0.55f, 0.1f)    // laranja-âmbar (hospedando IA)
         : hacked   ? new Color(1f, 0.04f, 0.04f)   // vermelho forte (só hackeada)
         : null;

    /// <summary>
    /// Re-aplica a cor da luz das APCs com tell a cada frame. O ApcVisualizerSystem padrão
    /// redefine a cor da luz pela carga sempre que a aparência muda, e ordenar nosso
    /// visualizador depois dele travava a sincronização do cliente. Só toca em APCs com tell
    /// e só quando a cor difere, então o custo é desprezível.
    /// </summary>
    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var query = EntityQueryEnumerator<StationAiApcControllableComponent, PointLightComponent>();
        while (query.MoveNext(out var uid, out var apc, out var light))
        {
            if (ApcTellTint(apc.Occupied, apc.Hacked) is not { } tint)
                continue;

            if (light.Color != tint)
                _lights.SetColor(uid, tint, light);
        }
    }

    /// <summary>
    /// Pede ao servidor para mover o olho da IA até a coordenada clicada num mapa.
    /// </summary>
    public void MoveEyeTo(EntityCoordinates coords)
    {
        RaiseNetworkEvent(new StationAiMoveEyeEvent(GetNetCoordinates(coords)));
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlayMgr.RemoveOverlay<StationAiOverlay>();
    }
}
