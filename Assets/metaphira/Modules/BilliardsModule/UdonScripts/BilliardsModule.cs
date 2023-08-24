#if UNITY_ANDROID
#define HT_QUEST
#endif

#if !HT_QUEST || true
#define HT8B_DEBUGGER
#endif

using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
using System;
using Metaphira.Modules.CameraOverride;

[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class BilliardsModule : UdonSharpBehaviour
{
    #region InspectorValues
    [SerializeField] [HideInInspector] public Color k_colour_foul,        // v1.6: ( 1.2, 0.0, 0.0, 1.0 )
                                                    k_colour_default,     // v1.6: ( 1.0, 1.0, 1.0, 1.0 )
                                                    k_colour_off = new Color(0.01f, 0.01f, 0.01f, 1.0f);

    [SerializeField] [HideInInspector] public Color k_teamColour_spots,   // v1.6: ( 0.00, 0.75, 1.75, 1.0 )
                                                    k_teamColour_stripes; // v1.6: ( 1.75, 0.25, 0.00, 1.0 )

    [SerializeField] [HideInInspector] public Color k_colour4Ball_team_0, // v1.6: ( )
                                                    k_colour4Ball_team_1; // v1.6: ( 2.0, 1.0, 0.0, 1.0 )

    [SerializeField] [HideInInspector] public Color k_fabricColour_8ball, // v1.6: ( 0.3, 0.3, 0.3, 1.0 )
                                                    k_fabricColour_9ball, // v1.6: ( 0.1, 0.6, 1.0, 1.0 )
                                                    k_fabricColour_4ball; // v1.6: ( 0.15, 0.75, 0.3, 1.0 )
    [Header("Textures")]
    [SerializeField] public Texture[] textureSets;
    [SerializeField] public ModelData[] tableModels;
    [SerializeField] public Texture2D[] tableSkins;
    [SerializeField] public Texture2D[] cueSkins;
    [HideInInspector][SerializeField] public Texture snookerTexture;

    [Header("Snooker Spawn Positions")]
    [SerializeField] public Transform[] coloredPositions;

    [Header("Managers")]
    [SerializeField] public NetworkingManager networkingManager;
    [SerializeField] public PracticeManager practiceManager;
    [SerializeField] public RepositionManager repositionManager;
    [SerializeField] public DesktopManager desktopManager;
    [SerializeField] public CameraManager cameraManager;
    [SerializeField] public GraphicsManager graphicsManager;
    [SerializeField] public StandardPhysicsManager standardPhysicsManager;
    [SerializeField] public MenuManager menuManager;
    [SerializeField] public UdonSharpBehaviour cameraModule;

    [HideInInspector]
    [SerializeField] public AudioClip snd_Intro,
                                      snd_Sink,
                                      snd_NewTurn,
                                      snd_PointMade,
                                      snd_btn,
                                      snd_spin,
                                      snd_spinstop,
                                      snd_hitball;

    [Space(10)]
    [Header("Internal (no touching!)")]

    [SerializeField] public CueController[] cueControllers;

    [SerializeField] public GameObject[] balls;

    [SerializeField] public GameObject guideline,
                                       devhit,
                                       markerObj,
                                       marker9ball;

    [SerializeField] Text ltext;
    [SerializeField] Text infReset;

    [SerializeField] ReflectionProbe reflection_main;
    #endregion

    #region NonSerialized
    [NonSerialized] public readonly string[] DEPENDENCIES = new string[] { nameof(CameraOverrideModule) };
    [NonSerialized] public readonly string VERSION = "6.0.0";
    [NonSerialized] public readonly int[] sixredsnooker_ballpoints = { 0, 7, 2, 5, 1, 6, 1, 3, 4, 1, 1, 1, 1 },
                                          break_order_sixredsnooker = { 4, 6, 9, 10, 11, 12, 2, 7, 8, 3, 5, 1 },
                                          break_order_8ball = { 9, 2, 10, 11, 1, 3, 4, 12, 5, 13, 14, 6, 15, 7, 8 },
                                          break_order_9ball = { 2, 3, 4, 5, 9, 6, 7, 8, 1 },
                                          break_rows_9ball = { 0, 1, 2, 1, 0 };

    [NonSerialized] public const int PERF_MAX = 6;

    [NonSerialized] public Vector3[] ballsP = new Vector3[16],
                                     ballsV = new Vector3[16],
                                     ballsW = new Vector3[16];

    [NonSerialized] public string[] playerNamesCached = new string[4],
                                    playerNamesLocal = new string[4];

    [NonSerialized] public int[] fbScoresLocal = new int[2];

    [NonSerialized] public string tournamentRefereeLocal;

    [NonSerialized] public float repoMaxX;

    [NonSerialized] public bool lobbyOpen,
                                gameLive,
                                teamsLocal,
                                noGuidelineLocal,
                                noLockingLocal,
                                isTableOpenLocal,
                                canPlayLocal,
                                isGuidelineValid,
                                colorTurnLocal,
                                redsOnTable,
                                isBreak,
                                isLocalSimulationRunning,
                                canHitCueBall = false,
                                isReposition = false,
                                is8Ball = false,
                                is9Ball = false,
                                is4Ball = false,
                                isJp4Ball = false,
                                isKr4Ball = false,
                                isSnooker6Red = false,
                                isPracticeMode = false,
                                timerRunning = false;

    [NonSerialized] public uint gameModeLocal,
                                timerLocal,
                                ballsPocketedLocal,
                                teamIdLocal,
                                fourBallCueBallLocal,
                                teamColorLocal,
                                winningTeamLocal,
                                previewWinningTeamLocal,
                                localTeamId = 0u;

    [NonSerialized] public int activeCueSkin,
                               tableSkinLocal,
                               PERF_MAIN = 0,
                               PERF_PHYSICS_MAIN = 1,
                               PERF_PHYSICS_VEL = 2,
                               PERF_PHYSICS_BALL = 3,
                               PERF_PHYSICS_CUSHION = 4,
                               PERF_PHYSICS_POCKET = 5,
                               localPlayerId = -1;

    // table model properties
    [NonSerialized] public float k_TABLE_WIDTH, // horizontal span of table
                                 k_TABLE_HEIGHT, // vertical span of table
                                 k_CUSHION_RADIUS, // The roundess of colliders
                                 k_POCKET_RADIUS, // Full diameter of pockets
                                 k_INNER_RADIUS; // Pocket 'hitbox' cylinder

    [NonSerialized] public Vector3 k_vE, // corner pocket data
                                   k_vF, // side pocket data
                                   k_rack_position = new Vector3(),
                                   k_rack_direction = new Vector3();

    [NonSerialized] public GameObject[] pockets;
    [NonSerialized] public GameObject auto_pocketblockers;
    [NonSerialized] public Transform table;
    [NonSerialized] public AudioSource aud_main;
    [NonSerialized] public UdonBehaviour callbacks;
    [NonSerialized] public UdonSharpBehaviour currentPhysicsManager;
    [NonSerialized] public CueController activeCue;
    [NonSerialized] public CameraOverrideModule cameraOverrideModule;
    [NonSerialized] public Vector3[][] initialPositions = new Vector3[5][];
    #endregion

    #region Private
    private readonly Color k_aimColour_aim = new Color(0.7f, 0.7f, 0.7f, 1.0f),
                           k_aimColour_locked = new Color(1.0f, 1.0f, 1.0f, 1.0f);
    private const int LOG_MAX = 32;

    private const float k_BALL_RADIUS = 0.03f,
                        k_BALL_DIAMETRE = 0.06f,
                        k_BALL_PL_X = 0.03f, // break placement X
                        k_BALL_PL_Y = 0.05196152422f, // sin(60) * 0.06
                        k_RANDOMIZE_F = 0.0001f,
                        k_SPOT_POSITION_X = 0.5334f, // First X position of the racked balls
                        k_SPOT_CAROM_X = 0.8001f; // Spot position for carom mode

    private GameObject auto_rackPosition;
    private GameObject auto_colliderBaseVFX;

    private uint[] initialBallsPocketed = new uint[5];

    private string[] LOG_LINES = new string[32];
    private string[] moderators = new string[0];
    private string[] perfNames = new string[] {
      "main",
      "physics",
      "physicsVel",
      "physicsBall",
      "physicsCushion",
      "physicsPocket"
   };

    private float[] perfCounters = new float[PERF_MAX],
                    perfTimings = new float[PERF_MAX],
                    perfStart = new float[PERF_MAX];

    private byte gameStateLocal = byte.MaxValue,
                 turnStateLocal = byte.MaxValue;

    private int timerStartLocal,
                tableModelLocal,
                firstHit = 0,
                secondHit = 0,
                thirdHit = 0,
                LOG_LEN = 0,
                LOG_PTR = 0;

    private uint repositionStateLocal,
                 ballsPocketedOrig;

    private bool isLocalSimulationOurs = false,
                 fbMadePoint = false,
                 fbMadeFoul = false;
    #endregion


    private void OnEnable()
    {

        _LogInfo("initializing billiards module");

        cameraOverrideModule = (CameraOverrideModule)_GetModule(nameof(CameraOverrideModule));

        initializeRack();

        resetCachedData();

        currentPhysicsManager = standardPhysicsManager;

        setTableModel(0, false);

        aud_main = this.GetComponent<AudioSource>();

        for (int i = 0; i < balls.Length; i++)
            balls[i].GetComponentInChildren<Repositioner>(true)._Init(this, i);

        networkingManager._Init(this);
        practiceManager._Init(this);
        repositionManager._Init(this);
        desktopManager._Init(this);
        cameraManager._Init(this);
        graphicsManager._Init(this);
        standardPhysicsManager._Init(this);
        menuManager._Init(this);

        currentPhysicsManager.SendCustomEvent("_InitConstants");


#if HT8B_DEBUGGER
        this.transform.Find("debugger").gameObject.SetActive(true);
#endif

        this.transform.Find("intl.balls/guide/guide_display").GetComponent<MeshRenderer>().material.SetMatrix("_BaseTransform", this.transform.worldToLocalMatrix);

        reflection_main.RenderProbe();

#if UNITY_EDITOR
        graphicsManager._OnGameStarted();
        menuManager.menuSettings.transform.localScale = Vector3.zero;
#endif
    }

    private void FixedUpdate()
    {
        currentPhysicsManager.SendCustomEvent("_FixedTick");
    }

    private void Update()
    {
        networkingManager._Tick();

        desktopManager._Tick();
        menuManager._Tick();

        _BeginPerf(PERF_MAIN);
        practiceManager._Tick();
        repositionManager._Tick();
        cameraManager._Tick();
        graphicsManager._Tick();
        tickTimer();
        _Update9BallMarker();

        networkingManager._FlushBuffer();
        _EndPerf(PERF_MAIN);

        if (perfCounters[PERF_MAIN] % 500 == 0) _RedrawDebugger();
    }

    public override void OnPlayerLeft(VRCPlayerApi player)
    {
        if (Networking.LocalPlayer == null) return;

        if (!lobbyOpen) return;

        VRCPlayerApi gameHost = _GetPlayerByName(playerNamesLocal[0]);
        if (!Utilities.IsValid(gameHost))
        {
            // host left. if they were the only ones in-game, instance master tries to close the lobby. otherwise, everyone in-lobby tries
            int otherPlayers = 0;
            for (int i = 0; i < 4; i++)
            {
                if (playerNamesLocal[i] == "") continue;

                VRCPlayerApi possiblePlayer = _GetPlayerByName(playerNamesLocal[i]);
                if (!Utilities.IsValid(possiblePlayer)) continue;

                otherPlayers++;
            }

            if ((otherPlayers == 0 && Networking.LocalPlayer.isMaster) || (otherPlayers > 0 && localPlayerId != -1))
            {
                networkingManager._OnLobbyClosed();
            }
        }
        else if (gameHost.isLocal)
        {
            // only host updates player list
            for (int i = 0; i < 4; i++)
            {
                if (playerNamesLocal[i] == "") continue;

                VRCPlayerApi possiblePlayer = _GetPlayerByName(playerNamesLocal[i]);
                if (Utilities.IsValid(possiblePlayer)) continue;

                networkingManager._OnKickLobby(i);
            }
        }
    }

    public UdonSharpBehaviour _GetModule(string type)
    {
        string[] parts = cameraModule.GetUdonTypeName().Split('.');
        if (parts[parts.Length - 1] == type)
        {
            return cameraModule;
        }
        return null;
    }

    #region Triggers
    public void _TriggerLobbyOpen()
    {
        if (lobbyOpen) return;

        networkingManager._OnLobbyOpened();
    }

    public void _TriggerLobbyClosed()
    {
        networkingManager._OnLobbyClosed();
    }

    public void _TriggerTeamsChanged(bool teamsEnabled)
    {
        networkingManager._OnTeamsChanged(teamsEnabled);
    }

    public void _TriggerNoGuidelineChanged(bool noGuidelineEnabled)
    {
        networkingManager._OnNoGuidelineChanged(noGuidelineEnabled);
    }

    public void _TriggerNoLockingChanged(bool noLockingEnabled)
    {
        networkingManager._OnNoLockingChanged(noLockingEnabled);
    }

    public void _TriggerTimerChanged(uint timerSelected)
    {
        networkingManager._OnTimerChanged(timerSelected);
    }

    public void _TriggerGameModeChanged(uint newGameMode)
    {
        networkingManager._OnGameModeChanged(newGameMode);
    }

    public void _TriggerGlobalSettingsUpdated(string newTournamentReferee, int newTableModel, int newTableSkin)
    {
        networkingManager._OnGlobalSettingsChanged(newTournamentReferee, (byte)newTableModel, (byte)newTableSkin);
    }

    public void _TriggerCueBallHit()
    {
        if (localTeamId != teamIdLocal && !isPracticeMode) return; // is there a better way to do this?

        _LogWarn("trying to propagate cue ball hit, linear velocity is " + ballsV[0].ToString("F4") + " and angular velocity is " + ballsW[0].ToString("F4"));

        if (float.IsNaN(ballsV[0].x) || float.IsNaN(ballsV[0].y) || float.IsNaN(ballsV[0].z) || float.IsNaN(ballsW[0].x) || float.IsNaN(ballsW[0].y) || float.IsNaN(ballsW[0].z))
        {
            ballsV[0] = Vector3.zero;
            ballsW[0] = Vector3.zero;
            return;
        }

        _TriggerCueDeactivate();

        networkingManager._OnHitBall(ballsV[0], ballsW[0]);
    }

    public void _TriggerCueActivate()
    {
        if (!isOurTurn()) return;

        if (Vector3.Distance(activeCue._GetCuetip().transform.position, ballsP[0]) < k_BALL_RADIUS)
        {
            _TriggerCueDeactivate();
            return;
        }

        canHitCueBall = true;
        this._TriggerOnPlayerPrepareShoot();

#if !HT_QUEST
        this.transform.Find("intl.balls/guide/guide_display").GetComponent<MeshRenderer>().material.SetColor("_Colour", k_aimColour_locked);
#endif
    }

    public void _TriggerCueDeactivate()
    {
        canHitCueBall = false;

#if !HT_QUEST
        guideline.gameObject.transform.Find("guide_display").GetComponent<MeshRenderer>().material.SetColor("_Colour", k_aimColour_aim);
#endif
    }

    public void _OnPickupCue()
    {
        if (!Networking.LocalPlayer.IsUserInVR()) desktopManager._OnPickupCue();
    }

    public void _OnDropCue()
    {
        if (!Networking.LocalPlayer.IsUserInVR()) desktopManager._OnDropCue();
    }

    public void _TriggerOnPlayerPrepareShoot()
    {
        networkingManager._OnPlayerPrepareShoot();
    }

    public void _OnPlayerPrepareShoot()
    {
        cameraManager._OnPlayerPrepareShoot();
    }

    public void _TriggerPlaceBall(int idx)
    {
        if (!canPlayLocal) return; // in case player was forced to drop ball since someone else took the shot

        // practiceManager._Record();

        bool consumeReposition = false;
        if (idx == 0)
        {
            currentPhysicsManager.SendCustomEvent("_IsCueBallTouching");
            bool isTouching = (bool)currentPhysicsManager.GetProgramVariable("outIsTouching");

            consumeReposition = !isTouching;
        }

        networkingManager._OnRepositionBalls(ballsP, consumeReposition);
    }

    public void _TriggerGameStart()
    {
        _LogYes("starting game");

        networkingManager._OnGameStart(initialBallsPocketed[gameModeLocal], initialPositions[gameModeLocal]);
    }

    public void _TriggerJoinTeam(int teamId)
    {
        if (localPlayerId != -1) return;

        _LogInfo("joining team " + teamId);

        localPlayerId = networkingManager._OnJoinTeam(teamId);
        if (localPlayerId != -1)
        {
            localTeamId = (uint)(localPlayerId & 0x1u);

            playerNamesLocal[localPlayerId] = Networking.LocalPlayer.displayName;
            menuManager._RefreshLobbyOpen();
            menuManager._RefreshPlayerList();
        }
        else
        {
            _LogWarn("failed to join team " + teamId + ", did someone else beat you to it?");
        }
    }

    public void _TriggerLeaveLobby()
    {
        if (localPlayerId == -1) return;

        _LogInfo("leaving lobby");
        
        networkingManager._OnLeaveLobby(localPlayerId);
        playerNamesLocal[localPlayerId] = "";
        localPlayerId = -1;
        localTeamId = 0;
        menuManager._RefreshLobbyOpen();
        menuManager._RefreshPlayerList();
    }

    public void _TriggerGameReset()
    {
        string self = Networking.LocalPlayer.displayName;

        if (!gameLive)
        {
            if (lobbyOpen && _IsModerator(Networking.LocalPlayer))
            {
                networkingManager._OnLobbyClosed();
            }
            return;
        }

        string[] allowedPlayers = playerNamesLocal;
        if (!string.IsNullOrEmpty(tournamentRefereeLocal))
        {
            allowedPlayers = new string[] { tournamentRefereeLocal };
        }

        bool allPlayersOffline = true;
        bool isAllowedPlayer = false;
        foreach (string allowedPlayer in allowedPlayers)
        {
            if (allPlayersOffline && _GetPlayerByName(allowedPlayer) != null) allPlayersOffline = false;

            if (allowedPlayer == self) isAllowedPlayer = true;
        }

        if (allPlayersOffline || isAllowedPlayer || _IsModerator(Networking.LocalPlayer))
        {
            _LogInfo("force resetting game");

            networkingManager._OnGameReset();
        }
        else
        {
            string playerStr = "";
            bool has = false;
            foreach (string allowedPlayer in allowedPlayers)
            {
                if (string.IsNullOrEmpty(allowedPlayer)) continue;
                if (has) playerStr += ", ";
                has = true;

                playerStr += graphicsManager._FormatName(allowedPlayer);
            }

            infReset.text = "Only these players may reset:\n" + playerStr;
        }
    }
    #endregion

    #region NetworkingClient
    // the order is important, unfortunately
    public void _OnRemoteDeserialization()
    {
        _LogInfo("processing latest remote state (packet=" + networkingManager.packetIdSynced + ", state=" + networkingManager.stateIdSynced + ")");
        Debug.Log("[BilliardsModule] latest game state is " + networkingManager._EncodeGameState());

        // propagate game settings first
        onRemoteGlobalSettingsUpdated(
            networkingManager.tournamentRefereeSynced,
            networkingManager.tableModelSynced,
            networkingManager.tableSkinSynced
        );
        onRemoteGameSettingsUpdated(
            networkingManager.gameModeSynced,
            networkingManager.timerSynced,
            networkingManager.teamsSynced,
            networkingManager.noGuidelineSynced,
            networkingManager.noLockingSynced
        );

        // propagate valid players second
        onRemotePlayersChanged(networkingManager.playerNamesSynced);

        // apply state transitions if needed
        onRemoteGameStateChanged(networkingManager.gameStateSynced);

        // now update game state
        onRemoteBallPositionsChanged(networkingManager.ballsPSynced);
        onRemoteTeamIdChanged(networkingManager.teamIdSynced);
        onRemoteFourBallCueBallChanged(networkingManager.fourBallCueBallSynced);
        onRemoteBallsPocketedChanged(networkingManager.ballsPocketedSynced);
        onRemoteFourBallScoresUpdated(networkingManager.fourBallScoresSynced);
        onRemoteRepositionStateChanged(networkingManager.repositionStateSynced);
        onRemoteIsTableOpenChanged(networkingManager.isTableOpenSynced, networkingManager.teamColorSynced);
        onRemoteTurnStateChanged(networkingManager.turnStateSynced);
        onRemotePreviewWinningTeamChanged(networkingManager.previewWinningTeamSynced);

        // finally, take a snapshot
        practiceManager._Record();

        redrawDebugger();
    }

    private void onRemoteGlobalSettingsUpdated(string tournamentRefereeSynced, byte tableModelSynced, byte tableSkinSynced)
    {
        if (gameLive) return;

        if (
            tournamentRefereeLocal == tournamentRefereeSynced &&
            tableModelLocal == tableModelSynced &&
            tableSkinLocal == tableSkinSynced
        )
        {
            return;
        }
        _LogInfo($"onRemoteGlobalSettingsUpdated tournamentReferee={tournamentRefereeSynced} tableModel={tableModelSynced} tableSkin={tableSkinSynced}");

        if (tournamentRefereeLocal != tournamentRefereeSynced)
        {
            tournamentRefereeLocal = tournamentRefereeSynced;
        }

        if (tableModelLocal != tableModelSynced)
        {
            setTableModel(tableModelSynced, true);
        }

        if (tableSkinLocal != tableSkinSynced)
        {
            tableSkinLocal = tableSkinSynced;
            graphicsManager._UpdateTableColorScheme();
        }
    }

    private void onRemoteGameSettingsUpdated(uint gameModeSynced, uint timerSynced, bool teamsSynced, bool noGuidelineSynced, bool noLockingSynced)
    {
        if (gameModeLocal == gameModeSynced &&
            timerLocal == timerSynced &&
            teamsLocal == teamsSynced &&
            noGuidelineLocal == noGuidelineSynced &&
            noLockingLocal == noLockingSynced) return;

        _LogInfo($"onRemoteGameSettingsUpdated gameMode={gameModeSynced} timer={timerSynced} teams={teamsSynced} guideline={!noGuidelineSynced} locking={!noLockingSynced}");

        if (gameModeLocal != gameModeSynced)
        {
            gameModeLocal = gameModeSynced;

            is8Ball = gameModeLocal == 0u;
            is9Ball = gameModeLocal == 1u;
            isJp4Ball = gameModeLocal == 2u;
            isKr4Ball = gameModeLocal == 3u;
            isSnooker6Red = gameModeLocal == 4u;
            is4Ball = isJp4Ball || isKr4Ball;

            menuManager._RefreshGameMode();
        }

        if (timerLocal != timerSynced)
        {
            timerLocal = timerSynced;

            menuManager._RefreshTimer();
        }

        bool refreshToggles = false;
        setToggle(ref teamsLocal, teamsSynced, ref refreshToggles);
        setToggle(ref noGuidelineLocal, noGuidelineSynced, ref refreshToggles);
        setToggle(ref noLockingLocal, noLockingSynced, ref refreshToggles);

        if (refreshToggles)
            menuManager._RefreshToggleSettings();
    }
    private void setToggle(ref bool localBool, bool syncedBool, ref bool refreshToggles )
    {
        if (localBool == syncedBool) return;

        localBool = syncedBool;
        refreshToggles = true;
    }
    private void onRemotePlayersChanged(string[] playerNamesSynced)
    {
        if (stringArrayEquals(playerNamesCached, playerNamesSynced)) return;
        Array.Copy(playerNamesSynced, playerNamesCached, playerNamesCached.Length);

        string[] playerDetails = new string[4];
        for (int i = 0; i < 4; i++)
            playerDetails[i] = playerNamesSynced[i] == "" ? "none" : playerNamesSynced[i];

        _LogInfo($"onRemotePlayersChanged newPlayers={string.Join(",", playerDetails)}");
        
        Array.Copy(playerNamesSynced, playerNamesLocal, playerNamesLocal.Length);

        localPlayerId = Array.IndexOf(playerNamesLocal, Networking.LocalPlayer.displayName);
        if (localPlayerId != -1) localTeamId = (uint)(localPlayerId & 0x1u);

        cueControllers[0]._SetAuthorizedOwners(new string[] { playerNamesLocal[0], playerNamesLocal[2] });
        cueControllers[1]._SetAuthorizedOwners(new string[] { playerNamesLocal[1], playerNamesLocal[3] });

        menuManager._RefreshLobbyOpen();
        menuManager._RefreshPlayerList();
    }

    private void onRemoteGameStateChanged(byte gameStateSynced)
    {
        if (gameStateLocal == gameStateSynced) return;

        gameStateLocal = gameStateSynced;
        _LogInfo($"onRemoteGameStateChanged newState={gameStateSynced}");
        switch (gameStateSynced)
        {
            case 0:
                onRemoteLobbyClosed();
                break;
            case 1:
                onRemoteLobbyOpened();
                break;
            case 2:
                onRemoteGameStarted();
                break;
            case 3:
                onRemoteGameEnded(networkingManager.winningTeamSynced);
                break;
        }
    }

    private void onRemoteLobbyOpened()
    {
        _LogInfo($"onRemoteLobbyOpened");

        lobbyOpen = true;
        graphicsManager._OnLobbyOpened();
        menuManager._RefreshLobbyOpen();
        menuManager._RefreshPlayerList();

        if (callbacks != null) callbacks.SendCustomEvent("_OnLobbyOpened");
    }

    private void onRemoteLobbyClosed()
    {
        _LogInfo($"onRemoteLobbyClosed");

        lobbyOpen = false;
        localPlayerId = -1;
        graphicsManager._OnLobbyClosed();
        menuManager._RefreshLobbyOpen();

        resetCachedData();

        if (callbacks != null) callbacks.SendCustomEvent("_OnLobbyClosed");
    }

    private void onRemoteGameStarted()
    {
        _LogInfo($"onRemoteGameStarted");

        lobbyOpen = false;
        gameLive = true;

        Array.Clear(perfCounters, 0, PERF_MAX);
        Array.Clear(perfStart, 0, PERF_MAX);
        Array.Clear(perfTimings, 0, PERF_MAX);

        isPracticeMode = playerNamesLocal[1] == "" && playerNamesLocal[3] == "";

        menuManager._DisableMenu();

        graphicsManager._OnGameStarted();
        desktopManager._OnGameStarted();
        applyCueAccess(false);
        practiceManager._Clear();
        repositionManager._OnGameStarted();
        if (isPracticeMode)
            cueControllers[1].gameObject.SetActive(false);

        Array.Clear(fbScoresLocal, 0, 2);
        auto_pocketblockers.SetActive(is4Ball);
        marker9ball.SetActive(is9Ball);

        graphicsManager._ShowBalls();

        // Reflect game state
        graphicsManager._UpdateScorecard();
        isReposition = false;
        markerObj.SetActive(false);

        // Effects
        graphicsManager._PlayIntroAnimation();
        aud_main.PlayOneShot(snd_Intro, 1.0f);

        graphicsManager._SetScorecardPlayers(playerNamesLocal);

        timerRunning = false;

        reflection_main.RenderProbe();

        activeCue = cueControllers[0];
    }

    private void onRemoteBallPositionsChanged(Vector3[] ballsPSynced)
    {
        if (vector3ArrayEquals(ballsP, ballsPSynced)) return;

        _LogInfo($"onRemoteBallPositionsChanged");

        Array.Copy(ballsPSynced, ballsP, ballsP.Length);
    }


    private void onRemotePreviewWinningTeamChanged(uint previewWinningTeamSynced)
    {
        if (!gameLive) return;
        if (string.IsNullOrEmpty(tournamentRefereeLocal)) return;

        if (previewWinningTeamLocal == previewWinningTeamSynced) return;

        _LogInfo($"onRemotePreviewWinningTeamChanged winningTeam={previewWinningTeamSynced}");
        previewWinningTeamLocal = previewWinningTeamSynced;

        if (previewWinningTeamSynced == 2)
            graphicsManager._ResetWinners();
        else
            graphicsManager._SetWinners(isPracticeMode ? 0u : previewWinningTeamSynced, playerNamesLocal);
    }

    private void onRemoteGameEnded(uint winningTeamSynced)
    {
        _LogInfo($"onRemoteGameEnded winningTeam={winningTeamSynced}");

        isLocalSimulationRunning = false;

        if (!string.IsNullOrEmpty(tournamentRefereeLocal))
        {
            // tournament mode has some special logic
            if (winningTeamSynced != 2u)
            {
                return;
            }

            winningTeamLocal = previewWinningTeamLocal;
        }
        else
        {
            winningTeamLocal = winningTeamSynced;
        }

        if (winningTeamLocal == 2)
        {
            winningTeamLocal = 0;

            isTableOpenLocal = true;
            _LogWarn("game reset");
            graphicsManager._OnGameReset();
        }
        else
        {
            _LogWarn("game over, team " + winningTeamLocal + " won (" + playerNamesLocal[winningTeamLocal] + " and " + playerNamesLocal[winningTeamLocal + 2] + ")");
            graphicsManager._SetWinners(isPracticeMode ? 0u : winningTeamLocal, playerNamesLocal);
        }

        gameLive = false;

        graphicsManager._UpdateTeamColor(winningTeamSynced);
        graphicsManager._UpdateScorecard();
        graphicsManager._RackBalls();

        disablePlayComponents();

        this.transform.Find("intl.controls/undo").gameObject.SetActive(false);
        this.transform.Find("intl.controls/redo").gameObject.SetActive(false);
        this.transform.Find("intl.controls/skipturn").gameObject.SetActive(false);

        // Remove any access rights
        localPlayerId = -1;
        localTeamId = 0;
        applyCueAccess(true);

        resetCachedData();

        cueControllers[1].gameObject.SetActive(true);

        menuManager._EnableMenu();

        infReset.text = "Reset";
    }

    private void onRemoteBallsPocketedChanged(uint ballsPocketedSynced)
    {
        if (!gameLive) return;

        // todo: actually use a separate variable to track local modifications to balls pocketed
        if (ballsPocketedLocal != ballsPocketedSynced) _LogInfo($"onRemoteBallsPocketedChanged ballsPocketed={ballsPocketedSynced:X}");

        ballsPocketedLocal = ballsPocketedSynced;

        graphicsManager._UpdateScorecard();
        graphicsManager._RackBalls();

        refreshBallPickups();
    }

    private void onRemoteFourBallScoresUpdated(int[] fbScoresSynced)
    {
        if (!gameLive) return;

        if (fbScoresLocal[0] == fbScoresSynced[0] && fbScoresLocal[1] == fbScoresSynced[1]) return;

        _LogInfo($"onRemoteFourBallScoresUpdated team1={fbScoresSynced[0]} team2={fbScoresSynced[1]}");

        Array.Copy(fbScoresSynced, fbScoresLocal, 2);
        graphicsManager._UpdateScorecard();
    }

    private void onRemoteTeamIdChanged(uint teamIdSynced)
    {
        if (!gameLive) return;

        if (teamIdLocal == teamIdSynced) return;

        _LogInfo($"onRemoteTeamIdChanged newTeam={teamIdSynced}");
        teamIdLocal = teamIdSynced;

        aud_main.PlayOneShot(snd_NewTurn, 1.0f);

        graphicsManager._UpdateTeamColor(teamIdLocal);

        // always use first cue if practice mode
        activeCue = cueControllers[isPracticeMode ? 0 : (int)teamIdLocal];
    }

    private void onRemoteFourBallCueBallChanged(uint fourBallCueBallSynced)
    {
        if (!gameLive) return;
        if (!is4Ball) return;

        if (fourBallCueBallLocal == fourBallCueBallSynced) return;

        _LogInfo($"onRemoteFourBallCueBallChanged cueBall={fourBallCueBallSynced}");
        fourBallCueBallLocal = fourBallCueBallSynced;

        graphicsManager._UpdateFourBallCueBallTextures(fourBallCueBallLocal);
    }

    private void onRemoteIsTableOpenChanged(bool isTableOpenSynced, uint teamColorSynced)
    {
        if (!gameLive) return;

        if (teamColorLocal == teamColorSynced && isTableOpenLocal == isTableOpenSynced) return;

        _LogInfo($"onRemoteIsTableOpenChanged isTableOpen={isTableOpenSynced} teamColor={teamColorSynced}");
        isTableOpenLocal = isTableOpenSynced;
        teamColorLocal = teamColorSynced;

        if (!isTableOpenLocal)
        {
            string color = (teamIdLocal ^ teamColorLocal) == 0 ? "blues" : "oranges";
            _LogInfo($"table closed, team {teamIdLocal} is {color}");
        }

        graphicsManager._UpdateTeamColor(teamIdLocal);
        graphicsManager._UpdateScorecard();
    }
    private void onRemoteColorTurnChanged(bool ColorTurnSynced)
    {
        if (!gameLive) return;

        if (colorTurnLocal == ColorTurnSynced) return;

        _LogInfo($"onRemoteColorTurnChanged colorTurn={ColorTurnSynced}");
        colorTurnLocal = ColorTurnSynced;
    }
    private void onRemoteRepositionStateChanged(uint repositionStateSynced)
    {
        if (!gameLive) return;

        if (repositionStateLocal == repositionStateSynced) return;

        _LogInfo($"onRemoteRepositionStateChanged repositionState={repositionStateSynced}");
        repositionStateLocal = repositionStateSynced;

        if (!isOurTurn() || repositionStateLocal == 0)
        {
            isReposition = false;
            setFoulPickupEnabled(false);
            return;
        }

        if (repositionStateLocal == 1 || repositionStateLocal == 2)
        {
            isReposition = true;
            if (repositionStateLocal == 1)
            {
                repoMaxX = -k_SPOT_POSITION_X;
            }
            else
            {
                Vector3 k_pR = (Vector3)currentPhysicsManager.GetProgramVariable("k_pR");
                repoMaxX = k_pR.x;
            }
            setFoulPickupEnabled(true);
        }
    }

    private void onRemoteTurnBegin(int timerStartSynced)
    {
        _LogInfo("onRemoteTurnBegin");
        canPlayLocal = true;
        timerStartLocal = timerStartSynced;

        enablePlayComponents();
        Array.Clear(ballsV, 0, ballsV.Length);
        Array.Clear(ballsW, 0, ballsW.Length);
    }

    private void onRemoteTurnSimulate(Vector3 cueBallV, Vector3 cueBallW, string simulationOwner)
    {
        _LogInfo($"onRemoteTurnSimulate cueBallV={cueBallV.ToString("F4")} cueBallW={cueBallW.ToString("F4")} owner={simulationOwner}");

        balls[0].GetComponent<AudioSource>().PlayOneShot(snd_hitball, 1.0f);

        canPlayLocal = false;
        disablePlayComponents();

        if (!_IsPlayer(Networking.LocalPlayer) && !table.GetComponent<MeshRenderer>().isVisible)
        {
            // don't bother simulating if the table isn't even visible
            _LogWarn("skipping simulation");
            return;
        }

        isLocalSimulationRunning = true;
        firstHit = 0;
        secondHit = 0;
        thirdHit = 0;
        fbMadePoint = false;
        fbMadeFoul = false;
        ballsPocketedOrig = ballsPocketedLocal;
        if (Networking.LocalPlayer.displayName == simulationOwner)
        {
            isLocalSimulationOurs = true;
        }

        for (int i = 0; i < ballsV.Length; i++)
        {
            ballsV[i] = Vector3.zero;
            ballsW[i] = Vector3.zero;
        }
        ballsV[0] = cueBallV;
        ballsW[0] = cueBallW;

        auto_colliderBaseVFX.SetActive(true);
    }

    private void onRemoteTurnStateChanged(byte turnStateSynced)
    {
        if (!gameLive) return;

        if (turnStateSynced == turnStateLocal) return;

        _LogInfo($"onRemoteTurnStateChanged newState={turnStateSynced}");
        turnStateLocal = turnStateSynced;

        if (turnStateLocal == 0 || turnStateLocal == 2)
        {
            if (turnStateLocal == 2) turnStateLocal = 0; // synthetic state

            onRemoteTurnBegin(networkingManager.timerStartSynced);
            // practiceManager._Record();
        }
        else if (turnStateLocal == 1)
        {
            onRemoteTurnSimulate(networkingManager.cueBallVSynced, networkingManager.cueBallWSynced, networkingManager.simulationOwnerSynced);
            // practiceManager._Record();
        }
        else
        {
            canPlayLocal = false;
            disablePlayComponents();
        }
    }
    #endregion

    #region PhysicsEngineCallbacks
    public void _TriggerCollision(int srcId, int dstId)
    {
        if (dstId < srcId)
        {
            int tmp = dstId;
            dstId = srcId;
            srcId = dstId;
        }
        if (srcId != 0) return;

        switch (gameModeLocal)
        {
            case 0:
            case 1:
                if (firstHit == 0) firstHit = dstId;
                break;
            case 2:
                if (firstHit == 0)
                {
                    firstHit = dstId;
                    break;
                }
                if (secondHit == 0)
                {
                    if (dstId != firstHit)
                    {
                        secondHit = dstId;
                        handle4BallHit(ballsP[dstId], true);
                    }
                    break;
                }
                if (thirdHit == 0)
                {
                    if (dstId != firstHit && dstId != secondHit)
                    {
                        thirdHit = dstId;
                        handle4BallHit(ballsP[dstId], true);
                    }
                    break;
                }
                break;
            case 3:
                if (dstId == 13)
                {
                    handle4BallHit(ballsP[dstId], false);
                    break;
                }
                if (firstHit == 0)
                {
                    firstHit = dstId;
                    break;
                }
                if (secondHit == 0)
                {
                    if (dstId != firstHit)
                    {
                        secondHit = dstId;
                        handle4BallHit(ballsP[dstId], true);
                    }
                    break;
                }
                break;
            case 4:
                if (firstHit == 0)
                    firstHit = dstId;
                break;
        }
    }

    public void _TriggerPocketBall(int id)
    {
        uint total = 0U;

        // Get total for X positioning
        int count_extent = is9Ball ? 10 : (isSnooker6Red ? 13: 16);
        for (int i = 1; i < count_extent; i++)
        {
            total += (ballsPocketedLocal >> i) & 0x1U;
        }

        // place ball on the rack
        ballsP[id] = k_rack_position + (float)total * k_BALL_DIAMETRE * k_rack_direction;

        ballsPocketedLocal ^= 1U << id;

        uint bmask = 0x1FCU << ((int)(teamIdLocal ^ teamColorLocal) * 7);

        if (isSnooker6Red)
        {
            bool redOnTable = sixRedCheckIfRedOnTable(ballsPocketedOrig);
            bool foulCondition = false;
            int pocketedBallTypes = sixRedCheckBallTypesPocketed(ballsPocketedOrig, ballsPocketedLocal);
            int nextcolor = sixRedFindLowestUnpocketedColor(ballsPocketedOrig);
            bmask = colorTurnLocal ? (!redOnTable ? (uint)(1 << break_order_sixredsnooker[nextcolor]) : 0x1AE) : (!redOnTable ? (uint)(1 << break_order_sixredsnooker[nextcolor]) : 0x1E50u);
            foulCondition = ((redOnTable && pocketedBallTypes == 0 && colorTurnLocal) ||
                (redOnTable && pocketedBallTypes > 0 && !colorTurnLocal) ||
                (!redOnTable && firstHit != break_order_sixredsnooker[nextcolor]) ||
                (!redOnTable && (ballsPocketedOrig & 0x1AE) < (ballsPocketedLocal & (0x1AE - bmask))));
            if (!foulCondition)
                graphicsManager._FlashTableLight();
            else
                graphicsManager._FlashTableError();
        }
        else
        {
            if (((0x1U << id) & ((bmask) | (isTableOpenLocal ? 0xFFFCU : 0x0000U) | ((bmask & ballsPocketedLocal) == bmask ? 0x2U : 0x0U))) > 0)
            {
                graphicsManager._FlashTableLight();
            }
            else
            {
                graphicsManager._FlashTableError();
            }
        }
        aud_main.PlayOneShot(snd_Sink, 1.0f);

#if !HT_QUEST

        // VFX ( make ball move )
        Rigidbody body = balls[id].GetComponent<Rigidbody>();
        body.isKinematic = false;
        body.velocity = this.transform.TransformVector(new Vector3(
           ballsV[id].x,
           0.0f,
           ballsV[id].z
        ));

#else
        balls[id].transform.localPosition = ballsP[id];
#endif
    }

    public void _TriggerSimulationEnded(bool forceScratch)
    {
        if (!isLocalSimulationRunning) return;
        isLocalSimulationRunning = false;

        _LogInfo("local simulation completed");
        cameraManager._OnLocalSimEnd();
        isBreak = false;
        auto_colliderBaseVFX.SetActive(false);

        // Make sure we only run this from the client who initiated the move
        if (isLocalSimulationOurs)
        {
            isLocalSimulationOurs = false;

            uint bmask = 0xFFFCu;
            uint emask = 0x0u;

            // Quash down the mask if table has closed
            if (!isTableOpenLocal)
            {
                bmask = bmask & (0x1FCu << ((int)(teamIdLocal ^ teamColorLocal) * 7));
                emask = 0x1FCu << ((int)(teamIdLocal ^ teamColorLocal ^ 0x1U) * 7);
            }

            // Common informations
            bool isSetComplete = (ballsPocketedLocal & bmask) == bmask;
            bool isScratch = (ballsPocketedLocal & 0x1U) == 0x1U || forceScratch;

            ballsPocketedLocal = ballsPocketedLocal & ~(0x1U);
            if (isScratch) ballsP[0] = Vector3.zero;
            // Append black to mask if set is done
            if (isSetComplete)
            {
                bmask |= 0x2U;
            }

            // These are the resultant states we can set for each mode
            // then the rest is taken care of
            bool
               isObjectiveSink,
               isOpponentSink,
               winCondition,
               foulCondition,
               deferLossCondition;

            if (is8Ball)
            {
                isObjectiveSink = (ballsPocketedLocal & bmask) > (ballsPocketedOrig & bmask);
                isOpponentSink = (ballsPocketedLocal & emask) > (ballsPocketedOrig & emask);

                // Calculate if objective was not hit first
                bool isWrongHit = ((0x1U << firstHit) & bmask) == 0;
                bool is8BallOnTable = (ballsPocketedLocal & 0x2U) == 0;
                bool is8Sink = (ballsPocketedLocal & 0x2U) == 0x2U;

                if (is8Sink && isPracticeMode)
                {
                    is8Sink = false;

                    ballsPocketedLocal = ballsPocketedLocal & ~(0x2U);
                    ballsP[1] = Vector3.zero;
                }

                winCondition = isSetComplete && is8Sink;
                foulCondition = isScratch || isWrongHit;

                deferLossCondition = is8Sink;
            }
            else if (is9Ball)
            {
                // Rules are from: https://www.youtube.com/watch?v=U0SbHOXCtFw

                // Rule #1: Cueball must strike the lowest number ball, first
                bool isWrongHit = !(findLowestUnpocketedBall(ballsPocketedOrig) == firstHit);

                // Rule #2: Pocketing cueball, is a foul

                // Win condition: Pocket 9 ball ( at anytime )
                winCondition = (ballsPocketedLocal & 0x200u) == 0x200u;

                // this video is hard to follow so im just gonna guess this is right
                isObjectiveSink = (ballsPocketedLocal & 0x3FEu) > (ballsPocketedOrig & 0x3FEu);

                isOpponentSink = false;
                deferLossCondition = false;

                foulCondition = isWrongHit || isScratch;

                // TODO: Implement rail contact requirement
            }
            else if (is4Ball)
            {
                isObjectiveSink = fbMadePoint;
                isOpponentSink = fbMadeFoul;
                foulCondition = false;
                deferLossCondition = false;

                winCondition = fbScoresLocal[teamIdLocal] >= 10;
            }
            else /* if (isSnooker) */
            {
                bool redOnTable = sixRedCheckIfRedOnTable(ballsPocketedOrig),
                     redOnTableAndColorTurn = redOnTable && colorTurnLocal,
                     redOnTableOrColorTurn = redOnTable || colorTurnLocal,
                     allBallsPocketed = ((ballsPocketedLocal & 0x1FFEu) == 0x1FFEu),
                     myTeamWinning;

                int ballScore = 0,
                    numBallsPocketed = 0,
                    highestPocketedBallScore = 0,
                    firsthittype = sixRedCheckFirstHit(firstHit),
                    pocketedBallTypes = sixRedCheckBallTypesPocketed(ballsPocketedOrig, ballsPocketedLocal),
                    nextcolor = sixRedFindLowestUnpocketedColor(ballsPocketedOrig);

                uint objective = 0x1E50u;

                isOpponentSink = false;

                objective = redOnTableAndColorTurn ? 0x1AE : (!redOnTableOrColorTurn ?
                    (uint)(1 << break_order_sixredsnooker[nextcolor]) : (colorTurnLocal ?
                    0x1AE : objective));

                isObjectiveSink = (ballsPocketedLocal & (objective)) > (ballsPocketedOrig & (objective));
                sixRedScoreBallsPocketed(ref ballScore, ref numBallsPocketed, ref highestPocketedBallScore);

                foulCondition = (redOnTable && firsthittype == 0 && colorTurnLocal) ||
                                (redOnTable && firsthittype == 1 && !colorTurnLocal) ||
                                (redOnTable && pocketedBallTypes == 0 && colorTurnLocal) ||
                                (redOnTable && pocketedBallTypes > 0 && !colorTurnLocal) ||
                                (!redOnTable && firstHit != break_order_sixredsnooker[nextcolor] && !colorTurnLocal) ||
                                (!redOnTable && (ballsPocketedOrig & 0x1AE) < (ballsPocketedLocal & (0x1AE - objective)) && !colorTurnLocal) ||
                                (firsthittype == -1) ||
                                (isScratch);

                if (foulCondition)
                    fbScoresLocal[1 - teamIdLocal] += Math.Max(ballScore, 4);
                else
                    fbScoresLocal[teamIdLocal] += ballScore;

                if (redOnTableOrColorTurn || foulCondition)
                    sixRedReturnColoredBalls(foulCondition ? nextcolor : 6);

                if (isScratch)
                    ballsP[0] = initialPositions[4][0];

                colorTurnLocal = (redOnTable && isObjectiveSink && !foulCondition) ?
                    !colorTurnLocal : false;

                myTeamWinning = fbScoresLocal[teamIdLocal] > fbScoresLocal[1 - teamIdLocal];

                winCondition = myTeamWinning && allBallsPocketed;
                if (winCondition) foulCondition = false;
                deferLossCondition = allBallsPocketed && !myTeamWinning;

                _LogInfo($"6RED: TeamScore 0: {fbScoresLocal[0]}\n6RED: TeamScore 1: {fbScoresLocal[1]}");
            }
            networkingManager._OnSimulationEnded(ballsP, ballsPocketedLocal, fbScoresLocal, colorTurnLocal);

            if (winCondition || deferLossCondition)
                onLocalTeamWin(foulCondition || deferLossCondition ? teamIdLocal ^ 0x1U : teamIdLocal);
            else if (foulCondition)
                onLocalTurnFoul();
            else if (isObjectiveSink && !isOpponentSink)
                onLocalTurnContinue();
            else
                onLocalTurnPass();
        }
    }
    #endregion

    #region sixRedSnooker
    public string sixRedNumberToColor(int ball)
    {
        string[] ballColors = new string[]
        {
            "Yellow",
            "Green",
            "Brown",
            "Blue",
            "Pink",
            "Black",
            "Red"
        };
        return (ball - 6) >= 0 && (ball - 6) < ballColors.Length ? ballColors[ball - 6] : "Red";
    }
    public int sixRedFindLowestUnpocketedColor(uint field)
    {
        bool isLowestUnpocketed;
        for (int i = 6; i < break_order_sixredsnooker.Length; i++)
        {
            isLowestUnpocketed = ((field >> break_order_sixredsnooker[i]) & 0x1U) == 0x00U;
            if (isLowestUnpocketed)
                return i;
        }
        return -1;
    }
    public bool sixRedCheckIfRedOnTable(uint field)
    {
        bool isBallOntable;
        for (int i = 0; i < 6; i++)
        {
            isBallOntable = ((field >> break_order_sixredsnooker[i]) & 0x1U) == 0x00U;
            if (isBallOntable)
                return true;
        }
        return false;
    }
    public int sixRedCheckFirstHit(int firstHit)
    {
        uint firstHitball = (uint)1 << firstHit;
        bool firstHitRed = (firstHitball & 0x1E50u) > 0;
        bool firstHitColor = (firstHitball & 0x1AE) > 0;

        return firstHitRed ? 0 : firstHitColor ? 1 : -1;
    }
    public void sixRedReturnColoredBalls(int from)
    {
        bool isBallInBreakOrder;
        for (int i = from; i < break_order_sixredsnooker.Length; i++)
        {
            isBallInBreakOrder = (ballsPocketedLocal & (1 << break_order_sixredsnooker[i])) > 0;

            if (!isBallInBreakOrder)
                continue;

            sixRedMoveBallUntilNotTouching(break_order_sixredsnooker[i]);
            ballsPocketedLocal = ballsPocketedLocal ^ (1u << break_order_sixredsnooker[i]);
        }
    }
    public void sixRedScoreBallsPocketed(ref int ballscore, ref int numBallsPocketed, ref int highestScoringBall)
    {
        bool isBallPocketted;
        bool highestLessThanBallPoint;
        for (int i = 1; i < 13; i++)
        {
            isBallPocketted = (ballsPocketedLocal & (1 << i)) > (ballsPocketedOrig & (1 << i));

            if (!isBallPocketted)
                continue;

            highestLessThanBallPoint = highestScoringBall < sixredsnooker_ballpoints[i];

            if (highestLessThanBallPoint)
                highestScoringBall = sixredsnooker_ballpoints[i];

            ballscore += sixredsnooker_ballpoints[i];
            numBallsPocketed++;
        }
    }
    public int sixRedCheckBallTypesPocketed(uint ballsPocketedOrig, uint ballsPocketedLocal)
    {
        bool redBallPocket = (ballsPocketedOrig & 0x1E50u) < (ballsPocketedLocal & 0x1E50u);
        bool coloredBallPocket = (ballsPocketedOrig & 0x1AE) < (ballsPocketedLocal & 0x1AE);
        bool bothPocket = (coloredBallPocket && redBallPocket);

        return bothPocket ? 
          2 : (coloredBallPocket ?
          1 : (redBallPocket ? 
          0 : 
         -1 ));
    }
    private void sixRedMoveBallUntilNotTouching(int Ball)
    {
        int blockingBall = CheckIfBallTouchingBall(Ball);
        ballsP[Ball] = initialPositions[4][Ball];
        if (blockingBall < 0)
            return;

        for (int i = break_order_sixredsnooker.Length - 1; i > 5; i--)
        {
            ballsP[Ball] = initialPositions[4][break_order_sixredsnooker[i]];
            if (blockingBall < 0)
                return;
        }

        ballsP[Ball] = initialPositions[4][Ball];
        Vector3 moveDir = ballsP[Ball] - ballsP[blockingBall];
        moveDir.y = 0;
        if (moveDir.sqrMagnitude == 0)
            moveDir = Vector3.left;

        moveDir = moveDir.normalized;

        while (CheckIfBallTouchingBall(Ball) > 0)
            ballsP[Ball] += (moveDir * k_BALL_RADIUS * .051f);
    }
    private int CheckIfBallTouchingBall(int Input)
    {
        bool isNotCorrectBall,
             isBallTouching;

        float ballDiameter = k_BALL_RADIUS * 2f,
              k_BALL_DSQR = ballDiameter * ballDiameter;

        for (int i = 1; i < 16; i++)
        {
            isNotCorrectBall = ((ballsPocketedLocal >> i) & 0x1u) == 0x1u || i == Input;
            if (isNotCorrectBall)
                continue;

            isBallTouching = (ballsP[Input] - ballsP[i]).sqrMagnitude < k_BALL_DSQR;
            if (isBallTouching)
                return i;
        }
        return -1;
    }
    #endregion

    #region GameLogic
    private void initializeRack()
    {
        _RackStart();
        _8BallRackLogic();
        _9BallRackLogic();
        _4BallRackLogic(true);
        _4BallRackLogic(false);
        _SnookerRackLogic();
    }
    private void _RackStart()
    {
        for (int i = 0; i < 5; i++)
        {
            initialPositions[i] = new Vector3[16];
            for (int j = 0; j < 16; j++)
            {
                initialPositions[i][j] = Vector3.zero;
            }

            // cue ball always starts here (unless four ball, but we override below)
            initialPositions[i][0] = new Vector3(-k_SPOT_POSITION_X, 0.0f, 0.0f);
        }
    }
    private void _4BallRackLogic(bool isJP)
    {
        if (isJP)
        {
            initialBallsPocketed[2] = 0x1FFEu;
            initialPositions[2][0] = new Vector3(-k_SPOT_CAROM_X, 0.0f, 0.0f);
            initialPositions[2][13] = new Vector3(k_SPOT_CAROM_X, 0.0f, 0.0f);
            initialPositions[2][14] = new Vector3(k_SPOT_POSITION_X, 0.0f, 0.0f);
            initialPositions[2][15] = new Vector3(-k_SPOT_POSITION_X, 0.0f, 0.0f);
        }
        else
        {
            initialBallsPocketed[3] = initialBallsPocketed[2];
            initialPositions[3] = initialPositions[2];
        }
    }
    private void _8BallRackLogic()
    {
        // 8 ball
        initialBallsPocketed[0] = 0x00u;
        makeTriangle(k_SPOT_POSITION_X, 0, break_order_8ball, 5);
    }
    private void _9BallRackLogic()
    {
        // 9 ball
        initialBallsPocketed[1] = 0xFC00u;
        int rown;
        for (int i = 0, k = 0; i < 5; i++)
        {
            rown = break_rows_9ball[i];
            for (int j = 0; j <= rown; j++)
            {
                initialPositions[1][break_order_9ball[k++]] = new Vector3
                (
                   k_SPOT_POSITION_X + i * k_BALL_PL_Y + UnityEngine.Random.Range(-k_RANDOMIZE_F, k_RANDOMIZE_F),
                   0.0f,
                   (-rown + j * 2) * k_BALL_PL_X + UnityEngine.Random.Range(-k_RANDOMIZE_F, k_RANDOMIZE_F)
                );
            }
        }
    }
    private void _SnookerRackLogic()
    {
        isBreak = true;
        // Snooker
        initialBallsPocketed[4] = 0xE000u;

        initialPositions[4][1] = new Vector3(coloredPositions[0].localPosition.x, 0f, 0f);//black
        initialPositions[4][5] = new Vector3(coloredPositions[1].localPosition.x, 0f, 0f);//pink
        initialPositions[4][2] = new Vector3(coloredPositions[2].localPosition.x, 0f, coloredPositions[2].localPosition.z);//yellow
        initialPositions[4][7] = new Vector3(coloredPositions[3].localPosition.x, 0f, coloredPositions[3].localPosition.z);//green  
        initialPositions[4][8] = new Vector3(coloredPositions[4].localPosition.x, 0f, 0f);//brown
        initialPositions[4][0] = new Vector3(coloredPositions[5].localPosition.x, 0f, coloredPositions[5].localPosition.z); //cue
                                                                                                                            //triangle
        float rackStartSnooker = coloredPositions[6].localPosition.x;
        makeTriangle(rackStartSnooker, 4, break_order_sixredsnooker, 3);
    }
    private void makeTriangle(float initialPosition, int gameMode, int[] breakOrder, int rows)
    {
        for (int i = 0, k = 0; i < rows; i++)// change 3 to 5 for 15 balls (rows)
        {
            for (int j = 0; j <= i; j++)
            {
                initialPositions[gameMode][breakOrder[k++]] = new Vector3
                (
                   initialPosition + i * k_BALL_PL_Y,
                   0.0f,
                   (-i + j * 2) * k_BALL_PL_X
                );
            }
        }
    }
    private void resetCachedData()
    {
        for (int i = 0; i < 4; i++)
        {
            playerNamesLocal[i] = "";
        }
        repositionStateLocal = 0;
        gameModeLocal = uint.MaxValue;
        turnStateLocal = byte.MaxValue;
        previewWinningTeamLocal = 2;
    }

    private void setTransform(Transform src, Transform dest, float sf)
    {
        dest.position = src.position;
        dest.rotation = src.rotation;
        dest.localScale = src.localScale * sf;
    }

    private void setTableModel(int newTableModel, bool update)
    {
        tableModels[tableModelLocal].gameObject.SetActive(false);
        tableModels[newTableModel].gameObject.SetActive(true);

        tableModelLocal = newTableModel;

        ModelData data = tableModels[tableModelLocal];
        k_TABLE_WIDTH = data.tableWidth;
        k_TABLE_HEIGHT = data.tableHeight;
        k_CUSHION_RADIUS = data.cushionRadius;
        k_POCKET_RADIUS = data.pocketRadius;
        k_INNER_RADIUS = data.innerRadius;
        k_vE = data.cornerPocket;
        k_vF = data.sidePocket;
        pockets = data.pockets;

        Transform table_base = _GetTableBase().transform;
        auto_pocketblockers = table_base.Find(".4BALL_FILL").gameObject;
        auto_rackPosition = table_base.Find(".RACK").gameObject;
        auto_colliderBaseVFX = table_base.Find("collision.vfx").gameObject;

        Transform transformSurface = (Transform)currentPhysicsManager.GetProgramVariable("transform_Surface");
        k_rack_position = transformSurface.InverseTransformPoint(auto_rackPosition.transform.position);
        k_rack_direction = transformSurface.InverseTransformDirection(auto_rackPosition.transform.up);

        table = table_base.Find("table");
        if (update)
        {
            currentPhysicsManager.SendCustomEvent("_InitConstants");
            graphicsManager._InitializeTable();
        }

        Transform menu_transform = this.transform.Find("intl.menu");
        setTransform(table_base.Find(".MENU"), menu_transform, 1.0f);
        menu_transform.transform.position += menu_transform.right * 0.4f;

        Transform score_info_root = this.transform.Find("intl.scorecardinfo");
        setTransform(table_base.Find(".NAME_0"), score_info_root.Find("player0-name").gameObject.GetComponent<RectTransform>(), 1F / 200F);
        setTransform(table_base.Find(".NAME_1"), score_info_root.Find("player1-name").gameObject.GetComponent<RectTransform>(), 1F / 200F);

        // todo: reposition cues
    }

    public GameObject _GetTableBase()
    {
        return tableModels[tableModelLocal].transform.Find("table_artwork").gameObject;
    }

    private void handle4BallHit(Vector3 loc, bool good)
    {
        if (good)
            handle4BallHitGood(loc);
        else
            handle4BallHitBad(loc);
        graphicsManager._SpawnFourBallPoint(loc, good);
        graphicsManager._UpdateScorecard();
    }

    private void handle4BallHitGood(Vector3 p)
    {
        fbMadePoint = true;
        aud_main.PlayOneShot(snd_PointMade, 1.0f);

        fbScoresLocal[teamIdLocal]++;
        if (fbScoresLocal[teamIdLocal] > 10) fbScoresLocal[teamIdLocal] = 10;
    }

    private void handle4BallHitBad(Vector3 p)
    {
        if (fbMadeFoul) return;
        fbMadeFoul = true;

        fbScoresLocal[teamIdLocal]--;
        if (fbScoresLocal[teamIdLocal] < 0) fbScoresLocal[teamIdLocal] = 0;
    }

    private void onLocalTeamWin(uint winner)
    {
        _LogInfo($"onLocalTeamWin {(winner)}");

        if (string.IsNullOrEmpty(tournamentRefereeLocal))
            networkingManager._OnGameWin(winner);
        else
            networkingManager._OnPreviewWinner(winner);
    }

    private void onLocalTurnPass()
    {
        _LogInfo($"onLocalTurnPass");

        networkingManager._OnTurnPass(teamIdLocal ^ 0x1u);
    }

    private void onLocalTurnFoul()
    {
        _LogInfo($"onLocalTurnFoul");

        networkingManager._OnTurnFoul(teamIdLocal ^ 0x1u);
    }

    private void onLocalTurnContinue()
    {
        _LogInfo($"onLocalTurnContinue");

        // try and close the table if possible
        if (is8Ball && isTableOpenLocal)
        {
            uint sink_orange = 0;
            uint sink_blue = 0;
            uint pmask = ballsPocketedLocal >> 2;

            for (int i = 0; i < 7; i++)
            {
                if ((pmask & 0x1u) == 0x1u)
                    sink_blue++;

                pmask >>= 1;
            }
            for (int i = 0; i < 7; i++)
            {
                if ((pmask & 0x1u) == 0x1u)
                    sink_orange++;

                pmask >>= 1;
            }

            if (sink_blue != sink_orange)
            {
                teamColorLocal = (sink_blue > sink_orange) ? teamIdLocal : teamIdLocal ^ 0x1u;
                networkingManager._OnTableClosed(teamColorLocal);
            }
        }

        networkingManager._OnTurnContinue();
    }

    private void onLocalTimerEnd()
    {
        timerRunning = false;

        _LogWarn("out of time!");

        graphicsManager._HideTimers();

        if (string.IsNullOrEmpty(tournamentRefereeLocal))
        {
            // no one is allowed to play
            canPlayLocal = false;

            if (isOurTurn())
            {
                // everyone on the current team propagates the change
                onLocalTurnFoul();
            }
        }
    }

    private void applyCueAccess(bool gameOver)
    {
        switch (localPlayerId)
        {
            case -1:
                cueControllers[0]._Disable(gameOver);
                cueControllers[1]._Disable(gameOver);
                break;
            case 0:
                cueControllers[0]._Enable(gameOver);
                cueControllers[1]._Disable(gameOver);
                break;
            default:
                cueControllers[1]._Enable(gameOver);
                cueControllers[0]._Disable(gameOver);
                break;
        }
    }

    // turn on any game elements that are enabled when someone is taking a shot
    private void enablePlayComponents()
    {
        bool isOurTurnVar = isOurTurn();

        if ((isOurTurnVar && isPracticeMode) || (!string.IsNullOrEmpty(tournamentRefereeLocal) && _IsLocalPlayerReferee()))
        {
            this.transform.Find("intl.controls/undo").gameObject.SetActive(true);
            this.transform.Find("intl.controls/redo").gameObject.SetActive(true);
            this.transform.Find("intl.controls/skipturn").gameObject.SetActive(true);
        }

        if (is9Ball)
        {
            marker9ball.SetActive(true);
            _Update9BallMarker();
        }

        refreshBallPickups();

        if (isOurTurnVar)
        {
            // Update for desktop
            desktopManager._AllowShoot();
        }
        else
        {
            desktopManager._DenyShoot();
        }

        if (timerLocal > 0)
        {
            timerRunning = true;
            graphicsManager._ShowTimers();
        }
    }

    public void _SkipTurn()
    {
        if (isPracticeMode || (!string.IsNullOrEmpty(tournamentRefereeLocal) && _IsLocalPlayerReferee()))
        {
            onLocalTurnFoul();
        }
    }

    public void _Update9BallMarker()
    {
        if (marker9ball.activeSelf)
        {
            int target = findLowestUnpocketedBall(ballsPocketedLocal);
            marker9ball.transform.localPosition = ballsP[target];
        }
    }

    // turn off any game elements that are enabled when someone is taking a shot
    private void disablePlayComponents()
    {
        marker9ball.SetActive(false);
        setFoulPickupEnabled(false);
        refreshBallPickups();
        devhit.SetActive(false);
        guideline.SetActive(false);
        isGuidelineValid = false;
        isReposition = false;

        desktopManager._DenyShoot();
        graphicsManager._HideTimers();
    }

    public int findLowestUnpocketedBall(uint field)
    {
        for (int i = 2; i <= 8; i++)
        {
            if (((field >> i) & 0x1U) == 0x00U)
                return i;
        }

        if (((field) & 0x2U) == 0x00U)
            return 1;

        for (int i = 9; i < 16; i++)
        {
            if (((field >> i) & 0x1U) == 0x00U)
                return i;
        }

        // ??
        return 0;
    }

    private void setBallPickupActive(int ballId, bool active)
    {
        Transform pickup = balls[ballId].transform.GetChild(0);

        pickup.gameObject.SetActive(active);
        pickup.GetComponent<SphereCollider>().enabled = active;
        ((VRC_Pickup)pickup.GetComponent(typeof(VRC_Pickup))).pickupable = active;
        if (!active) ((VRC_Pickup)pickup.GetComponent(typeof(VRC_Pickup))).Drop();
    }

    private void refreshBallPickups()
    {
        bool canUsePickup = (isOurTurn() && isPracticeMode) || (!string.IsNullOrEmpty(tournamentRefereeLocal) && _IsLocalPlayerReferee());

        uint ball_bit = 0x1u;
        for (int i = 0; i < balls.Length; i++)
        {
            if (gameLive && (canUsePickup || (i == 0 && isReposition)) && canPlayLocal && (ballsPocketedLocal & ball_bit) == 0x0u)
            {
                setBallPickupActive(i, true);
            }
            else
            {
                setBallPickupActive(i, false);
            }
            ball_bit <<= 1;
        }
    }

    private void setFoulPickupEnabled(bool enabled)
    {
        markerObj.SetActive(enabled);
        if (enabled)
        {
            setBallPickupActive(0, true);
        }
        else if (!isPracticeMode && !(!string.IsNullOrEmpty(tournamentRefereeLocal) && _IsLocalPlayerReferee()))
        {
            setBallPickupActive(0, false);
        }
    }

    private void tickTimer()
    {
        if (gameLive && timerRunning && canPlayLocal)
        {
            float timeRemaining = timerLocal - (Networking.GetServerTimeInMilliseconds() - timerStartLocal) / 1000.0f;
            float timePercentage = timeRemaining >= 0.0f ? 1.0f - (timeRemaining / timerLocal) : 0.0f;

            graphicsManager._SetTimerPercentage(timePercentage);

            if (timeRemaining < 0.0f)
            {
                onLocalTimerEnd();
            }
        }
    }

    private bool isOurTurn()
    {
        return localPlayerId >= 0 && (localTeamId == teamIdLocal || isPracticeMode);
    }

    public bool _AllPlayersOffline()
    {
        for (int i = 0; i < 4; i++)
        {
            if (playerNamesLocal[i] == "") continue;

            VRCPlayerApi player = _GetPlayerByName(playerNamesLocal[i]);
            if (Utilities.IsValid(player))
                return false;
        }

        return true;
    }

    public VRCPlayerApi _GetPlayerByName(string name)
    {
        VRCPlayerApi[] onlinePlayers = VRCPlayerApi.GetPlayers(new VRCPlayerApi[VRCPlayerApi.GetPlayerCount()]);
        for (int playerId = 0; playerId < onlinePlayers.Length; playerId++)
            if (onlinePlayers[playerId].displayName == name)
                return onlinePlayers[playerId];
        return null;
    }

    public void _IndicateError()
    {
        graphicsManager._FlashTableColor(k_colour_foul);
    }

    public void _IndicateSuccess()
    {
        // nothing, for now
    }

    public string _SerializeGameState()
    {
        return networkingManager._EncodeGameState();
    }

    public void _LoadSerializedGameState(string gameState)
    {
        if (string.IsNullOrEmpty(tournamentRefereeLocal))
        {
            // no loading on top of other people's games
            if (!_IsPlayer(Networking.LocalPlayer)) return;

            // no loading outside of practice
            if (!isPracticeMode) return;
        }
        else
        {
            // only host can load on top of tournament
            if (!_IsLocalPlayerReferee()) return;
        }

        networkingManager._OnLoadGameState(gameState);
        // practiceManager._Record();
    }

    public object[] _SerializeInMemoryState()
    {
        Vector3[] positionClone = new Vector3[ballsP.Length];
        Array.Copy(ballsP, positionClone, ballsP.Length);
        int[] scoresClone = new int[fbScoresLocal.Length];
        Array.Copy(fbScoresLocal, scoresClone, fbScoresLocal.Length);
        return new object[14]
        {
            positionClone, ballsPocketedLocal, scoresClone, gameModeLocal, teamIdLocal, repositionStateLocal, isTableOpenLocal, teamColorLocal, fourBallCueBallLocal,
            turnStateLocal, networkingManager.cueBallVSynced, networkingManager.cueBallWSynced, networkingManager.previewWinningTeamSynced, colorTurnLocal
        };
    }

    public void _LoadInMemoryState(object[] state, int stateIdLocal)
    {
        networkingManager._ForceLoadFromState(
            stateIdLocal,
            (Vector3[])state[0], (uint)state[1], (int[])state[2], (uint)state[3], (uint)state[4], (uint)state[5], (bool)state[6], (uint)state[7], (uint)state[8],
            (byte)state[9], (Vector3)state[10], (Vector3)state[11], (byte)state[12], (bool)state[13]
        );
    }

    public bool _AreInMemoryStatesEqual(object[] a, object[] b)
    {
        Vector3[] posA = (Vector3[])a[0];
        Vector3[] posB = (Vector3[])b[0];
        for (int i = 0; i < ballsP.Length; i++) if (posA[i] != posB[i]) return false;

        int[] scoresA = (int[])a[2];
        int[] scoresB = (int[])b[2];
        for (int i = 0; i < fbScoresLocal.Length; i++) if (scoresA[i] != scoresB[i]) return false;

        for (int i = 0; i < a.Length; i++) if (i != 0 && i != 2 && !a[i].Equals(b[i])) return false;

        return true;
    }

    public bool _IsLocalPlayerReferee()
    {
        return _IsReferee(Networking.LocalPlayer);
    }

    public bool _IsModerator(VRCPlayerApi player)
    {
        return Array.IndexOf(moderators, player.displayName) != -1;
    }

    public bool _IsReferee(VRCPlayerApi player)
    {
        if (player == null) return false;

        if (string.IsNullOrEmpty(tournamentRefereeLocal)) return false;

        return player.displayName == tournamentRefereeLocal || _IsModerator(player);
    }

    public bool _IsPlayer(VRCPlayerApi who)
    {
        if (who == null) return false;
        if (who.isLocal && localPlayerId >= 0) return true;

        for (int i = 0; i < 4; i++)
        {
            if (playerNamesLocal[i] == who.displayName)
            {
                return true;
            }
        }

        return false;
    }

    private bool stringArrayEquals(string[] a, string[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i]) return false;
        }
        return true;
    }

    private bool intArrayEquals(int[] a, int[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i]) return false;
        }
        return true;
    }

    private bool vector3ArrayEquals(Vector3[] a, Vector3[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i]) return false;
        }
        return true;
    }
    #endregion

    #region Debugger
    const string LOG_LOW = "<color=\"#ADADAD\">";
    const string LOG_ERR = "<color=\"#B84139\">";
    const string LOG_WARN = "<color=\"#DEC521\">";
    const string LOG_YES = "<color=\"#69D128\">";
    const string LOG_END = "</color>";
