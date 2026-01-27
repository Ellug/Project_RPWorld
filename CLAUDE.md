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
Title → (Login/SignUp) → Lobby → Stage
```
The game has 4 scenes in `Assets/_Scenes/`:
- **Title** - Entry point, authentication (Firebase Auth), Photon lobby connection
- **Loading** - Transition/loading screen
- **Lobby** - Pre-game lobby (after Photon lobby join)
- **Stage** - Main gameplay scene

### Script Organization
Scripts are in `Assets/_Scripts/` organized by scene/system:
```
_Scripts/
├── S_Title/
│   └── TitleManager.cs      # UI flow, login/signup, scene transition
├── S_Loading/
├── S_Lobby/
├── System/
│   ├── Singleton.cs         # Generic singleton base class
│   └── NetworkManager.cs    # Photon Fusion networking
└── Firebase/
    ├── AuthManager.cs       # Firebase Auth (singleton)
    └── FirestoreManager.cs  # Firestore DB (singleton)
```

### Singleton Pattern
All managers inherit from `Singleton<T>` (`Assets/_Scripts/System/Singleton.cs`):
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
- `JoinOrCreateRoom(roomName, sceneName)` - joins/creates a room
- `PlayerNickname` - stores the player's nickname for Photon
- Requires `NetworkRunner` prefab assigned in Inspector

Photon Fusion SDK location: `Assets/Photon/Fusion/`

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
- Scene-specific script folders use `S_` prefix (e.g., `S_Title`)
- Class names: PascalCase (e.g., `TitleManager`, `AuthManager`)
- Private fields: `_camelCase` with underscore prefix

### Async Patterns
- Firebase operations use `async/await`
- Use `TaskCompletionSource` for initialization waiting
- Always check `IsInitialized` before calling manager methods

### MonoBehaviour Pattern
- Scene managers handle per-scene logic (e.g., `TitleManager`)
- System managers are singletons that persist across scenes
- Use `[SerializeField]` for Inspector references

## Lobby 작업 (2026-01-27)
- Lobby 매니저/뷰 스크립트 추가 및 로비 기능 구현: 방 목록 갱신, 조인, 퀵조인, 방 만들기, 비밀번호 조인, 안내 문구 처리.
- 로비 UI 버튼 이벤트와 참조를 LobbyManager로 연결 (Lobby 씬 YAML에 직접 연결).
- RoomUnit Panel 기반으로 방 리스트 렌더링, 패스워드/호스트 정보 세션 프로퍼티 사용 (키: pw, host).
- NetworkManager에 세션 리스트 이벤트/캐시 및 커스텀 프로퍼티 포함 JoinOrCreateRoom 확장.
- AuthManager에 SignOut 추가 (로비 EXIT 시 연결 해제 및 데이터 정리).

### TODO
- Loading 씬 이후 Stage 씬 전환 로직 구체화 (현재는 Loading 씬 진입까지만 처리).
