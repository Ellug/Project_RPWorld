# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Project_RPWorld is a C# Unity RPG game built on Unity 6 (6000.3.5f1) using the Universal Render Pipeline (URP 17.3.0). The project uses Firebase for backend services and Photon Fusion 2 for networking.

## Build & Development

This is a Unity project - there are no CLI build commands. Use Unity Editor for:
- **Build**: File > Build Settings > Build (Windows 64-bit target)
- **Play**: Press Play button or Ctrl+P in Unity Editor
- **Tests**: Window > General > Test Runner (uses com.unity.test-framework)

Opening the project:
- Open Unity Hub and add this project folder
- Solution file: `Project_RPWorld.slnx`

## Architecture

### Scene Flow
```
Title → (Login/SignUp) → Lobby → Loading → Stage
```
The game has 4 scenes in `Assets/_Scenes/`:
- **Title** - Entry point, authentication (Firebase Auth), Photon lobby connection
- **Lobby** - Room list, room creation/joining, quick start
- **Loading** - Transition screen (TODO: implement Stage transition)
- **Stage** - Main gameplay scene

### Script Organization
Scripts are in `Assets/_Scripts/` organized by scene/system:
- `S_Title/` - Title scene (TitleManager)
- `S_Lobby/` - Lobby scene (LobbyManager, RoomListView, CreateRoomPanelView, JoinPwPanelView, RoomUnitView, RoomListItemData)
- `S_Loading/` - Loading scene (LoadingManager - stub)
- `System/` - Cross-scene singletons (Singleton, NetworkManager)
- `Firebase/` - Firebase services (AuthManager, FirestoreManager)

### Manager/View Pattern
Scene UIs follow a Manager/View separation:
- **Manager** (e.g., `LobbyManager`) - Business logic, coordinates views, handles events
- **View** (e.g., `RoomListView`, `CreateRoomPanelView`) - UI rendering, receives callbacks from Manager
- Views call `SetLobbyManager(this)` or receive callbacks to communicate back

### Singleton Pattern
All system managers inherit from `Singleton<T>` (`Assets/_Scripts/System/Singleton.cs`):
- `DontDestroyOnLoad` enabled by default
- Access via `ManagerClass.Instance`
- Override `OnSingletonAwake()` for initialization

## Core Systems

### Authentication (Firebase)
**AuthManager** (`Assets/_Scripts/Firebase/AuthManager.cs`):
- Firebase Auth initialization with async waiting
- `WaitForInitialization()` - await before using auth methods
- `SignInAsync(email, password)` - returns FirebaseUser
- `SignUpAsync(email, password)` - returns FirebaseUser
- `SignOut()` - signs out and clears user data
- `SetUserData(uid, email, nickname)` - stores current user info
- Stores current user data: `CurrentUserUid`, `CurrentUserEmail`, `CurrentUserNickname`

**FirestoreManager** (`Assets/_Scripts/Firebase/FirestoreManager.cs`):
- `CreateUserDocument(uid, email, password, nickname)` - creates `users/{uid}` document
- `GetUserDocument(uid)` - returns `UserData` (Uid, Email, Nickname)

Firestore structure:
```
users/{uid}
├── email: string
├── password: string
└── nickname: string
```

### Networking (Photon Fusion 2)
**NetworkManager** (`Assets/_Scripts/System/NetworkManager.cs`):
- Implements `INetworkRunnerCallbacks`
- `JoinLobby(nickname)` - connects to Photon session lobby
- `JoinOrCreateRoom(roomName, sceneName)` - joins/creates a room (simple overload)
- `JoinOrCreateRoom(roomName, sceneName, sessionProperties, maxPlayers, isVisible, isOpen)` - full overload with custom session properties
- `Disconnect()` - shuts down runner and clears state
- `PlayerNickname` - stores the player's nickname for Photon
- `LastSessionList` - cached list of available sessions
- `SessionListUpdated` event - fires when session list changes (subscribe in OnEnable, unsubscribe in OnDisable)
- Requires `NetworkRunner` prefab assigned in Inspector

Session properties used:
- `pw` - room password (empty string = no password)
- `host` - host player's nickname

### Lobby System
**LobbyManager** (`Assets/_Scripts/S_Lobby/LobbyManager.cs`):
- Manages room list display, room creation, and joining
- Public methods for UI buttons: `OnClickLeaveToTitle()`, `OnClickOpenCreateRoom()`, `OnClickQuickStart()`, `OnClickRefresh()`
- `RequestCreateRoom(roomName, password)` - creates room with optional password
- `SubmitJoinPassword(password)` - validates password for protected rooms

**RoomListView** - Renders list of rooms using `RoomUnitView` prefabs
**RoomListItemData** - Data class wrapping `SessionInfo` with host name and password
**CreateRoomPanelView** / **JoinPwPanelView** - Modal panels for room creation and password entry

### Title Scene Flow
**TitleManager** (`Assets/_Scripts/S_Title/TitleManager.cs`):
1. Shows "Press Start" text (blinking)
2. Any key or mouse click → Show SignIn panel
3. SignIn/SignUp with Firebase Auth
4. On success → Join Photon lobby with nickname
5. On lobby join → Load "Lobby" scene

Features:
- Tab key navigation between input fields
- Enter key submits current form
- Async Firebase initialization waiting

## Input System
Uses Unity's new Input System. Input actions in `Assets/InputSystem_Actions.inputactions`:
- **Player map**: Move, Look, Attack, Interact (hold), Crouch, Jump, Sprint, Previous, Next
- **UI map**: Navigate, Submit, Cancel, Point, Click, etc.
- Supports: Keyboard&Mouse, Gamepad, Touch, Joystick, XR

## Key Dependencies

### Unity Packages (Packages/manifest.json)
- `com.unity.render-pipelines.universal` (17.3.0) - URP rendering
- `com.unity.inputsystem` (1.17.0) - New Input System
- `com.unity.ai.navigation` (2.0.9) - NavMesh/AI navigation
- `com.unity.timeline` (1.8.10) - Cutscenes/animation sequencing

### External SDKs (Assets/)
- **Firebase SDK 13.6.0** - Auth, Firestore, Database, Storage
  - Config: `Assets/google-services.json`
  - DLLs: `Assets/Firebase/Plugins/`
- **Photon Fusion 2** - Networking
  - Config: `Assets/Photon/Fusion/Resources/PhotonAppSettings.asset`
  - DLLs: `Assets/Photon/Fusion/Assemblies/`

## Conventions

### Naming
- Folders prefixed with underscore (`_Scripts`, `_Scenes`, `_Prefabs`) appear first in Unity
- Scene-specific script folders use `S_` prefix (e.g., `S_Title`, `S_Lobby`)
- Class names: PascalCase (e.g., `TitleManager`, `AuthManager`)
- Private fields: `_camelCase` with underscore prefix
- Session property keys: lowercase short strings (e.g., `pw`, `host`)

### Async Patterns
- Firebase operations use `async/await`
- Use `TaskCompletionSource` for initialization waiting
- Always check `IsInitialized` before calling manager methods

### MonoBehaviour Pattern
- Scene managers handle per-scene logic (e.g., `TitleManager`, `LobbyManager`)
- System managers are singletons that persist across scenes
- Use `[SerializeField]` for Inspector references
- Views use `AutoWireIfNeeded()` pattern for fallback reference finding

### UI Event Wiring
- UI button events are wired in scene YAML to Manager public methods (e.g., `OnClickRefresh`)
- Views receive callbacks via constructor/setter methods

## Current Development Status

### TODO
- Loading scene: Implement transition logic to Stage scene (currently stub only)
