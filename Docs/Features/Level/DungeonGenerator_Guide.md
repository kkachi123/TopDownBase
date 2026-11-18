# 던전 생성기 — 핵심 구조와 사용법

상위 문서: [레벨 분야 과정](Process.md) · 진행과 검증 기록: [자연환경 맵 과정](NatureEnvironment/Process.md) · 이전 벽 방식 기록: [던전 생성 프로토타입 과정](DungeonGenerationPrototype/Process.md)

보스 투기장 앞의 던전 구간을 손 배치 없이 만드는 프로토타입이다. 던전은 "불규칙 공터 + 굽은 길 + 자연물 경계"로 생성되고, 던전 끝의 포털로 보스 씬에 넘어가는 데까지 담당한다. 게임 플레이어·적·보상 로직과는 아직 연결되어 있지 않다.

2026-11-18에 벽으로 방을 나누던 방식을 이 자연환경 방식으로 대체했다(개발자 결정). 벽 방식의 코드는 Git 기록에만 남아 있다.

## 1. 한눈에 보기

```text
시드 ─▶ ① 방 배치 ─▶ ② 걸을 수 있는 영역 ─▶ ③ 지면·충돌·소품 ─▶ ④ 장애물 ─▶ ⑤ 방·스폰 ─▶ ⑥ 포털·열쇠 ─▶ ⑦ 입구 잠금 ─▶ ⑧ NavMesh
        (그래프)    (공터·길·입구·스폰 위치)  (Terrain, 보이지 않는 상자)  (바위)     (DungeonRoom)  (보스 씬 이동)  (꺼진 채로)    (빌드와 검사)
```

플레이 흐름은 "던전 탐색 → 아이템 방에서 열쇠 획득 → 보스 입구 방의 포털 → 보스 씬"이다.

- **같은 시드와 같은 설정은 항상 같은 맵을 만든다.** 방 배치·지도·소품·장애물·스폰은 시드에서 따로 파생한 난수를 쓴다.
- **보이는 부분은 환경 세팅 에셋(`DungeonEnvironmentSettings`)이 정한다.** 지면 색·언덕, 경계 소품, 장애물과 문의 모양이다. 에셋을 바꿔도 방 모양, 장애물 위치와 충돌, 스폰 위치, 입구는 달라지지 않는다.
- **모든 방은 항상 연결된다.** 방 배치가 루프 없는 트리이고, 그래프의 간선마다 길을 판다.
- **생성물은 `DungeonGenerator` 오브젝트의 `Generated` 자식 아래에만 놓이며 씬에 저장되지 않는다.** Terrain 데이터와 머티리얼이 메모리에만 있기 때문이다. Play 시작과 인스펙터 버튼으로 다시 만든다.

## 2. 파일 구성

모두 `Assets/Game/Scripts/Dungeon/` 아래에 있다.

| 파일 | 역할 | Unity 의존 |
| --- | --- | --- |
| [DungeonLayout.cs](../../../Assets/Game/Scripts/Dungeon/DungeonLayout.cs) | 방 연결 그래프 생성, 역할 지정, 조건 미달 시 재시도 | 순수 로직 (`Vector2Int`, `Mathf`만 사용) |
| [WalkableMap.cs](../../../Assets/Game/Scripts/Dungeon/WalkableMap.cs) | 1유닛 격자의 걸을 수 있는 영역 지도. 공터·길·입구·장애물·적 스폰 위치 | 순수 로직 (`Vector2`, `Mathf`만 사용) |
| [DungeonGenerator.cs](../../../Assets/Game/Scripts/Dungeon/DungeonGenerator.cs) | 생성 순서, 장애물, 방·스폰, 포털·열쇠, 입구 잠금, NavMesh. 모든 인스펙터 필드 | MonoBehaviour |
| [DungeonGenerator.Ground.cs](../../../Assets/Game/Scripts/Dungeon/DungeonGenerator.Ground.cs) | 지면(Terrain)과 이동을 막는 보이지 않는 상자 | 같은 클래스의 나머지 |
| [DungeonGenerator.Props.cs](../../../Assets/Game/Scripts/Dungeon/DungeonGenerator.Props.cs) | 경계 소품 배치와 묶어 그리기, 색을 곱한 머티리얼 사본, 바닥 가림 측정 | 같은 클래스의 나머지 |
| [DungeonEnvironmentSettings.cs](../../../Assets/Game/Scripts/Dungeon/DungeonEnvironmentSettings.cs) | 환경 세팅 에셋(ScriptableObject). 에셋은 `Assets/Game/Environment/Dungeon/`에 있다 | 데이터 |
| [DungeonRoom.cs](../../../Assets/Game/Scripts/Dungeon/DungeonRoom.cs) | 방 하나의 결과물: 역할, 적 스폰 위치, 입구 잠금, 열쇠 방 여부 | MonoBehaviour |
| [DungeonPortal.cs](../../../Assets/Game/Scripts/Dungeon/DungeonPortal.cs) | 보스 입구 방의 포털. 열린 상태에서 플레이어가 들어오면 보스 씬을 불러온다 | MonoBehaviour |
| [DungeonKeyPickup.cs](../../../Assets/Game/Scripts/Dungeon/DungeonKeyPickup.cs) | 아이템 방의 열쇠. 플레이어가 닿으면 포털을 연다 | MonoBehaviour |
| [DungeonMapSource.cs](../../../Assets/Game/Scripts/Dungeon/DungeonMapSource.cs) | 확인용 이동체·카메라가 쓰는 기반 클래스(시작 위치, 재생성, 맵 영역, 잠금 전환) | MonoBehaviour |
| [Editor/DungeonGeneratorEditor.cs](../../../Assets/Game/Scripts/Dungeon/Editor/DungeonGeneratorEditor.cs) | 인스펙터의 생성 버튼과 상태 안내 (UI Toolkit) | 에디터 전용 |
| [Editor/DungeonPrototypeSceneBuilder.cs](../../../Assets/Game/Scripts/Dungeon/Editor/DungeonPrototypeSceneBuilder.cs) | 확인용 씬을 만드는 메뉴, 소품 프리팹 지정, 후처리 | 에디터 전용 |
| [Prototype/](../../../Assets/Game/Scripts/Dungeon/Prototype/DungeonPrototypeWalker.cs) | 확인용 임시 이동체와 카메라 | 게임 플레이어 아님 |

