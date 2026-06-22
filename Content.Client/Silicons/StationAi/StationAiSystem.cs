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

    /// <summary>
    /// Cor do tell da APC: laranja-âmbar se hospedando IA shuntada, vermelho se só hackeada,
    /// null se nenhum dos dois (sem tell).
    /// </summary>
    private static Color? ApcTellTint(bool occupied, bool hacked)
        => occupied ? new Color(1f, 0.55f, 0.1f)    // laranja-âmbar (hospedando IA)
         : hacked   ? new Color(1f, 0.04f, 0.04f)   // vermelho forte (só hackeada)
         : null;

    /// <summary>
    /// Re-aplica o tell visual (cor da TELA e da LUZ) das APCs a cada frame, lendo os campos
    /// networkados <see cref="StationAiApcControllableComponent.Occupied"/>/<c>Hacked</c> — não
    /// a appearance data. Motivo: o <see cref="ApcVisualizerSystem"/> padrão redefine a cor da
    /// luz pela carga a cada mudança de aparência (e ordenar nosso visualizador depois dele
    /// travava a sincronização do cliente); e o <c>AppearanceChangeEvent</c> da tela pode não
    /// chegar no instante do shunt (o olho da IA é removido na mesma passada, mudando a
    /// perspectiva do cliente). Re-aplicar todo frame faz a tela e a luz se auto-corrigirem.
    /// Só escreve quando a cor difere, então o custo é desprezível.
    /// </summary>
    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var query = EntityQueryEnumerator<StationAiApcControllableComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var apc, out var sprite))
        {
            var tint = ApcTellTint(apc.Occupied, apc.Hacked);

            // Luz: com tell, tinge; sem tell, deixa a cor que o ApcVisualizerSystem define pela carga.
            if (tint is { } lightTint
                && TryComp<PointLightComponent>(uid, out var light)
                && light.Color != lightTint)
            {
                _lights.SetColor(uid, lightTint, light);
            }

            // Tela (camada ChargeState): com tell, tinge; sem tell, volta ao branco (tela normal).
            if (_sprite.TryGetLayer((uid, sprite), ApcVisualLayers.ChargeState, out var screenLayer, false))
            {
                var screenColor = tint ?? Color.White;
                if (screenLayer.Color != screenColor)
                    _sprite.LayerSetColor(screenLayer, screenColor);
            }
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
