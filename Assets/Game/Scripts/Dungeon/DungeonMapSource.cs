using UnityEngine;

namespace TopDownBase.Dungeon
{
    /// <summary>
    /// 맵을 만드는 컴포넌트의 공통 기반. 확인용 이동체와 카메라가 맵의 종류를 몰라도
    /// 시작 위치, 재생성, 전체 영역을 쓸 수 있게 한다.
    /// </summary>
    public abstract class DungeonMapSource : MonoBehaviour
    {
        /// <summary>플레이어를 놓을 위치.</summary>
        public abstract Vector3 StartPosition { get; }

        public abstract void GenerateWithRandomSeed();

        /// <summary>맵 전체를 감싸는 월드 영역. 전체 보기 카메라에 쓴다.</summary>
        public abstract Bounds GetWorldBounds();
    }
}
