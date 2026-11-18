using System;
using UnityEngine;

namespace TopDownBase.Dungeon
{
    /// <summary>목록에서 가중치에 따라 뽑히는 프리팹 하나.</summary>
    [Serializable]
    public class DungeonPrefabChoice
    {
        public GameObject Prefab;
        [Tooltip("같은 목록 안에서 뽑힐 비율. 0이면 쓰지 않는다.")]
        [Min(0f)] public float Weight = 1f;
        [Tooltip("머티리얼 색에 곱하는 색. 흰색이면 원래 색 그대로다. 원본 머티리얼은 바꾸지 않고 사본에 입힌다.")]
        public Color Tint = Color.white;
    }

    /// <summary>경계 소품 하나.</summary>
    [Serializable]
    public class DungeonPropEntry : DungeonPrefabChoice
    {
        [Tooltip("프리팹 원래 크기에 곱하는 배율의 (최소, 최대). 프리팹마다 원래 크기가 달라 소품별로 정한다.")]
        public Vector2 Scale = Vector2.one;
    }

    /// <summary>장애물로 보여 줄 프리팹 하나. 충돌은 프리팹과 상관없이 생성기가 정한 기둥이다.</summary>
    [Serializable]
    public class DungeonObstacleVisual : DungeonPrefabChoice
    {
        [Tooltip("프리팹의 대략적인 바닥 지름(유닛). 장애물 반지름에 맞춰 크기를 정하는 데 쓴다.")]
        [Min(0.1f)] public float Diameter = 1.5f;
    }

    /// <summary>
    /// 던전의 보이는 부분(지면, 경계 소품, 장애물 모양, 문 모양). 이 에셋만 바꿔 다른 분위기의 맵을 만든다.
    /// 방 배치, 공터와 길의 모양, 장애물 위치와 충돌, 스폰 위치는 생성기 설정이 정하므로 에셋을 바꿔도 달라지지 않는다.
    /// 후처리는 씬의 Volume이 맡는다.
    /// </summary>
    [CreateAssetMenu(fileName = "DungeonEnvironment", menuName = "TopDownBase/Dungeon Environment")]
    public class DungeonEnvironmentSettings : ScriptableObject
    {
        [Header("Ground")]
        [Tooltip("경계 바깥 언덕의 최대 높이(유닛). 캐릭터 키(2)보다 낮아야 남쪽 가장자리에서 캐릭터가 가려지지 않는다.")]
        [Range(0f, 1.8f)] public float HillHeight = 1f;
        [Tooltip("걸을 수 있는 영역의 끝에서 이 거리(유닛)까지는 평지로 둔다.")]
        [Min(0.5f)] public float HillStart = 2.5f;
        [Min(0.5f)] public float HillWidth = 4f;
        [Min(1f)] public float HillNoiseScale = 12f;
        [Tooltip("흙과 풀이 섞이는 폭(유닛).")]
        [Min(0.1f)] public float EdgeBlend = 1.2f;
        [Tooltip("경계 쪽 지면 색. 텍스처가 있으면 텍스처에 곱한다.")]
        public Color GroundColor = new Color(0.33f, 0.45f, 0.28f);
        [Tooltip("공터와 길의 지면 색. 텍스처가 있으면 텍스처에 곱한다.")]
        public Color ClearingColor = new Color(0.52f, 0.47f, 0.38f);
        [Tooltip("비워 두면 단색으로 칠한다.")]
        public Texture2D GroundTexture;
        public Texture2D ClearingTexture;
        [Tooltip("텍스처 한 장이 덮는 크기(유닛).")]
        [Min(0.5f)] public float TextureTileSize = 4f;

        [Header("Props")]
        [Tooltip("경계 가장자리에 놓는 낮은 소품(바위, 덤불). 루트에 메시 하나가 있는 프리팹이어야 묶어 그릴 수 있다.")]
        public DungeonPropEntry[] LowProps = Array.Empty<DungeonPropEntry>();
        [Tooltip("가장자리 바깥에 놓는 키 큰 소품(나무). 공터와 길의 남쪽에는 놓이지 않는다.")]
        public DungeonPropEntry[] TallProps = Array.Empty<DungeonPropEntry>();
        [Tooltip("걸을 수 있는 영역에서 이 거리(유닛) 안쪽의 막힌 칸을 가장자리로 본다.")]
        [Min(1f)] public float EdgeDepth = 2f;
        [Range(0f, 1f)] public float EdgeDensity = 0.55f;
        [Tooltip("걸을 수 있는 곳과 바로 맞닿은 첫 줄(대각선 포함)의 소품 밀도. 높이면 공터와 길의 테두리가 끊기지 않는다.")]
        [Range(0f, 1f)] public float RimDensity = 0.85f;
        [Range(0f, 1f)] public float FillDensity = 0.07f;
        public bool PropsCastShadows = true;
        [Tooltip("끄면 가장자리의 낮은 소품은 그림자를 드리우지 않는다. 그림자 단계마다 한 번씩 더 그리는 비용이 줄어든다.")]
        public bool LowPropsCastShadows;

        [Header("Obstacles")]
        [Tooltip("전투 방 장애물의 모양. 비워 두면 원기둥을 쓴다.")]
        public DungeonObstacleVisual[] Obstacles = Array.Empty<DungeonObstacleVisual>();

        [Header("Gates")]
        [Tooltip("입구 잠금의 모양. 비워 두면 빨간 상자를 쓴다. 충돌은 프리팹과 상관없이 입구를 덮는 상자다.")]
        public GameObject GatePrefab;
        [Tooltip("GatePrefab의 원래 폭(유닛). 입구 폭에 맞춰 옆으로 늘이는 데 쓴다.")]
        [Min(0.1f)] public float GatePrefabWidth = 2f;
    }
}
