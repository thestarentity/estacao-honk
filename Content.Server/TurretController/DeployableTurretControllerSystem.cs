using Content.Server.DeviceNetwork.Systems;
using Content.Shared.Access;
using Content.Shared.DeviceNetwork;
using Content.Shared.DeviceNetwork.Components;
using Content.Shared.DeviceNetwork.Events;
using Content.Shared.DeviceNetwork.Systems;
using Content.Shared.TurretController;
using Content.Shared.Turrets;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using System.Linq;
using Content.Server.Administration.Logs;
using Content.Shared.Database;

namespace Content.Server.TurretController;

/// <inheritdoc/>
public sealed partial class DeployableTurretControllerSystem : SharedDeployableTurretControllerSystem
{
    [Dependency] private UserInterfaceSystem _userInterfaceSystem = default!;
    [Dependency] private DeviceNetworkSystem _deviceNetwork = default!;
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private SharedDeployableTurretSystem _deployableTurret = default!;

    /// Keys for the device network. See <see cref="DeviceNetworkConstants"/> for further examples.
    public const string CmdSetArmamemtState = "set_armament_state";
    public const string CmdSetAccessExemptions = "set_access_exemption";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DeployableTurretControllerComponent, MapInitEvent>(OnControllerMapInit);
        SubscribeLocalEvent<DeployableTurretControllerComponent, BoundUIOpenedEvent>(OnBUIOpened);
        SubscribeLocalEvent<DeployableTurretControllerComponent, DeviceListUpdateEvent>(OnDeviceListUpdate);
        SubscribeLocalEvent<DeployableTurretControllerComponent, DeviceNetworkPacketEvent>(OnPacketReceived);
    }

    /// <summary>
    /// Os links torreta↔painel salvos no MAPA não disparam <see cref="DeviceListUpdateEvent"/> (esse só
    /// dispara na religação manual via configurador de rede). Sem isto, o <see cref="DeployableTurretComponent.AiController"/>
    /// ficaria nulo numa rodada normal e o menu radial da IA aberto direto na torreta sairia VAZIO. Aqui,
    /// no MapInit do painel, gravamos o link em cada torreta já ligada (mesma lógica do laço de "add" do
    /// <see cref="OnDeviceListUpdate"/>). (Fork Estação Honk.)
    /// </summary>
    private void OnControllerMapInit(Entity<DeployableTurretControllerComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<DeviceListComponent>(ent, out var deviceList))
            return;

        foreach (var turretUid in deviceList.Devices)
        {
            if (TryComp<DeployableTurretComponent>(turretUid, out var turretComp))
                _deployableTurret.SetAiController((turretUid, turretComp), ent);
        }
    }

    private void OnBUIOpened(Entity<DeployableTurretControllerComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUIState(ent);
    }

    private void OnDeviceListUpdate(Entity<DeployableTurretControllerComponent> ent, ref DeviceListUpdateEvent args)
    {
        if (!TryComp<DeviceNetworkComponent>(ent, out var deviceNetwork))
            return;

        // List of new added turrets
        var turretsToAdd = args.Devices.Except(args.OldDevices);

        // Request data from newly linked devices
        var payload = new NetworkPayload
        {
            [DeviceNetworkConstants.Command] = DeviceNetworkConstants.CmdUpdatedState,
        };

        foreach (var turretUid in turretsToAdd)
        {
            if (!TryComp<DeployableTurretComponent>(turretUid, out var turretComp))
                continue;

            // Liga a torreta a este painel (a IA usa isso p/ abrir o radial direto na torreta).
            _deployableTurret.SetAiController((turretUid, turretComp), ent);

            if (!TryComp<DeviceNetworkComponent>(turretUid, out var turretDeviceNetwork))
                continue;

            _deviceNetwork.QueuePacket(ent, turretDeviceNetwork.Address, payload, device: deviceNetwork);
        }

        // Remove newly unlinked devices
        var turretsToRemove = args.OldDevices.Except(args.Devices);
        var refreshUi = false;

        foreach (var turretUid in turretsToRemove)
        {
            // Desfaz o link torreta→painel.
            if (TryComp<DeployableTurretComponent>(turretUid, out var turretComp))
                _deployableTurret.SetAiController((turretUid, turretComp), null);

            if (!TryComp<DeviceNetworkComponent>(turretUid, out var turretDeviceNetwork))
                continue;

            if (ent.Comp.LinkedTurrets.Remove(turretDeviceNetwork.Address))
                refreshUi = true;
        }

        if (refreshUi)
            UpdateUIState(ent);
    }

    private void OnPacketReceived(Entity<DeployableTurretControllerComponent> ent, ref DeviceNetworkPacketEvent args)
    {
        if (!args.Data.TryGetValue(DeviceNetworkConstants.Command, out string? command))
            return;

        if (!TryComp<DeviceNetworkComponent>(ent, out var deviceNetwork) || deviceNetwork.ReceiveFrequency != args.Frequency)
            return;

        // If an update was received from a turret, connect to it and update the UI
        if (command == DeviceNetworkConstants.CmdUpdatedState &&
            args.Data.TryGetValue(command, out DeployableTurretState updatedState))
        {
            ent.Comp.LinkedTurrets[args.SenderAddress] = updatedState;
            UpdateUIState(ent);
        }
    }

    /// <summary>
    /// Define o armamento de todas as torretas ligadas IGNORANDO a checagem de acesso — para a IA de
    /// estação acionar pelo menu radial (ela fura o ID, como faz com portas/APC). (Fork Estação Honk.)
    /// </summary>
    public void SetArmamentFromAi(Entity<DeployableTurretControllerComponent> ent, int armamentState, EntityUid user)
    {
        ChangeArmamentSetting(ent, armamentState, user);
    }

    protected override void ChangeArmamentSetting(Entity<DeployableTurretControllerComponent> ent, int armamentState, EntityUid? user = null)
    {
        base.ChangeArmamentSetting(ent, armamentState, user);

        if (!TryComp<DeviceNetworkComponent>(ent, out var device))
            return;

        // Update linked turrets' armament statuses
        var payload = new NetworkPayload
        {
            [DeviceNetworkConstants.Command] = CmdSetArmamemtState,
            [CmdSetArmamemtState] = armamentState,
        };

        _adminLogger.Add(LogType.ItemConfigure, LogImpact.Medium, $"{ToPrettyString(user)} set {ToPrettyString(ent)} to {armamentState}");

        _deviceNetwork.QueuePacket(ent, null, payload, device: device);
    }

    protected override void ChangeExemptAccessLevels(
        Entity<DeployableTurretControllerComponent> ent,
        HashSet<ProtoId<AccessLevelPrototype>> exemptions,
        bool enabled,
        EntityUid? user = null
    )
    {
        base.ChangeExemptAccessLevels(ent, exemptions, enabled, user);

        if (!TryComp<DeviceNetworkComponent>(ent, out var device) ||
            !TryComp<TurretTargetSettingsComponent>(ent, out var turretTargetingSettings))
            return;

        // Update linked turrets' target selection exemptions
        var payload = new NetworkPayload
        {
            [DeviceNetworkConstants.Command] = CmdSetAccessExemptions,
            [CmdSetAccessExemptions] = turretTargetingSettings.ExemptAccessLevels,
        };

        foreach (var exemption in exemptions)
        {
            _adminLogger.Add(LogType.ItemConfigure, LogImpact.Medium, $"{ToPrettyString(user)} set {ToPrettyString(ent)} authorization of {exemption} to {enabled}");
        }

        _deviceNetwork.QueuePacket(ent, null, payload, device: device);
    }

    private void UpdateUIState(Entity<DeployableTurretControllerComponent> ent)
    {
        var turretStates = new Dictionary<string, string>();

        foreach (var (address, state) in ent.Comp.LinkedTurrets)
        {
            var stateName = state.ToString().ToLower();
            var stateDesc = Loc.GetString("turret-controls-window-turret-" + stateName);
            turretStates.Add(address, stateDesc);
        }

        var uiState = new DeployableTurretControllerBoundInterfaceState(turretStates);
        _userInterfaceSystem.SetUiState(ent.Owner, DeployableTurretControllerUiKey.Key, uiState);
    }
}
