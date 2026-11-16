# 던전 생성기 — 핵심 구조와 사용법

상위 문서: [레벨 분야 과정](Process.md) · 진행과 검증 기록: [던전 생성 프로토타입 과정](DungeonGenerationPrototype/Process.md)

보스 투기장 앞의 던전 구간을 손 배치 없이 만드는 프로토타입이다. 형상은 전부 기본 도형이고, 게임 플레이어·적·보상 로직과는 아직 연결되어 있지 않다.

## 1. 한눈에 보기

```text
시드 ─▶ ① 방 배치 ─▶ ② 방 조립 ─▶ ③ 벽과 문 ─▶ ④ NavMesh
        (그래프)      (바닥·장애물·스폰)  (문 차단물 포함)   (빌드와 검사)
```

- **같은 시드와 같은 설정은 항상 같은 맵을 만든다.** 무작위 값은 `System.Random(Seed)` 하나에서만 나온다.
- **모든 방은 항상 연결된다.** 방 배치가 루프 없는 트리라서 연결 검사가 필요 없다.
- **생성물은 `DungeonGenerator` 오브젝트의 `Generated` 자식 아래에만 놓인다.** 다시 생성하거나 지울 때 이 자식만 삭제한다.

## 2. 파일 구성

모두 `Assets/Game/Scripts/Dungeon/` 아래에 있다.

| 파일 | 역할 | Unity 의존 |
| --- | --- | --- |
| [DungeonLayout.cs](../../../Assets/Game/Scripts/Dungeon/DungeonLayout.cs) | 방 연결 그래프 생성, 역할 지정, 조건 미달 시 재시도 | 순수 로직 (`Vector2Int`, `Mathf`만 사용) |
| [DungeonGenerator.cs](../../../Assets/Game/Scripts/Dungeon/DungeonGenerator.cs) | 그래프를 실제 오브젝트로 조립, NavMesh 빌드 | MonoBehaviour |
| [DungeonRoom.cs](../../../Assets/Game/Scripts/Dungeon/DungeonRoom.cs) | 방 하나의 결과물: 역할, 적 스폰 위치, 문 잠금 | MonoBehaviour |
| [Editor/DungeonGeneratorEditor.cs](../../../Assets/Game/Scripts/Dungeon/Editor/DungeonGeneratorEditor.cs) | 인스펙터의 생성 버튼과 상태 안내 (UI Toolkit) | 에디터 전용 |
| [Editor/DungeonPrototypeSceneBuilder.cs](../../../Assets/Game/Scripts/Dungeon/Editor/DungeonPrototypeSceneBuilder.cs) | 확인용 씬을 만드는 메뉴 | 에디터 전용 |
| [Prototype/](../../../Assets/Game/Scripts/Dungeon/Prototype/DungeonPrototypeWalker.cs) | 확인용 임시 이동체와 카메라 | 게임 플레이어 아님 |

## 3. 생성 순서

