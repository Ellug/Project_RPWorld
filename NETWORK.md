# NETWORK.md

이 문서는 최근 네트워크/맵 시스템 변경사항을 한국어로 요약합니다.

## 변경 요약
- **맵 동기화 방식 개선**: `[Networked] MapName`을 상태로 동기화하고 `FixedUpdateNetwork`에서 변경을 감지해 맵 로드를 트리거합니다. (Late Joiner 대응)
- **타일 룩업 동기화 개선**: `WorldTile`이 `Spawned/Despawned/OnDisable`에서 스스로 등록/해제하여 클라이언트의 `_tileLookup`도 채워집니다.
- **저장 포맷 개선**: 타일을 `Vector2Int key(x,y)` 기반으로 저장하고 `version=1` 추가. 레거시 `Position` 포맷 로드는 내부 변환 지원.
- **월드 바운더리 추가**: 타일 좌표 기준으로 맵 크기를 제한하고, 바운더리 밖 배치/삭제 및 프리뷰를 차단합니다.
- **맵 전환 권한/옵션**: 비호스트 맵 변경 요청 허용 여부, 맵 전환 전 자동 저장 옵션 추가.
- **맵 파일 목록 유틸**: `persistentDataPath/Maps`의 `*.json`을 읽어 맵 목록 자동 로드 가능.

## 적용된 파일
- `Assets/_Scripts/Map/WorldMapState.cs`
- `Assets/_Scripts/Map/WorldTile.cs`
- `Assets/_Scripts/Map/TilePlacementController.cs`
- `Assets/_Scripts/S_Stage/StageManager.cs`
- `Assets/_Scripts/Map/MapFileUtility.cs` (신규)

## 핵심 동작 상세

### 1) MapName 동기화 (Late Joiner 대응)
- `WorldMapState.MapName`을 `[Networked]`로 선언.
- `FixedUpdateNetwork()`에서 `MapName` 변경을 감지해 `LoadMapInternal()` 실행.
- 클라이언트가 늦게 입장해도 현재 `MapName`이 동기화되어 동일 맵을 로드합니다.

### 2) 타일 등록/해제 방식
- `WorldTile`이 `Spawned()`에서 `WorldMapState.RegisterTile(key, this)` 호출.
- `Despawned()` / `OnDisable()`에서 `UnregisterTile` 호출.
- 따라서 `_tileLookup`이 호스트/클라 모두에서 자동 채워집니다.

### 3) 저장/로드 포맷 (스파스 저장)
- 저장은 `WorldMapState._tileLookup`을 기준으로 **놓인 타일만** 저장합니다.
- 포맷 (v1):
  ```json
  {
    "MapName": "default",
    "Version": 1,
    "Tiles": [
      { "TileId": 0, "X": 10, "Y": -3 }
    ]
  }
  ```
- 레거시 포맷(`Position`)은 내부 변환 로직으로 v1로 변환해 로드합니다.

### 4) 월드 바운더리 (타일 좌표 기준)
- `WorldMapState` 인스펙터 필드:
  - `_mapWidthTiles` (기본 512)
  - `_mapHeightTiles` (기본 512)
  - `_originMode` (Center / BottomLeft)
- 바운더리 밖 클릭:
  - `RequestPlaceTile` / `RequestRemoveTile`에서 무시
  - `TilePlacementController` 프리뷰 비활성화

### 5) 맵 전환 권한/옵션
- `WorldMapState`:
  - `_allowNonHostMapSwitch`: 비호스트 맵 변경 요청 허용 여부
  - `_autoSaveBeforeMapSwitch`: 맵 변경 전 자동 저장
- `StageManager`:
  - `_allowClientsToRequestMapSwitch`가 켜져 있으면 클라도 PageUp/PageDown 요청 가능
  - 클라이언트는 `RequestChangeMap(mapName)`을 호출해 호스트에게 요청

### 6) 맵 목록 자동 로드 (옵션)
- `StageManager`에서 `_mapRotation`이 비어 있으면
  `MapFileUtility.GetMapNamesFromDisk()`로 자동 채움.

## 이벤트/플래그
- 로딩 중 플래그: `_isLoading`일 때 배치/삭제/저장 차단.
- 로드 이벤트:
  - `MapLoadStarted(mapName)`
  - `MapLoadCompleted(mapName, tileCount)`
- 바운더리 알림 이벤트:
  - `PlacementOutOfBounds(key)`

## 사용 팁
- 빠른 테스트: 호스트 1명 + 클라 1명으로 접속 후, 타일 배치/삭제가 즉시 동기화되는지 확인.
- Late Joiner 테스트: 클라가 늦게 입장했을 때 현재 맵이 자동 로드되는지 확인.
- 맵 전환 자동 저장을 끄고 싶으면 `_autoSaveBeforeMapSwitch = false`로 설정.

## TODO (성능 확장 포인트)
- 현재는 타일 1개 = NetworkObject 1개.
- 대규모 맵을 위해 **Chunk Mesh / Instancing** 기반으로 전환 필요.