#if HT8B_DEBUGGER
    public void _Log(string msg)
    {
        _log(LOG_WARN + msg + LOG_END);
    }
    public void _LogYes(string msg)
    {
        _log(LOG_YES + msg + LOG_END);
    }
    public void _LogWarn(string msg)
    {
        _log(LOG_WARN + msg + LOG_END);
    }
    public void _LogError(string msg)
    {
        _log(LOG_ERR + msg + LOG_END);
    }
    public void _LogInfo(string msg)
    {
        _log(LOG_LOW + msg + LOG_END);
    }
    public void _RedrawDebugger()
    {
        redrawDebugger();
    }
#else
public void _Log(string msg) { }
public void _LogYes(string msg) { }
public void _LogInfo(string msg) { }
public void _LogWarn(string msg) { }
public void _LogError(string msg) { }
public void _RedrawDebugger() { }
#endif

    public void _BeginPerf(int id)
    {
        perfStart[id] = Time.realtimeSinceStartup;
    }

    public void _EndPerf(int id)
    {
        perfTimings[id] += Time.realtimeSinceStartup - perfStart[id];
        perfCounters[id]++;
    }

    private void _log(string ln)
    {
        Debug.Log("[<color=\"#B5438F\">BilliardsModule</color>] " + ln);

        LOG_LINES[LOG_PTR++] = "[<color=\"#B5438F\">BilliardsModule</color>] " + ln + "\n";
        LOG_LEN++;

        if (LOG_PTR >= LOG_MAX)
        {
            LOG_PTR = 0;
        }

        if (LOG_LEN > LOG_MAX)
        {
            LOG_LEN = LOG_MAX;
        }

        redrawDebugger();
    }

    private void redrawDebugger()
    {
        string output = "BilliardsModule ";

        // Add information about game state:
        output += Networking.IsOwner(Networking.LocalPlayer, networkingManager.gameObject) ?
           "<color=\"#95a2b8\">net(</color> <color=\"#4287F5\">OWNER</color> <color=\"#95a2b8\">)</color> " :
           "<color=\"#95a2b8\">net(</color> <color=\"#678AC2\">RECVR</color> <color=\"#95a2b8\">)</color> ";

        output += isLocalSimulationRunning ?
           "<color=\"#95a2b8\">sim(</color> <color=\"#4287F5\">ACTIVE</color> <color=\"#95a2b8\">)</color> " :
           "<color=\"#95a2b8\">sim(</color> <color=\"#678AC2\">PAUSED</color> <color=\"#95a2b8\">)</color> ";

        VRCPlayerApi currentOwner = Networking.GetOwner(networkingManager.gameObject);
        output += "<color=\"#95a2b8\">owner(</color> <color=\"#4287F5\">" + (currentOwner != null ? currentOwner.displayName + ":" + currentOwner.playerId : "[null]") + "/" + teamIdLocal + "</color> <color=\"#95a2b8\">)</color> ";

        output += "\n---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------\n";

        for (int i = 0; i < PERF_MAX; i++)
        {
            output += "<color=\"#95a2b8\">" + perfNames[i] + "(</color> " + (perfCounters[i] > 0 ? perfTimings[i] * 1e6 / perfCounters[i] : 0).ToString("F2") + "µs <color=\"#95a2b8\">)</color> ";
        }

        output += "\n---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------\n";

        // Update display 
        for (int i = 0; i < LOG_LEN; i++)
        {
            output += LOG_LINES[(LOG_MAX + LOG_PTR - LOG_LEN + i) % LOG_MAX];
        }

        ltext.text = output;
    }
    #endregion
}
