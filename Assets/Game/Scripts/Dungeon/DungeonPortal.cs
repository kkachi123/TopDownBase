using UnityEngine;
using UnityEngine.SceneManagement;

namespace TopDownBase.Dungeon
{
    /// <summary>
    /// 보스 입구 방의 포털. 열린 상태에서 플레이어가 들어오면 보스 씬을 불러온다.
    /// 씬이 바뀌므로 던전으로 되돌아오는 길은 없다.
    /// 포털은 씬 이름만 알며, 보스 투기장의 크기나 구성은 그 씬이 정한다.
    /// </summary>
    public class DungeonPortal : MonoBehaviour
    {
        /// <summary>포털과 열쇠가 반응하는 대상의 태그.</summary>
        public const string TravellerTag = "Player";

        [SerializeField] private string _bossSceneName;
        [SerializeField] private bool _locked;
        [SerializeField] private Renderer _visual;
        [SerializeField] private Material _lockedMaterial;
        [SerializeField] private Material _unlockedMaterial;

        public bool IsLocked => _locked;
        public bool HasDestination => !string.IsNullOrEmpty(_bossSceneName);
        public string BossSceneName => _bossSceneName;

        /// <summary>생성기가 만든 직후 필요한 값을 넘겨준다.</summary>
        public void Initialize(string bossSceneName, bool locked, Renderer visual,
            Material lockedMaterial, Material unlockedMaterial)
        {
            _bossSceneName = bossSceneName;
            _locked = locked;
            _visual = visual;
            _lockedMaterial = lockedMaterial;
            _unlockedMaterial = unlockedMaterial;
            ApplyVisual();
        }

        public void Unlock()
        {
            _locked = false;
            ApplyVisual();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_locked || !HasDestination || !other.CompareTag(TravellerTag))
            {
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(_bossSceneName))
            {
                Debug.LogError($"[Dungeon] 보스 씬 '{_bossSceneName}'을 불러올 수 없습니다. " +
                               "Build Settings의 씬 목록에 등록되어 있는지 확인하세요.", this);
                return;
            }

            SceneManager.LoadScene(_bossSceneName);
        }

        private void ApplyVisual()
        {
            if (_visual != null)
            {
                _visual.sharedMaterial = _locked ? _lockedMaterial : _unlockedMaterial;
            }
        }
    }
}