## 3. 생성 순서

진입점은 [`DungeonGenerator.Generate()`](../../../Assets/Game/Scripts/Dungeon/DungeonGenerator.cs#L241)이다.

| 단계 | 메서드 | 하는 일 |
| --- | --- | --- |
| 0 | `Clear()` | 이전 `Generated` 자식, 생성한 머티리얼·메시·지형 데이터, NavMesh, 소품 그리기 등록을 지운다 |
| 1 | `DungeonLayout.Generate()`, `PickKeyRoom()` | 격자 위에 방을 잇고 역할을 정한다. 조건 미달이면 다시 뽑는다. 아이템 방 중 하나를 열쇠 방으로 고른다 |
| 2 | `WalkableMap.Build()`, `PlaceEnemySpawns()` | 방마다 공터를, 간선마다 굽은 길을 파고, 입구와 장애물 위치를 정한다. 전투 방에 적 스폰 위치를 고른다 |
| 3 | `BuildGround()` | Terrain 지면, 걸을 수 없는 칸을 덮는 보이지 않는 상자, 경계 소품 |
| 4 | `BuildObstacles()` | 전투 방의 바위. 충돌은 기둥 모양 |
| 5 | `BuildRooms()` | 공터마다 `DungeonRoom`과 역할 표식, 적 스폰 표식 |
| 6 | `BuildBossPortalAndKey()` | 보스 입구 공터에 포털, 열쇠 방 공터에 열쇠를 만들고 서로 연결한다 |
| 7 | `BuildGateObjects()` | 입구마다 잠금 오브젝트를 꺼진 채로 만들어 그 방에 등록한다 |
| 8 | `BakeAndCheckNavMesh()` | NavMesh를 굽고 시작 방에서 모든 방·열쇠·포털까지의 경로와 스폰 위치를 검사한다 |

결과는 콘솔에 한 줄로 남는다.

```text
[Dungeon] seed 777: rooms 12, boss path 8, item rooms 2, entrances 22, enemy spawns 24, portal locked by key,
navmesh rooms reachable 12/12, boss reachable, key reachable, enemy spawns on mesh 24/24, objects 120, 116 ms
```

## 4. 핵심 규칙

### 방 배치 (Growing Tree)

1. 격자 중앙 칸에 시작 방을 놓는다.
2. 기존 방 하나를 골라 빈 이웃 칸으로 새 방을 잇는다. 방 수가 찰 때까지 반복한다.
3. 어느 방을 고를지를 `NewestPickChance`가 정한다.

| `NewestPickChance` | 결과 |
| --- | --- |
| 1.0 | 항상 가장 최근 방에서 뻗는다. Recursive Backtracking과 같고 거의 일자가 된다 |
| 0.6 (기본) | 곁가지가 섞인 형태 |
| 0.0 | 아무 방에서나 뻗는다. 가지가 많고 보스가 가까워진다 |

보스 입구까지 `MinBossPathLength`방 이상, 아이템 방 `MinItemRooms`개 이상이어야 한다. `MaxLayoutAttempts`회 안에 못 찾으면 가장 가까운 배치를 쓰고 경고를 남긴다.

### 역할

| 역할 | 조건 | 공터에 놓이는 것 |
| --- | --- | --- |
| `Start` | 격자 중앙 칸 | 시작 표식 (초록) |
| `BossGate` | 시작 방에서 가장 먼 방 | 보스 포털 (열림: 보라, 잠김: 어두운 회색) |
| `Item` | 나머지 막다른 방 | 아이템 표식 (노랑). 열쇠 방에는 표식 대신 열쇠 (하늘색) |
| `Combat` | 그 외 | 바위 장애물, 적 스폰 표식 (빨강) |

### 공터와 길

- 방 격자 한 칸의 크기는 `RoomSize + BoundaryWidth`다. 공터는 `RoomSize` 안에서 크기(`ClearingScale`), 중심(`CenterJitter`), 윤곽(`OutlineWobble`)이 흔들리는 타원이다. `Shape`를 `Rectangle`로 바꾸면 직사각형 방이 된다.
- 길은 이웃한 공터의 중심을 잇는 두 구간으로, 가운데가 옆으로 `PathBend`까지 밀린다. 폭은 `PathWidth`다. NavMesh 에이전트 반지름이 1이라 4보다 좁히지 않는다.
- **걸을 수 있는 곳은 높이 0의 평지다.** Terrain은 경계 바깥에만 `HillHeight`까지 낮은 언덕을 만들고, 흙과 풀을 섞어 칠해 공터 가장자리를 부드럽게 보이게 한다.
- **이동을 막는 것은 보이지 않는 상자다.** 걸을 수 없는 칸을 가로줄 단위로 합쳐 덮는다(맵당 200개 안팎). 소품에는 충돌이 없다.

### 환경 세팅 에셋

| 에셋 | 분위기 |
| --- | --- |
| `Forest` (확인용 씬 기본) | 갈색 바위·풀·청록과 보라 버섯 모양 나무. 3단계까지의 모습과 같다 |
| `PineHighland` | 침엽수·활엽수·단풍·마른 나무, 잎 식물, 이끼 바위, 회녹색 바위. 언덕이 조금 더 높다 |
| `Ruins` | 폐허 요새. 회색으로 보정한 석재 블록, 마른 나무. 랜드마크: 부서진 탑·감시탑·벽돌 요새. 장애물: 석재 블록·제단 |
| `Desert` | 사막 협곡. 모래 지면, 높은 언덕, 바위·선인장·야자. 랜드마크: 제단·모래 언덕 |
| `TrialCourse` | 시련장. 주황 길, 청록 땅, 원색 나무·청록 돌·표지판. 장애물: 가시 롤러·가시공·지뢰·상자·회전 칼날(피해 없음). 문: 결승선 게이트 |
| `ColosseumOutskirts` | 콜로세움 외곽. Colosseum 지면 색(갈색 공터, 녹색 바깥), 바위·마른 풀·상자. 랜드마크: 벽돌 요새·탑·제단. 문: START 게이트(채도 낮춤) |
| `TropicalCoast` | 열대 해안. 모래 공터, 야자·바나나 나무, 잎 식물. 랜드마크: 감시탑·탑. 장애물: 바위·덤불·화물 상자 |
| `DeadMarsh` | 고사목 늪. 어두운 지면, 마른 나무 위주, 어둡게 보정한 바위. 랜드마크: 어둡게 보정한 탑·감시탑 |

- 소품·장애물은 목록의 항목마다 **가중치(`Weight`), 크기 배율(`Scale`), 곱할 색(`Tint`), 회색으로 옮기는 정도(`Desaturate`)**를 따로 정한다. 프리팹마다 원래 크기와 색이 달라 같은 목록 안에서도 맞춰야 하기 때문이다.
- `Tint`는 원본 머티리얼을 바꾸지 않고 사본에 곱한다. 후처리(채도 +44)로 형광처럼 보이는 연두를 차분하게 하는 등 색 보정에 쓴다. 곱하는 방식이라 색을 어둡게·덜 선명하게만 할 수 있다.
- `Desaturate`는 곱하기 전에 원래 색을 회색 쪽으로 옮긴다. 1이면 회색이 되고, 거기에 `Tint`를 곱해 원하는 색을 입힌다. 초록 블록(`Grass_3~8`)을 석재로, 원색 게이트를 바랜 색으로 바꾸는 데 쓴다. 머티리얼 색에만 적용되고 텍스처 색(`Tree_1~3` 등)에는 적용되지 않는다.
- 지면은 색만 정하거나, 텍스처(`GroundTexture`, `ClearingTexture`)를 넣고 색을 곱할 수 있다.
- 문 모양(`GatePrefab`)을 넣으면 입구 폭에 맞춰 옆으로 늘여 쓴다. 폭이 z 방향인 프리팹(START·결승선 게이트)은 `GateRotation` y를 90으로 둔다. 색은 `GateTint`·`GateDesaturate`로 보정한다. 비어 있으면 빨간 상자다. 충돌은 모양과 상관없다.
- 후처리는 에셋에 없다. 씬의 Global Volume이 맡는다(2026-11-18 개발자 결정).
- 새 환경은 Project 창에서 **Create > TopDownBase > Dungeon Environment**로 만들거나 기존 에셋을 복제한다.
- `Environment`가 비어 있으면 소품 없이 기본 색 지면만 만든다.

### 경계 소품

- 걸을 수 있는 곳과 맞닿은 첫 줄은 `RimDensity`, 그 안쪽 `EdgeDepth`까지는 `EdgeDensity`, 나머지는 `FillDensity` 밀도로 놓는다(모두 환경 에셋 값).
- 가장자리에는 낮은 소품(`LowProps`: 바위, 풀)만 쓴다. 키 큰 소품(`TallProps`: 나무)은 그 바깥에 놓되, **북쪽 가까이에 공터나 길이 있으면 놓지 않는다.** 카메라가 남쪽에서 내려다보므로 그 자리의 나무가 바닥을 가리기 때문이다. 거리는 소품 높이와 `CameraPitch`로 계산하고 `CameraClearance`보다 짧아지지 않는다.
- **`DungeonPrototype` 씬의 조명은 그림자를 끈다.** 따라가는 카메라가 대상에서 80유닛 떨어져 있어 화면 전체가 URP 에셋의 그림자 거리(50) 밖이라, 켜 두면 그림자는 보이지 않고 그리는 비용만 든다. 그림자를 보이게 하려면 그림자 거리를 100 정도로 늘리고 단계를 1로 줄인다(PineHighland 기준 DrawCall 201). 측정은 [자연환경 과정](NatureEnvironment/Process.md)의 그림자 설정 측정.
- **소품은 GameObject 없이 묶어 그린다(`PropRendering` = `Instanced`).** 카메라마다 화면 안(그림자 여유 `ShadowCullPadding` 포함)의 소품만 골라 프리팹 종류마다 `Graphics.RenderMeshInstanced`로 한 번에 그린다. DrawCall은 소품 수가 아니라 (프리팹, 색) 종류 수에 비례한다. `Forest`(9종)는 따라가는 화면 96, `PineHighland`(18종)는 154다(그림자 끔 기준). 프리팹은 루트에 메시 하나가 있는 형태여야 하며, 아니면 경고를 남기고 건너뛴다. `GameObjects`로 바꾸면 소품마다 오브젝트를 만든다(비교용).
- 소품 머티리얼은 인스턴싱을 켠(필요하면 색을 보정한) 사본을 만들어 쓴다. 원본 에셋은 바꾸지 않는다.
- 머티리얼이 여러 개인 프리팹은 종류 하나가 머티리얼 수만큼 DrawCall을 쓴다(건물 3~5개, 표지판 4개, 야자 4개). DrawCall이 기준을 넘으면 이런 종류부터 줄인다.
- 가려지는 바닥은 대부분 가장자리 첫 줄의 낮은 소품에서 나온다(`Forest`: 소품을 모두 빼면 3.1%, 낮은 소품만 빼면 4.0%). 표지판처럼 높거나 폭이 넓은 낮은 소품은 크기를 줄이거나 드물게 둔다.

### 랜드마크

- 건물처럼 크고 드문 소품은 `Landmarks`에 넣는다. 방 하나당 `LandmarksPerRoom`개를 목표로, 막힌 칸을 무작위 순서로 훑어 놓을 수 있는 자리에만 놓는다.
- 자리 조건: 바닥 원(모서리까지 감싸는 반지름)이 걸을 수 있는 영역에서 `LandmarkClearance` 이상, 다른 랜드마크와 `LandmarkSpacing` 이상 떨어지고, 맵 안에 들어가며, 북쪽으로 꼭대기가 바닥을 가리는 거리 안에 공터나 길이 없어야 한다.
- 랜드마크 자리(바닥 원 + 1유닛)에는 다른 소품을 놓지 않는다. 랜드마크는 별도 난수를 쓰므로 랜드마크가 없는 환경의 소품 배치는 이전과 같다.

### 장애물

- 전투 방마다 `ObstacleCount`개. 경계에서 `ObstacleMargin`, 서로 `ObstacleSpacing`, 입구에서 (최대 반지름 + 1) 이상 띄운다. 그래서 장애물이 길목을 막지 않는다.
- 충돌은 반지름 `ObstacleRadius`의 기둥(CapsuleCollider)이고, 보이는 것은 환경 에셋 `Obstacles` 중 하나의 복제본이다. 프리팹 크기는 `Diameter`로 기둥 지름에 맞춘다. 프리팹의 충돌체는 떼어 낸다.

### 입구와 잠금

- **입구:** 방 칸과 맞닿은(대각선 포함) 길 칸을 방마다 모아, 서로 이어진 덩어리 하나를 입구 하나로 본다. 길 하나에는 양 끝 방에 입구가 하나씩 생긴다. 입구마다 그 칸을 빠짐없이 덮는 회전된 상자를 구한다.
- **잠금 오브젝트:** 입구 상자 크기의 충돌 상자(높이 `GateHeight`), `NavMeshObstacle`(carving), 보이는 낮은 벽(빨간색 임시 표시). 꺼진 채로 만들어 NavMesh에는 열린 상태가 구워진다.
- **`DungeonRoom.SetDoorsLocked(true)`가 그 방의 입구를 모두 켠다.** 잠긴 동안에는 충돌과 NavMesh 양쪽에서 방 밖으로 나가는 길이 끊긴다.
- 길이 다른 방의 공터를 스쳐 지나가면 그 방에도 입구가 생긴다. 그 방을 잠그면 스쳐 가는 길도 막힌다.

### 적 스폰 위치

전투 방의 공터 칸 중에서 고른다.

- 경계에서 `SpawnEdgeMargin`, 입구에서 `SpawnEntranceClearance`, 장애물 가장자리에서 `SpawnObstacleClearance` 이상 떨어진 칸.
- 서로 `SpawnSpacing` 이상 떨어지게 무작위로 `EnemySpawnCount` 범위의 개수를 고른다. 자리가 모자라면 그보다 적게 놓인다(시드 2,000개 맵의 전투 방 12,035개 중 2개가 1개로 모자랐다).

### 보스 포털과 열쇠

- **포털은 보스 씬의 이름만 안다.** 플레이어가 들어오면 `BossSceneName` 씬을 불러온다. 투기장을 바꿀 때는 씬 이름만 바꾸면 된다.
- **씬이 바뀌므로 던전으로 돌아오는 길은 없다.**
- **열쇠는 아이템 방 중 한 곳의 공터 중심에 놓인다.** 얻기 전에는 포털이 반응하지 않고, 얻으면 포털 색이 바뀐다.
- **아이템 방이 없는 배치에서는 포털을 열어 둔다.** `LockPortalWithKey`를 끄면 항상 열려 있다.
- **포털과 열쇠는 `Player` 태그에 반응한다.** 플레이어 오브젝트에 충돌체와 이 태그가 있어야 한다.
- **보스 씬은 Build Settings의 씬 목록에 있어야 한다.** 인스펙터가 미리 경고한다.

## 5. 사용 방식

### 에디터에서 바로 생성

`DungeonGenerator` 오브젝트를 선택하면 인스펙터 아래쪽에 **맵 생성** 영역이 나온다. Play를 누르지 않아도 된다.

| 버튼 | 동작 |
| --- | --- |
| 현재 시드로 생성 | `Seed` 값으로 생성한다. 설정을 바꿔 가며 같은 맵을 비교할 때 쓴다 |
| 무작위 시드로 생성 | 새 시드를 뽑아 `Seed`에 기록하고 생성한다 |
| 지우기 | 생성물을 모두 삭제한다 |

버튼 아래 안내 상자는 차례로 마지막 생성 결과, 보스 포털 상태(이동할 씬과 열쇠, 씬 이름이 비었거나 빌드 목록에 없으면 경고), 지금 설정에서 Play를 누르면 어떻게 되는지를 알려 준다.

- **편집 모드에서 만든 맵은 씬에 저장되지 않는다.** 생성해도 씬이 변경된 것으로 표시되지 않고, Play 진입·씬 닫기·스크립트 재컴파일 때 사라진다. 무작위 시드로 바뀐 `Seed` 값도 씬을 따로 저장하지 않으면 남지 않는다.
- 편집 모드에서는 NavMesh를 검사만 하고 남기지 않는다. NavMesh는 Play 시작 시 다시 빌드된다.
- 맵을 저장해 손으로 고치는 기능은 없다(2026-11-18 개발자 결정으로 이번 단계에서 제외).

### Play를 눌렀을 때

| `GenerateOnAwake` | `RandomizeSeedOnAwake` | Play 시 동작 |
| --- | --- | --- |
| 켬 | 켬 (기본) | 새 시드로 생성 |
| 켬 | 끔 | `Seed` 값으로 생성. 에디터에서 본 맵과 같다 |
| 끔 | — | 생성하지 않는다. 스크립트에서 `Generate()`를 불러야 한다 |

### 확인용 씬

`Assets/Scenes/DungeonPrototype.unity`를 연다. 메뉴 **TopDownBase > Dungeon Prototype > Create Or Open Scene**으로도 열 수 있다. 씬의 생성기에는 `Forest` 환경 에셋이 연결되어 있고, 데모의 후처리를 복사한 Global Volume(`Assets/Game/Environment/PolyStyle/Settings/NatureVolumeProfile.asset`)이 걸려 있다.

| 키 | 동작 |
| --- | --- |
| WASD | 이동 |
| Tab (누르는 동안) | 던전 전체 보기 |
| R | 새 시드로 다시 생성 |
| L | 지금 있는 방의 입구 잠금 전환 |

Game 창을 한 번 클릭해 포커스를 줘야 키가 들어간다. 하늘색 열쇠에 닿으면 보스 입구 방의 포털이 보라색으로 바뀌고, 그 포털에 들어가면 보스 씬(현재 `SampleScene`)으로 넘어간다.

## 6. 인스펙터 설정

| 묶음 | 필드 | 기본값 | 의미 |
| --- | --- | --- | --- |
| Seed | `Seed` | 12345 | 맵을 결정하는 값 |
| | `RandomizeSeedOnAwake` / `GenerateOnAwake` | 켬 / 켬 | Play 시작 시 새 시드를 뽑는다 / 생성한다 |
| Layout | `GridSize` | 5×5 | 방을 놓을 수 있는 격자 크기 |
| | `RoomCount` | 8 (확인용 씬 12) | 만들 방 수 |
| | `NewestPickChance` | 0.6 | 일자형(1) ↔ 방사형(0) |
| | `MinBossPathLength` / `MinItemRooms` | 5 / 1 | 보스 입구까지 최소 방 수(양 끝 포함) / 최소 아이템 방 수 |
| | `MaxLayoutAttempts` | 50 | 조건을 만족할 때까지의 재시도 한도 |
| Shape | `Shape` | Clearing | 불규칙 공터 / 직사각형 |
| | `RoomSize` | 24×20 | 방 하나가 쓸 수 있는 최대 크기(유닛) |
| | `BoundaryWidth` | 5 | 이웃한 방 사이 경계의 폭(유닛). 맵 크기와 숲의 두께를 정한다 |
| | `ClearingScale` / `CenterJitter` / `OutlineWobble` | 0.8~1 / 1 / 0.2 | 공터 크기 배율, 중심 흔들림, 윤곽 울퉁불퉁함 |
| | `PathWidth` / `PathBend` | 5 / 2 | 길 폭, 길 가운데를 옆으로 미는 최대 거리(유닛) |
| | `OuterMargin` | 10 | 맵 바깥쪽 숲의 폭(유닛) |
| Obstacles | `ObstacleCount` | 2~4 | 전투 방 하나의 장애물 수 |
| | `ObstacleMargin` / `ObstacleSpacing` | 4 / 9 | 경계와의 거리, 장애물 중심 사이 거리(유닛) |
| | `ObstacleRadius` | 1.2~2 | 충돌 기둥의 반지름 |
| | `ObstacleHeight` | 1.5 | 충돌 기둥 높이 |
| Enemy spawns | `EnemySpawnCount` | 2~4 | 전투 방 하나의 스폰 위치 수 |
| | `SpawnEdgeMargin` / `SpawnEntranceClearance` / `SpawnObstacleClearance` / `SpawnSpacing` | 2 / 5 / 1 / 4 | 경계·입구·장애물·서로 사이의 최소 거리(유닛) |
| Boss portal | `BossSceneName` | 비어 있음 (확인용 씬 `SampleScene`) | 포털이 불러올 보스 씬 이름 |
| | `LockPortalWithKey` | 켬 | 아이템 방의 열쇠를 얻어야 포털이 열린다 |
| Gates | `BuildGates` | 켬 | 입구 잠금 오브젝트를 만든다 |
| | `GateHeight` / `GateVisualHeight` / `GateVisualThickness` / `GateColor` | 3 / 1.2 / 0.6 / 빨강 | 충돌 높이, 보이는 벽의 크기와 색 |
| Environment | `Environment` | 확인용 씬: `Forest` | 환경 세팅 에셋. 아래 표 |
| Ground | `UseTerrainGround` | 켬 | 끄면 평평한 판과 칸 단위 표면 |
| | `HeightmapResolution` / `AlphamapResolution` | 257 / 512 | 지형 해상도 |
| Props | `PropRendering` | Instanced | 묶어 그리기 / 오브젝트로 만들기 |
| | `ShadowCullPadding` | 8 | 화면 밖이라도 그리는 거리(그림자용) |
| | `CameraClearance` / `CameraPitch` | 8 / 40 | 나무를 놓지 않는 북쪽 거리, 게임 카메라 각도 |
| Collision | `BlockerHeight` | 3 | 보이지 않는 상자의 높이 |
| NavMesh | `BuildNavMesh` | 켬 | 생성 후 NavMesh를 굽고 검사한다 |
| Look | `BaseMaterial` | 비어 있음 | 색을 입힐 바탕 머티리얼. 비우면 렌더 파이프라인 기본 머티리얼 |
| | 색상 7종 | — | 장애물(환경에 모양이 없을 때), 역할별 표식, 잠긴 포털, 열쇠 |

환경 세팅 에셋(`DungeonEnvironmentSettings`):

| 묶음 | 필드 | 기본값 | 의미 |
| --- | --- | --- | --- |
| Ground | `HillHeight` / `HillStart` / `HillWidth` / `HillNoiseScale` | 1 / 2.5 / 4 / 12 | 바깥 언덕 높이, 평지로 두는 거리, 오르막 폭, 높이 잡음 크기 |
| | `EdgeBlend` | 1.2 | 흙과 풀이 섞이는 폭(유닛) |
| | `GroundColor` / `ClearingColor` | 풀색 / 흙색 | 지면 색. 텍스처가 있으면 곱한다 |
| | `GroundTexture` / `ClearingTexture` / `TextureTileSize` | 없음 / 없음 / 4 | 지면 텍스처와 한 장이 덮는 크기 |
| Props | `LowProps` / `TallProps` | — | 가장자리 소품 / 바깥 소품. 항목마다 `Prefab`, `Weight`, `Tint`, `Desaturate`, `Scale` |
| | `EdgeDepth` / `EdgeDensity` / `RimDensity` / `FillDensity` | 2 / 0.55 / 0.85 / 0.07 | 가장자리 깊이와 밀도 |
| | `PropsCastShadows` / `LowPropsCastShadows` | 켬 / 끔 | 그림자. 조명 그림자를 켤 때만 의미가 있다(`DungeonPrototype`은 꺼 둠) |
| Landmarks | `Landmarks` | — | 크고 드문 소품. 항목은 `LowProps`와 같다 |
| | `LandmarksPerRoom` / `LandmarkSpacing` / `LandmarkClearance` | 0.5 / 6 / 1.5 | 방당 목표 수, 랜드마크끼리 간격, 걸을 수 있는 영역과 간격(유닛) |
| Obstacles | `Obstacles` | — | 장애물 모양. 항목마다 `Prefab`, `Weight`, `Tint`, `Desaturate`, `Diameter`. 비우면 원기둥 |
| Gates | `GatePrefab` / `GatePrefabWidth` | 없음 / 2 | 문 모양과 그 원래 폭(회전한 뒤 입구를 가로지르는 방향). 비우면 빨간 상자 |
| | `GateRotation` / `GateTint` / `GateDesaturate` | 0 / 흰색 / 0 | 문 프리팹 회전(도)과 색 보정 |

## 7. 코드에서 쓰기

생성기는 씬에서 찾지 않고 **참조를 받아서** 쓴다. `FindFirstObjectByType`, `GameObject.Find` 같은 탐색 함수는 씬 전체를 훑으므로 쓰지 않는다.

### 인스펙터 할당

씬에 함께 놓이는 컴포넌트는 필드로 받아 인스펙터에서 연결한다. 확인용 이동체와 카메라가 이 방식이다.

```csharp
using TopDownBase.Dungeon;
using UnityEngine;

public class DungeonRunController : MonoBehaviour
{
    [SerializeField] private DungeonGenerator _generator;   // 인스펙터에서 연결

    private void Start()
    {
        // 생성기는 Awake에서 맵을 만들므로 Start부터 결과를 쓸 수 있다.
        Vector3 playerStart = _generator.StartPosition;      // 시작 공터 중심
        DungeonPortal bossPortal = _generator.BossPortal;    // 보스 씬으로 넘어가는 포털

        foreach (DungeonRoom room in _generator.Rooms)
        {
            if (room.Role != DungeonRoomRole.Combat) continue;

            room.SetDoorsLocked(true);                       // 전투 시작: 입구 잠금
            foreach (Transform spawn in room.EnemySpawnPoints)
            {
                // spawn.position에 적 생성
            }
        }
    }
}
```

### 의존성 주입

런타임에 만들어지는 오브젝트나 MonoBehaviour가 아닌 클래스는 만드는 쪽이 참조를 넘겨준다.

```csharp
// 일반 클래스: 생성자로 받는다.
public class EncounterDirector
{
    private readonly DungeonGenerator _generator;

    public EncounterDirector(DungeonGenerator generator)
    {
        _generator = generator;
    }

    public void StartRun(int seed)
    {
        _generator.Seed = seed;
        _generator.Generate();                               // 또는 GenerateWithRandomSeed()
    }
}

// 런타임에 생성되는 컴포넌트: 만든 직후 초기화 메서드로 받는다.
public class EnemySpawner : MonoBehaviour
{
    private DungeonRoom _room;

    public void Initialize(DungeonRoom room)
    {
        _room = room;
    }
}
```

필요한 것이 방 하나뿐이면 생성기 전체가 아니라 `DungeonRoom`만 넘긴다. 플레이어가 어느 방에 있는지는 `RoomAt(위치)`로 얻는다.

| API | 내용 |
| --- | --- |
| `Generate()` / `GenerateWithRandomSeed()` / `Clear()` | 생성, 새 시드로 생성, 삭제 |
| `Rooms`, `StartRoom`, `BossGateRoom`, `StartPosition` | 생성 결과. `Rooms`의 순서는 `Layout.Rooms`, `Map.Rooms`와 같다 |
| `RoomAt(Vector3)` | 위치가 들어 있는 방. 길이나 경계 위면 null |
| `ToggleRoomLockAt(Vector3)` | 그 위치 방의 입구 잠금을 전환한다(L 키) |
| `BossPortal`, `KeyPickup` | 보스 포털과 열쇠. 열쇠가 없는 배치에서는 `KeyPickup`이 null |
| `DungeonPortal.IsLocked`, `Unlock()`, `BossSceneName` | 포털 잠금 상태, 열기, 불러올 씬 |
| `DungeonKeyPickup.IsCollected` | 열쇠 획득 여부 |
| `Layout` | 방 연결 그래프 (`BossPathLength`, `ItemRoomCount`, `MeetsRequirements` 등) |
| `Map` | 걸을 수 있는 영역 지도 (`Rooms`, `Entrances`, `Obstacles`, `EnemySpawns`, `IsWalkable`, `RoomIndexAt`) |
| `HasGeneratedMap`, `LastReport`, `LastGenerateMilliseconds`, `GeneratedObjectCount` | 생성물 존재 여부와 마지막 결과 |
| `PropCount`, `BlockerCount`, `MeasureOccludedRatio(각도)` | 소품 수, 충돌 상자 수, 카메라에서 가려지는 바닥 비율 |
| `DungeonRoom.Role`, `Cell`, `DistanceFromStart`, `InteriorSize`, `HasKey` | 방 정보. `InteriorSize`는 공터의 지름(가로, 세로) |
| `DungeonRoom.EnemySpawnPoints` | 적 스폰 위치 목록 |
| `DungeonRoom.SetDoorsLocked(bool)`, `DoorsLocked` | 방의 입구 전체 잠금 |

## 8. 생성물 계층 구조

```text
DungeonGenerator            (DungeonGenerator, NavMeshSurface)
└── Generated               씬에 저장되지 않음
    ├── Terrain             (Terrain, TerrainCollider) 지형 데이터는 메모리에만 있음
    ├── Blockers            (BoxCollider × 200개 안팎) 보이지 않는 이동 제한
    ├── Obstacles
    │   └── Obstacle …      (CapsuleCollider) └── Rock_2 … 보이기만 함
    ├── Rooms
    │   ├── Room_2_2_Start      (DungeonRoom) 공터 중심에 놓임
    │   │   └── PlayerStart     표식, 충돌 없음
    │   ├── Room_3_2_Combat     (DungeonRoom)
    │   │   └── EnemySpawn …    표식, 충돌 없음
    │   ├── Room_4_3_Item       (DungeonRoom, 열쇠 방)
    │   │   └── Key             (DungeonKeyPickup, 트리거) └── Visual
    │   └── Room_1_2_BossGate   (DungeonRoom)
    │       └── BossPortal      (DungeonPortal, 트리거) └── Visual
    └── Gates
        └── Gate_Room3_0 …  (BoxCollider, NavMeshObstacle) 꺼진 상태, 해당 방에 등록됨
```

경계 소품은 `Instanced` 방식에서는 오브젝트가 없다. `GameObjects` 방식에서는 `Generated/Props`(NavMesh 빌드 제외) 아래에 만들어진다. 맵 하나의 생성물은 120개 안팎이다.

## 9. 알아둘 제약

- **빌드에서는 실행해 보지 않았다.** `BaseMaterial` 지정(렌더 파이프라인 기본 머티리얼은 에디터에서만 제공), Terrain 셰이더 포함 여부를 빌드 단계에서 확인해야 한다.
- **생성에 100ms 안팎이 걸린다.** 대부분 Terrain 데이터와 NavMesh 빌드다. 한 판에 한 번 생성하는 용도로는 문제없는 수준이다.
- **소품 프리팹은 루트에 메시 하나가 있어야 묶어 그린다.** 소품 메시의 Read/Write가 꺼져 있어도 된다.
- **잠금 표시는 환경마다 다르다.** `Forest`·`PineHighland`는 빨간 상자, 나머지 6개 환경은 석재 블록·게이트 등 문 프리팹을 쓴다.
- **함정 모양 장애물(`TrialCourse`)은 피해를 주지 않는다.** 모양만 함정이고 충돌은 다른 장애물과 같은 기둥이다. 피해 판정은 플레이어·적 구현 뒤에 붙인다.
- **이 환경들의 머티리얼 텍스처는 `Assets/@Asset/POLY STYLE`에 있다.** 외부 에셋 폴더라 Git에 없으므로, 설치하지 않은 환경에서는 텍스처를 쓰는 소품(`Tree_1~3`, `Hill_1~5` 등)이 텍스처 없이 보인다(확인하지 않음).
- **씬을 넘어갈 때 상태를 옮기지 않는다.** 포털은 보스 씬을 불러오기만 한다.
- **보스 씬은 임시로 `SampleScene`을 쓴다.** 투기장 규격과 보스 구성은 미확정이다.
- **열쇠와 포털은 TopDown Engine의 `KeyOperatedZone`·`Teleporter`를 쓰지 않는다.** 엔진 기반 플레이어가 생기면 교체를 검토한다.
- **임시 이동체는 게임 플레이어가 아니다.** TopDown Engine과 연결되지 않은 캡슐이다.
- **공터 크기, 길 폭, 방 수는 미확정이다.** 플레이어와 적이 구현된 뒤 전투 공간 조정 단계에서 정한다.