진입점은 [`DungeonGenerator.Generate()`](../../../Assets/Game/Scripts/Dungeon/DungeonGenerator.cs#L143)이다.

| 단계 | 메서드 | 하는 일 |
| --- | --- | --- |
| 0 | `Clear()` | 이전 `Generated` 자식, 생성한 머티리얼, NavMesh를 지운다 |
| 1 | `DungeonLayout.Generate()` | 격자 위에 방을 잇고 역할을 정한다. 조건 미달이면 다시 뽑는다 |
| 2 | `BuildRoom()` | 방마다 바닥을 깔고 역할에 따라 장애물·스폰·표식을 놓는다 |
| 3 | `BuildWalls()`, `BuildCornerPosts()` | 벽, 문 틈, 문 차단물, 모서리 기둥을 만든다 |
| 4 | `BakeAndCheckNavMesh()` | NavMesh를 굽고 시작 방→보스 입구 경로와 스폰 위치를 검사한다 |

결과는 콘솔에 한 줄로 남는다.

```text
[Dungeon] seed 12345: rooms 8, boss path 6, item rooms 1, layout attempts 1,
requirements met True, navmesh path start→boss PathComplete, enemy spawns on mesh 16/16
```

## 4. 핵심 규칙

### 방 배치 (Growing Tree)

1. 격자 중앙 칸에 시작 방을 놓는다.
2. 기존 방 하나를 골라 빈 이웃 칸으로 새 방을 잇는다. 방 수가 찰 때까지 반복한다.
3. 어느 방을 고를지를 `NewestPickChance`가 정한다.

| `NewestPickChance` | 결과 |
| --- | --- |
| 1.0 | 항상 가장 최근 방에서 뻗는다. Recursive Backtracking과 같고 거의 일자가 된다 |
| 0.6 (기본) | 보스까지 5~6방, 곁가지 1~2개 |
| 0.0 | 아무 방에서나 뻗는다. 가지가 많고 보스가 가까워진다 |

### 역할 지정

| 역할 | 조건 | 방 안에 놓이는 것 |
| --- | --- | --- |
| `Start` | 격자 중앙 칸 | 시작 표식 (초록) |
| `BossGate` | 시작 방에서 가장 먼 방 | 보스 입구 표식 (보라) |
| `Item` | 나머지 막다른 방 | 아이템 표식 (노랑) |
| `Combat` | 그 외 | 장애물 스탬프, 적 스폰 위치 (빨강) |

### 재시도 조건

보스 입구까지 `MinBossPathLength`방 이상, 아이템 방 `MinItemRooms`개 이상이어야 한다. `MaxLayoutAttempts`회 안에 못 찾으면 가장 가까운 배치를 쓰고 경고를 남긴다.

### 벽과 문

- 방 사이의 벽은 한 번만 만든다. 동·북쪽 벽은 자기 방이, 서·남쪽 벽은 이웃이 없을 때만 만든다.
- 연결된 방 사이의 벽에는 가운데에 `DoorTiles`칸 너비의 틈을 낸다.
- 문마다 차단물을 꺼 둔 상태로 만들어 두고 양쪽 방에 등록한다. 방을 잠그면 이 차단물이 켜진다.

### 장애물 스탬프

기둥 2×2, 낮은 벽 3칸(가로·세로), ㄴ자 2종의 다섯 가지 묶음을 무작위로 놓는다. 두 가지 간격 규칙을 지킨다.

- 벽에서 `ObstacleWallMargin`칸 이상 띄운다. 방 둘레에 항상 빈 통로가 남는다.
- 스탬프끼리 `ObstacleSpacing`칸 이상 띄운다. 서로 붙어 길을 막지 않는다.

이 두 규칙으로 빈 칸이 모두 이어지므로 도달 가능성 검사를 따로 하지 않는다. 스탬프 목록은 [`Stamps`](../../../Assets/Game/Scripts/Dungeon/DungeonGenerator.cs#L75) 배열에 있다.

### 적 스폰 위치

장애물이 없고 문에서 `SpawnDoorClearance`칸 이상 떨어진 칸 중에서, 서로 `SpawnSpacing`칸 이상 떨어지게 고른다.

## 5. 사용 방식

### 에디터에서 바로 생성

`DungeonGenerator` 오브젝트를 선택하면 인스펙터 아래쪽에 **맵 생성** 영역이 나온다. Play를 누르지 않아도 된다.

| 버튼 | 동작 |
| --- | --- |
| 현재 시드로 생성 | `Seed` 값으로 생성한다. 설정을 바꿔 가며 같은 맵을 비교할 때 쓴다 |
| 무작위 시드로 생성 | 새 시드를 뽑아 `Seed`에 기록하고 생성한다 |
| 지우기 | 생성물을 모두 삭제한다 |

버튼 아래 첫 번째 안내 상자는 마지막 생성 결과를, 두 번째 안내 상자는 지금 설정에서 Play를 누르면 어떻게 되는지를 알려 준다.

- 생성하거나 지우면 씬이 변경된 것으로 표시된다. 저장하면 맵이 씬에 함께 저장된다.
- 되돌리기(Ctrl+Z)는 지원하지 않는다. 이전 맵으로 돌아가려면 그 시드로 다시 생성한다.
- 편집 모드에서는 NavMesh를 검사만 하고 남기지 않는다. NavMesh는 Play 시작 시 항상 다시 빌드된다.

### Play를 눌렀을 때

| `GenerateOnAwake` | `RandomizeSeedOnAwake` | Play 시 동작 | 쓰는 경우 |
| --- | --- | --- | --- |
| 켬 | 켬 (기본) | 새 시드로 다시 생성. 에디터에서 만든 맵은 대체된다 | 매번 다른 맵 |
| 켬 | 끔 | `Seed` 값으로 다시 생성. 에디터에서 본 맵과 같다 | 마음에 든 시드 고정 |
| 끔 | — | 씬에 저장된 맵을 그대로 사용. NavMesh만 다시 빌드 | 생성한 맵을 손으로 고쳐 쓰기 |

세 번째 방식에서는 `Layout`이 비어 있다. 방 목록과 시작 방·보스 입구는 그대로 쓸 수 있다.

### 확인용 씬

`Assets/Scenes/DungeonPrototype.unity`를 연다. 메뉴 **TopDownBase > Dungeon Prototype > Create Or Open Scene**으로도 열 수 있다.

| 키 | 동작 |
| --- | --- |
| WASD | 이동 |
| Tab (누르는 동안) | 던전 전체 보기 |
| R | 새 시드로 다시 생성 |
| L | 지금 있는 방의 문 잠금 전환 |

Game 창을 한 번 클릭해 포커스를 줘야 키가 들어간다.

## 6. 인스펙터 설정

| 묶음 | 필드 | 기본값 | 의미 |
| --- | --- | --- | --- |
| Seed | `Seed` | 12345 | 맵을 결정하는 값 |
| | `RandomizeSeedOnAwake` | 켬 | Play 시작 시 새 시드를 뽑는다 |
| | `GenerateOnAwake` | 켬 | Play 시작 시 다시 생성한다 |
| Layout | `GridSize` | 5×5 | 방을 놓을 수 있는 격자 크기 |
| | `RoomCount` | 8 | 만들 방 수 |
| | `NewestPickChance` | 0.6 | 일자형(1) ↔ 방사형(0) |
| | `MinBossPathLength` | 5 | 보스 입구까지 최소 방 수 (양 끝 포함) |
| | `MinItemRooms` | 1 | 최소 아이템 방 수 |
| | `MaxLayoutAttempts` | 50 | 조건을 만족할 때까지의 재시도 한도 |
| Room | `TileSize` | 2 | 한 칸의 크기(유닛) |
| | `RoomTiles` | 12×10 | 방 안쪽 크기(칸). 기본값이면 24×20 유닛 |
| | `DoorTiles` | 3 | 문 너비(칸) |
| | `WallThickness` / `WallHeight` | 1 / 2 | 벽 두께와 높이(유닛) |
| Obstacles | `ObstacleCount` | 2~4 | 전투 방 하나의 스탬프 수 (최소, 최대) |
| | `ObstacleWallMargin` | 2 | 벽에서 띄우는 칸 수 |
| | `ObstacleSpacing` | 5 | 스탬프 사이 최소 칸 수 |
| | `ObstacleHeight` | 1.5 | 장애물 높이(유닛) |
| Enemy spawns | `EnemySpawnCount` | 2~4 | 전투 방 하나의 스폰 위치 수 (최소, 최대) |
| | `SpawnDoorClearance` | 3 | 문에서 띄우는 칸 수 |
| | `SpawnSpacing` | 2 | 스폰 위치 사이 최소 칸 수 |
| NavMesh | `BuildNavMesh` | 켬 | 생성 후 NavMesh를 굽고 검사한다 |
| Look | `BaseMaterial` | 비어 있음 | 색을 입힐 바탕 머티리얼. 비우면 렌더 파이프라인 기본 머티리얼 |
| | 색상 8종 | — | 바닥, 벽, 장애물, 문 차단물, 역할별 표식 색 |

방 크기를 바꿀 때는 `RoomTiles`와 `TileSize`를 함께 본다. 장애물과 스폰 간격은 모두 칸 단위라서 `TileSize`를 키우면 간격도 같이 커진다.

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
        Vector3 playerStart = _generator.StartPosition;      // 시작 방 중앙
        DungeonRoom bossGate = _generator.BossGateRoom;      // 보스 투기장으로 넘어갈 방

        foreach (DungeonRoom room in _generator.Rooms)
        {
            if (room.Role != DungeonRoomRole.Combat) continue;

            room.SetDoorsLocked(true);                       // 전투 시작: 문 잠금
            foreach (Transform spawn in room.EnemySpawnPoints)
            {
                // spawn.position에 적 생성
            }
        }
    }
}
```

### 의존성 주입

런타임에 만들어지는 오브젝트나 MonoBehaviour가 아닌 클래스는 만드는 쪽이 참조를 넘겨준다. 받는 쪽은 생성기가 어디에 있는지 알 필요가 없다.

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

참조를 가진 쪽(예: `DungeonRunController`)이 `new EncounterDirector(_generator)`나 `spawner.Initialize(room)`으로 넘겨준다. 필요한 것이 방 하나뿐이면 생성기 전체가 아니라 `DungeonRoom`만 넘긴다.

생성기 내부도 같은 원칙을 따른다. 생성물 루트, 방 목록, 시작 방, 보스 입구, NavMeshSurface를 직렬화된 참조로 보관하므로 저장된 맵을 다시 쓸 때 이름이나 타입으로 찾지 않는다.

| API | 내용 |
| --- | --- |
| `Generate()` / `GenerateWithRandomSeed()` / `Clear()` | 생성, 새 시드로 생성, 삭제 |
| `AdoptExistingMap()` | 씬에 저장된 맵을 다시 생성하지 않고 인식한다 |
| `Rooms`, `StartRoom`, `BossGateRoom`, `StartPosition` | 생성 결과 |
| `Layout` | 방 연결 그래프 (`BossPathLength`, `ItemRoomCount`, `MeetsRequirements` 등) |
| `HasGeneratedMap`, `LastReport` | 생성물 존재 여부, 마지막 결과 요약 |
| `DungeonRoom.Role`, `Cell`, `DistanceFromStart`, `InteriorSize` | 방 정보 |
| `DungeonRoom.EnemySpawnPoints` | 적 스폰 위치 목록 |
| `DungeonRoom.SetDoorsLocked(bool)`, `DoorsLocked` | 방에 연결된 문 전체 잠금 |

## 8. 생성물 계층 구조

```text
DungeonGenerator            (DungeonGenerator, NavMeshSurface)
└── Generated
    ├── Room_2_2_Start      (DungeonRoom)
    │   ├── Floor
    │   ├── PlayerStart     표식, 충돌 없음
    │   ├── Wall …
    │   └── DoorBlocker …   꺼진 상태
    ├── Room_3_2_Combat     (DungeonRoom)
    │   ├── Floor
    │   ├── Obstacle …
    │   ├── EnemySpawn …    표식, 충돌 없음
    │   └── Wall …, DoorBlocker …
    ├── …
    └── CornerPosts
        └── CornerPost …
```

방 이름은 `Room_{격자 x}_{격자 y}_{역할}`이다. 방 중심 사이의 간격은 방 안쪽 크기에 벽 두께를 더한 값이다(기본 25×21 유닛).

## 9. 알아둘 제약

- **형상은 기본 도형이다.** KayKit 모듈은 연결하지 않았다.
- **빌드에서는 `BaseMaterial`을 지정해야 한다.** 렌더 파이프라인 기본 머티리얼은 에디터에서만 제공된다. 빌드에서는 실행해 보지 않았다.
- **문을 잠가도 NavMesh는 바뀌지 않는다.** 차단물은 충돌만 막는다. 적이 잠긴 문 너머로 경로를 잡지 않게 하려면 연동 시 별도 처리가 필요하다.
- **보스 입구 방은 표식만 있다.** 투기장 전환은 연결하지 않았다.
- **임시 이동체는 게임 플레이어가 아니다.** TopDown Engine과 연결되지 않은 캡슐이다.
- **기획서상 절차적 생성은 MVP 제외 항목이다.** 이 기능은 탐색용 프로토타입이며 채택 여부는 개발자가 결정한다.
