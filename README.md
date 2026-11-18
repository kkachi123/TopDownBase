# TopDownBase

Unity 3D 탑다운 근접 액션 로그라이트 프로젝트입니다.

- Unity 버전: `6000.6.0f1`
- 기획: [초기 게임 기획서](Docs/TopDownBase_Initial_Game_Plan.md)
- 진행 상황: [전체 개발 과정](PROCESS.md)

## 주의사항

### 1. 외부 에셋은 저장소에 없습니다

용량 문제로 아래 폴더는 `.meta`를 포함해 Git에서 제외했습니다(`.gitignore`). clone만 하면 프로젝트가 정상 동작하지 않습니다.

| 폴더 | 내용 |
| --- | --- |
| `Assets/TopDownEngine/` | TopDown Engine |
| `Assets/@Asset/Effect/` | Hovl Studio-Magic, Hovl Studio-Sword, PixPlays, UNI VFX |
| `Assets/@Asset/KayKit/` | Characters, Platformer |
| `Assets/@Asset/Kevin Iglesias/` | Human Animations |
| `Assets/@Asset/POLY STYLE/` | Fantasy Treasure Chest, Platformer Starter Pack, Vegetation Pack 2 |
| `Assets/@Asset/PolysplitGames/` | LowPolyMedievalFantasyHeroes |
| `Assets/Fantasy Treasure Chest_Standard/` | Fantasy Treasure Chest (Standard) |
| `Assets/vFavorites/` | vFavorites |

### 2. 새 PC에서 프로젝트 열기

1. 저장소를 clone합니다.
2. **Unity로 열기 전에** 위 에셋을 모두 원래 경로에 넣습니다.
   - 에셋 스토어 패키지는 **기존과 같은 버전**으로 import해야 GUID가 일치해서 씬과 프리팹의 참조가 이어집니다.
   - `.meta`까지 포함된 기존 폴더 백업이 있다면 그대로 복사하는 것이 가장 확실합니다.
3. Unity로 열고 콘솔 오류와 씬의 Missing 참조를 확인합니다.

> 에셋 없이 먼저 열면 참조가 Missing으로 표시됩니다. 그 상태로 씬이나 프리팹을 **저장하면 참조가 영구히 끊어집니다.** 에셋을 넣기 전에는 저장하지 마세요.

### 3. 외부 에셋 수정 금지

- 외부 에셋 폴더 안의 파일은 Git이 추적하지 않습니다. 원본을 직접 수정하면 다른 PC에는 반영되지 않고, 되돌릴 수도 없습니다.
- 수정이 필요하면 `Assets/Game/` 아래로 복사한 뒤 사본을 고칩니다.
- 새 외부 에셋을 추가하면 `.gitignore`의 "외부 에셋" 항목과 위 표에 함께 추가합니다.

### 4. Git LFS를 사용하지 않습니다

- GitHub 무료 플랜(LFS 예산 $0)에서는 LFS 업로드가 막히므로 LFS를 쓰지 않습니다. `.gitattributes`에서는 바이너리 파일을 `binary`로만 지정합니다.
- 바이너리 파일도 일반 Git으로 올라가며, 수정할 때마다 저장소 기록에 통째로 쌓입니다.
- **100MB를 넘는 파일은 GitHub가 push를 거부합니다.** 50MB부터는 경고가 나옵니다. 큰 텍스처, 모델, 오디오는 외부 에셋처럼 Git 밖에서 관리합니다.
- `.gitattributes`에 `lfs` 규칙을 다시 추가하지 마세요.
