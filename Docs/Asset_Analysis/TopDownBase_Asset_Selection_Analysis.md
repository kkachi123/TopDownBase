# 플레이어·애니메이션·맵·이펙트 선정 분석

조사일: 2026-11-10. 기준: [초기 게임 기획서](../TopDownBase_Initial_Game_Plan.md). 범위는 `Assets/@Asset/`의 파일·프리팹 YAML·일부 FBX 임포트 메타데이터·README 및 프로젝트 설정이다. **확인**은 정적으로 읽은 사실, **판단**은 이를 바탕으로 한 활용 가능성이다. 에셋 조사는 정적 확인이 중심이며, 이후 순차 재생 프리팹의 Avatar와 첫 상태 전환을 Unity 에디터에서 확인했다. 전체 동작의 화면·성능·빌드는 검사하지 않았다. 압축된 unitypackage 내부와 모든 에셋의 개별 외형은 검사하지 않았다. 원본은 변경하지 않았다.

## 빠른 확인 — 현재 결정

| 항목 | 현황 |
| --- | --- |
| 플레이어 | KayKit Medium 사용. 구체적인 모델과 동작은 테스트 후 선택. |
| 보스 | KayKit Large 사용. 보스 등장 시점과 패턴은 미결정. |
| 애니메이션 | 각 캐릭터는 당분간 KayKit의 해당 리그 전용 클립만 사용. |
| 맵 | 맵 구조는 [던전 생성기](../Features/Level/DungeonGenerator_Guide.md)로 만든다(2026-11-16 개발자 결정, 현재 프로토타입). 형상과 장식에 쓸 팩은 KayKit Platformer, POLY STYLE을 검토 중이며 미확정. |
| 이펙트 | Hovl Sword, Hovl Magic, PixPlays 등을 비교할 후보로 정리. 미확정. |

**직접 확인할 파일:** `Assets/Game/Characters/Knight/Knight.prefab`과 `Assets/Game/Characters/BarbarianLarge/BarbarianLarge.prefab`. 씬에 배치하고 Play를 누르면 각각 동작이 순서대로 반복된다. 현재 Controller는 동작 확인용이며 게임 전투 로직이 아니다. 두 프리팹은 최종 외형을 확정한 것이 아니다.

## 다른 세션을 위한 인계

- **문서 상태:** 분석 진행 중. 아래 상세 조사에는 확정되지 않은 후보와 정적 조사에 따른 판단이 포함되어 있다. 게임 범위와 세부 수치는 초기 기획서를 기준으로 하고, 새로 확정하지 않는다.
- **확인한 것:** KayKit Medium·Large의 Avatar가 유효한 Humanoid이고 두 프리팹의 Controller 참조 및 첫 상태에서 다음 상태로의 전환을 Unity 에디터에서 확인했다. 상세 파일 구성과 호환성은 아래 근거를 참조한다.
- **미검증:** 전체 순서의 화면 재생, 애니메이션의 실제 조작감·검 위치·발 접지, 맵과 이펙트의 시각적 호환성, 성능과 빌드. 정적 조사와 실제 테스트 결과를 혼동하지 않는다.
- **다음 작업:** Medium 플레이어와 Large 보스의 동봉 클립을 각각 시각적으로 확인하고, 동일한 카메라·조명에서 맵 후보와 전투 이펙트를 비교한다. 선택 결과를 개발자와 확정한다.
- **작업 제약:** 외부 에셋 원본·데모를 직접 수정하지 않고 프로젝트 소유 코드와 프리팹 Variant/복제본을 사용한다. 원본 패키지 임포트나 렌더 설정 변경은 필요성과 영향을 확인한 뒤 별도 작업으로 진행한다.
- **문서 유지:** 이 파일을 사용자 확인과 세션 간 인계의 단일 기준으로 사용한다. 이후 선택·검증이 바뀌면 위 빠른 확인 표, 이 인계 요약, 아래 상세 근거와 미결정 항목을 함께 갱신하고 확인 사실과 추정을 구분한다.

## POLY STYLE URP 수정 현황 — 2026-11-10

- 프로젝트 소유 `Assets/Game/Environment/PolyStyle/Prefabs`에 환경·DemoScene 프리팹 Variant 87개와 URP 머티리얼을 구성했다. 에디터에서 이 87개에 대해 누락 MeshFilter 메시, 빈 머티리얼 슬롯, Built-in Standard 셰이더 참조가 각각 0개임을 확인했다. DemoScene에는 56개 메시가 있다.
- Unity 에디터의 별도 프리뷰 카메라에서 DemoScene의 지형이 렌더링되는 것을 확인했지만 화면에서 색상과 질감이 의도한 것과 일치하는지, 조명·식생·바다의 품질은 검증이 더 필요하다. 원본 셰이더와 URP 조명은 다를 수 있다.
- Vegetation Pack 2 임포트 전부터 일부 Starter Pack 프리팹은 해당 패키지에 들어 있는 FBX GUID를 참조했다. 임포트 후 Starter Pack 쪽 일부 기존 GUID도 바뀌어 참조가 끊어진 것을 확인했다. `Assets/Game/Environment/PolyStyle/StarterSource`에는 Starter Pack 패키지에서 필요한 원본 GUID를 별도 복구한 에셋이 있으므로 참조 확인 없이 삭제하지 않는다.
- **미해결:** 원본 상자 프리팹 15개와 상자 `Demo.prefab`의 머티리얼 참조가 임포트 후 누락되어 현재 복사본에 포함하지 않았다. 올바른 원본 재질을 찾기 전 임의 대체하지 않는다. Fantasy Treasure Chest 별도 패키지는 미검사다.
- 검증 및 향후 비교는 원본 데모 씬을 수정하지 않고 복사본에서 한다. 프로젝트 전체 URP 이전이나 조명 일치 검증을 완료했다는 의미는 아니다.

## 선택 현황과 우선 비교

- **사용자 선택 (2026-11-10):** KayKit **Medium은 플레이어블 캐릭터**, **Large는 보스용**으로 사용한다. 애니메이션은 당분간 각각 KayKit의 `Rig_Medium`, `Rig_Large` 동봉 전용 클립만 사용한다. 구체적인 모델·클립은 테스트 후 결정하며 다른 팩과의 리타게팅은 현재 선택 범위 밖이다.
- **현재 확인용 구성:** `Assets/Game/Characters/Knight/Knight.prefab`과 `Assets/Game/Characters/BarbarianLarge/BarbarianLarge.prefab`에 각각 동봉 애니메이션의 순차 반복 Controller를 연결했다. Knight와 BarbarianLarge는 동작 확인에 쓰는 프리팹이며 최종 외형 확정을 뜻하지 않는다. Unity에서 Avatar 유효성, Controller 참조 및 첫 상태에서 다음 상태로의 전환을 확인했다. 전체 동작의 화면 재생과 플레이 테스트는 아직 하지 않았다. 보스 역할 선택만으로 보스 패턴이나 등장 시점·기획 범위를 확정하지 않는다.
- **판단:** KayKit Medium 캐릭터와 Medium 애니메이션은 같은 Avatar 참조와 MVP 동작에 해당하는 클립이 있어 첫 시도에 적합하다. 다만 3타의 연결감과 대시의 움직임·길이는 재생 전 확정할 수 없다. `Barbarian_Large`는 별도 Large 리그라 같은 목록을 그대로 가정할 수 없다.
- **비교할 맵 후보:** KayKit Platformer의 목재 바닥·장벽과 POLY STYLE Platformer Starter Pack의 지형·환경. 전자는 전투 공간을 빠르게 구성할 후보, 후자는 주변 환경과 지형의 후보다. 서로 다른 모듈성·스타일은 실제 프리팹 배치 전 확정하지 않는다.
- **비교할 이펙트 후보:** Hovl Sword의 `Sword Slash`와 `Prick`을 1순위로 한손검에 시험하고, Hovl Magic의 `Sparks`/`Hits`와 PixPlays 발사체를 필요할 때 비교한다. 선택 확정이 아니라 파일 구성에 따른 추천이다.

## 1. 플레이어 모델

| 팩과 후보 | 확인한 근거 | 판단 및 확인할 점 |
| --- | --- | --- |
| **KayKit Adventurers** | `Prefabs/Characters`에 Knight, Barbarian, Barbarian_Large, Druid, Engineer, Mage, Ranger, Rogue, Rogue_Hooded 9종. `Accessories`에 `sword_1handed`, 활 등 별도 프리팹. 캐릭터 FBX 메타상 8종은 Medium Avatar, Barbarian_Large는 Large Avatar를 참조한다. | 사용자 선택. Knight·Rogue·Barbarian 등은 먼저 검토할 수 있으나 이름만으로 전투 적합성이 확정되지는 않는다. 탑다운 시 실루엣, 검의 크기·손 부착, 이동 방향 가독성을 비교한다. |
| **Polysplit LowPolyMedievalFantasyHeroes** | `BasicHeroes/prefabs`에 남녀 Swordsman, Fighter, Knight, Archer 등, `BasicWeapons` 및 `BaseCharacters`가 있다. `BaseMale.fbx.meta`에 Humanoid 설정과 Hips 등 본 매핑이 기록되어 있다. | 대체 외형 후보. KayKit과 다른 모델 비율·손 뼈·셰이더로 인해 모션·무기 그립 검증이 필요하다. 조사한 BaseMale 설정이 모든 프리팹의 유효 Avatar를 보장하지 않는다. 데모 포즈 Controller는 전투 시스템이 아니다. |
| **Kevin Iglesias Human Animations 모델** | `Models/HumanM_Model.fbx`, `HumanF_Model.fbx`와 애니메이션이 있다. 조사한 Male 모델 메타의 `animationType: 3`, `avatarSetup: 1`. | 애니메이션 검사용 또는 외형 대안. KayKit 로우폴리 스타일과 일치하는지, 유효 Avatar와 무기 결합은 Unity에서 확인한다. |

KayKit `Knight.prefab`은 Animator의 `m_Avatar`가 `Rig_Medium_Avatar.asset`을 가리키며 Controller는 비어 있고 `m_ApplyRootMotion: 0`이다. 따라서 원본 프리팹만으로 필요한 상태 전환이 이미 완성되었다고 볼 수 없다. `Barbarian_Large.prefab`은 `Rig_Large_Avatar.asset`을 참조한다. 각 프리팹·애니메이션의 최종 사용은 프로젝트 소유 Controller와 복제본/Variant에서 검증한다.

## 2. 플레이어 애니메이션: 동봉 구성의 충족 가능성

| MVP 동작 | Medium 클립의 실제 이름 | 판단 및 미확인 |
| --- | --- | --- |
| 대기·이동 | `General/Idle_A`, `Idle_B`; `Movement Basic/Running_A`, `Running_B`, `Walking_A`~`C`; `Movement Advanced/Running_Strafe_Left`, `Running_Strafe_Right`, `Walking_Backwards` | 이동과 조준 방향이 다른 상황의 후보가 있다. 전방/후방 달리기용 클립과 블렌딩의 자연스러움은 실제 재생으로 확인한다. |
| 한손검 3타 | `Combat Melee/Melee_1H_Attack_Chop`, `Melee_1H_Attack_Slice_Diagonal`, `Melee_1H_Attack_Slice_Horizontal` | 서로 다른 공격 세 개가 존재한다. 이를 3연타로 연결했을 때의 자세·길이·취소 타이밍은 미검증이며, 단일 클립 파일이 입력·연계·피해 판정을 구현하지 않는다. |
| 전방 특수 찌르기 | `Combat Melee/Melee_1H_Attack_Stab` | 후보 클립이 있다. 해당 `.anim`의 기록상 종료 시간은 약 1.6초이며 실제 재생 속도·유효 구간은 미정. 공격 모션의 선후딜과 취소 불가 규칙은 게임 로직에서 검증해야 한다. |
| 대시·회피 | `Movement Advanced/Dodge_Forward`, `Dodge_Backward`, `Dodge_Left`, `Dodge_Right` | 네 방향 동작 후보. 클립이 제자리인지, 전진 이동이 포함되는지, 실제 대시 거리·무적 시간과 맞는지 미검증. 대시와 정확한 회피 판정은 애니메이션 파일만으로 제공되지 않는다. |
| 피격·사망 | `General/Hit_A`, `Hit_B`, `Death_A`, `Death_B` | 피드백 후보가 있다. 시인성과 빠른 전투에서의 입력 차단 시간은 미검증. |
| 원거리 적 참고 | `Combat Ranged/Ranged_Bow_Draw`, `Ranged_Bow_Release`, `Ranged_Magic_Spellcasting`, `Ranged_Magic_Shoot` 등 | 적용은 사용자 선택 범위 밖의 후보. 투사체·예고·피해 기능 자체를 뜻하지 않는다. |

**결론:** 필수 동작별 이름의 클립은 동봉되어 있으므로 KayKit만으로 프로토타입을 시작할 근거가 충분하다. 그러나 실제 3타 연속성, 찌르기 가독성, 회피 방향·속도는 아직 확인하지 않아 최종 채택 가능성은 조건부다. 특정 수치·클립 순서는 플레이 테스트로 결정한다.

**Large 차이:** `Rig_Large/Combat Melee`에는 `Melee_1H_Slash`, `Melee_1H_Stab` 등, `Movement Advanced`에는 네 방향 Dodge가 있다. Medium과 파일 구성이 다르므로 Large로 바꾸면 세 가지 구별 가능한 1H 동작이 제공되는지와 별도 조합을 다시 확인한다.

**리타게팅 경고:** `Source_FBX/.../Rig_Medium_CombatMelee.fbx.meta`는 Humanoid(`animationType: 3`), `Rig_Medium_Avatar` 참조를 기록한다. 같은 메타의 저장된 임포트 경고에는 `Melee_1H_Attack_Slice_Diagonal`과 `Melee_1H_Attack_Stab` 등에서 일부 본의 평행이동 애니메이션이 버려져 리타게팅 품질이 낮아질 수 있다고 나온다. 이는 해당 원본 FBX의 기록이며 모든 별도 `.anim`이나 실제 화면에 문제가 있다는 뜻은 아니다. 설정을 원본에서 바꾸지 말고 눈으로 팔·다리·발 접지를 확인한다.

## 3. 모델과 애니메이션의 교차 조합 (참고: 현재 사용 계획 밖)

| 모델 × 애니메이션 | 정적 근거와 판단 | 실행 시 필수 확인 |
| --- | --- | --- |
| KayKit Medium × KayKit Medium | Knight 프리팹의 Avatar와 Medium CombatMelee 소스 FBX의 Avatar GUID가 동일하다. 별도 `Melee_1H_Attack_Stab.anim`에는 `Rig_Medium/root/.../handslot` 등의 명시적 경로도 있다. **가장 직접적인 후보.** | 모델별 Animator와 Avatar 유효성, `.anim` 및 FBX 클립의 실제 재생, 핸드 슬롯·검 위치, 발 미끄러짐과 방향. |
| KayKit Large × KayKit Large | Large 캐릭터와 Large 클립·Avatar가 별도로 제공된다. | Large의 동작 수·클립 연결과 손 위치. Medium과 동일한 동작 구성을 가정하지 않는다. |
| KayKit × Kevin | 조사한 Kevin `HumanM@Attack1H01_L/R.fbx.meta`는 Humanoid 설정과 Avatar 참조가 있다. KayKit도 Humanoid Avatar를 사용하므로 **FBX의 Humanoid 클립을 통한 리타게팅 시험 후보**다. | 양쪽 Avatar의 유효성, 클립 종류와 뼈 매핑, 발 위치·무기 그립·과장된 비율. 반드시 재생 시험이 필요하며 현재 사용 결정은 KayKit 동봉 우선이다. |
| Polysplit × KayKit | 조사한 Polysplit `BaseMale.fbx.meta`와 KayKit 애니메이션 소스 FBX에 Humanoid 설정이 있다. **FBX Humanoid 클립은 리타게팅을 시험할 근거가 있다.** | 실제 대상 프리팹이 유효한 Humanoid Avatar를 쓰는지 확인. KayKit 별도 `.anim`의 명시적 뼈 경로는 타 모델에서 직접 맞지 않을 수 있으므로 FBX 클립과 구별해 시험한다. |
| Polysplit × Kevin | 조사한 양쪽 FBX의 Humanoid 설정을 근거로 **시험 후보**다. | 대상 Avatar, 본 비율·손 부착·발 접지와 원본 클립의 루트 모션 확인. |

다른 팩 사이의 교차 조합은 실제 Unity 재생을 확인하지 않았다. KayKit 동봉 조합은 위에 적은 첫 상태 전환만 확인했다. Humanoid로 설정되어 있다는 사실만으로 모든 본이나 부속물 곡선이 올바르게 전달된다고 결론 내리지 않는다. 테스트는 원본 Import Settings 수정 없이 프로젝트 소유 Controller·프리팹 복제본을 사용하고, 설정 변경이 필요하면 복사본에서 검토한다.

## 4. Level / Map

| 팩 | 실제 구성 | 활용 가능성·제약 |
| --- | --- | --- |
| KayKit Platformer | `Prefabs/neutral`에 `floor_wood_1x1`, `floor_wood_2x2`, `floor_wood_2x6`, `floor_wood_4x4`, 여러 크기의 `barrier`, `pillar`, `structure`, `floor_spikes` 등이 있고 blue/green/neutral/red/yellow 폴더가 있다. URP 변환 unitypackage도 별도 존재한다. | 격자 모듈이라 던전 생성기의 방 형상으로 시험할 후보. 목재·플랫폼 중심의 외형이 원하는 판타지 전투 방에 맞는지는 화면 확인 필요. 함정 프리팹이 있다는 이유로 함정 기능을 추가하지 않는다. |
| POLY STYLE Platformer Starter Pack | `Platformer Starter Pack_URP/Prefabs`에 `Ground_A`~계열, `Hill`, `Stone`, `Tree`, `Buildings`, `Bridge`, `Props`, `Treasure_Chest` 등과 데모·재질·설정이 풀려 있다. 별도 Standard unitypackage도 있다. | 지형 및 배경 장식 후보. Ground 이름만으로 평평한 타일이나 충돌체 구성을 확정할 수 없다. 높이 변화·장식이 탑다운 시야와 공격 예고를 가리지 않는지 검토한다. |
| POLY STYLE Fantasy Treasure Chest / Vegetation Pack 2 | 각각 README와 Standard unitypackage만 풀린 폴더에서 확인. | 패키지 내부 구성, URP 호환은 미확인. Starter Pack에도 상자·식생 후보가 있으므로 우선 비교하고 추가 임포트 필요성을 판단한다. |

맵 선정 기준: 캐릭터 대비, 바닥 예고의 명암, 벽과 장식의 가림, 카메라 시점에서의 충돌 영역 인지, 에셋 간 축척과 기존 URP 설정에서의 재질. 배치·콜라이더·성능은 아직 실제 검증하지 않았다.

### KayKit Platformer와 POLY STYLE 비교 — 2026-11-10

- **KayKit Platformer:** 실제 파일에 여러 규격의 `floor_wood`, `barrier`, `pillar`, `structure` 및 함정 모양 프리팹과 색상별 폴더가 있다. 모듈 이름과 구성을 근거로 전투 방의 빠른 블록아웃에 우선 추천한다. 함정 이름은 기능 구현을 뜻하지 않는다. 목재와 플랫폼 중심의 분위기가 게임에 맞는지, 캐릭터 크기·충돌 및 URP 머티리얼이 정상인지 실제 씬에서 확인해야 한다. 같은 KayKit 계열이므로 스타일 비교의 유력 후보이나 실제 조화가 보장되는 것은 아니다.
- **POLY STYLE Platformer Starter Pack:** 프로젝트 복제본에서 Ground·Hill·Stone·Tree·Grass·Buildings·Bridge 등 프리팹을 확인했다. 자연 지형과 주변 장식을 구성할 때 후보가 폭넓다. Ground가 평탄한 전투 바닥이나 격자형 모듈인지 이름만으로 확정할 수 없다. 복제본 87개의 메시·재질 연결과 데모 지형 렌더링은 확인했으나 화면 품질·충돌·캐릭터와의 조화는 추가 확인이 필요하다. 상자 원본 15개 및 상자 Demo는 머티리얼 누락으로 복제본 대상에서 빠져 있다.
- **추천:** 전투 방 단독 팩을 우선한다면 KayKit Platformer부터 시험하고, 자연 배경이나 장식이 필요하면 POLY STYLE을 함께 비교한다. 이는 기존에 POLY STYLE 단독을 먼저 시험하자고 한 환경 중심 추천을, KayKit 모듈 구성까지 직접 비교한 뒤 조정한 것이다. 최종 선택은 미정이며 두 팩을 실제 게임 카메라·조명 아래 비교하지 않았다.
- **씬 비교 예시:** `Assets/Scenes/SampleScene.unity`의 `Environment Comparison` 아래 KayKit 목재 바닥·장벽과 POLY STYLE 지형·식생을 나란히 배치하고 각각 Knight를 두었다. 비교용으로 Main Camera를 조정했다. Unity 카메라 렌더링 및 배치물 메시·머티리얼 참조는 확인했으나, 전투용 충돌체 구성과 이동·전투 플레이는 검증하지 않았다. 원본 에셋은 변경하지 않았다.
- **콜로세움 배치:** 같은 씬의 `Colosseum`은 처음 지름 20유닛으로 배치했으나 사용자의 피드백 후 **지름 32유닛**으로 확장했다. KayKit 장벽·기둥, POLY STYLE 바위·나무·풀, 플레이어 Knight와 보스 BarbarianLarge 및 내부 기둥 4개가 있다. 기존 `Environment Comparison`은 비활성화해 보존했고 Main Camera는 콜로세움 전체를 보는 임시 구도다. Unity 에디터 카메라로 두 캐릭터와 경기장이 보이는 것을 확인했고, 배치물의 누락 메시와 머티리얼은 각각 0개였다. 두 캐릭터의 기존 Animator Controller 연결도 확인했다. 편집 화면에서는 기본 포즈로 보이며 애니메이션 재생, 이동·전투와 충돌은 시험하지 않았다. 바닥 크기는 테스트용이며 Hades의 실제 치수를 의미하지 않는다.
- **Hades 구조 조사 및 현재 배치의 한계:** Hades 레벨 디자이너의 [설명](https://threadreaderapp.com/scrolly/1311777010239193088)에 따르면 방은 진행 및 전투에 따라 크기가 다르고, 벽·기둥은 이동과 투사체를 막는 등 역할이 있다. [엘리시움 보스전 공략](https://playerassist.com/hades-defeat-theseus-asterius/)은 테세우스·아스테리우스의 원형 경기장 중앙 부근 기둥과 외곽 회피 경로를 설명한다. 이 전투는 보스 2명 동시 전투로 본 프로젝트의 1:1 계획과 다르다. 최초 20 유닛 경기장은 근거 없는 초기값이었고 사용자가 좁다고 평가해 32유닛으로 확장했다. Hades의 실제 치수와 두 게임의 캐릭터 대비 비율은 확인되지 않았으므로 숫자로 동일성을 주장하지 않는다. 기둥은 배치 확인용이며 실제 충돌과 역할은 미확정이다. 정확한 크기·충돌·카메라 추적은 이동과 전투 테스트 후 결정한다.
- **전투 화면 카메라 시험:** `SampleScene`의 Main Camera를 정사영 크기 12, 아래 방향 각도 40도로 조정해 두 캐릭터가 더 크게 보이도록 했다. Unity 카메라 렌더로 두 캐릭터와 중앙 공간을 확인했고, 커밋 준비 시 씬이 저장된 상태임을 확인했다. 이는 Hades를 참고한 임시 화면 구도이며 실제 Hades의 카메라 수치나 동작과 동일함을 확인하지 못했다. 플레이어 이동·추적 및 전투 플레이 검증은 하지 않았다.

### 지형·환경 후보 비교 — 2026-11-10

**현재 추천이며 채택 확정은 아님:** 전투 방은 KayKit Platformer를 우선 시험하고 자연 환경은 POLY STYLE을 비교한다. POLY STYLE의 상자 원본 15개와 상자 데모는 머티리얼 참조가 누락되어 복제본에 포함되지 않았다. 공격 예고 가독성 및 캐릭터 시야가 우선이라는 기획을 바탕으로 한 추천이다.

| 후보 | 확인된 사실 | 판단 및 확인할 점 |
| --- | --- | --- |
| [Low-Poly Simple Nature Pack](https://assetstore.unity.com/packages/3d/environments/landscapes/low-poly-simple-nature-pack-162153) | 스토어 설명상 무료, 프리팹 24개, URP용 별도 unitypackage 제공. | 나무·식생 등 자연 장식 보강 후보. 지형 바닥까지 이 팩 하나로 충분한지는 확인되지 않았다. 기존 POLY STYLE의 장식과 겹칠 수 있다. |
| [Low Poly Environment Starter Pack](https://assetstore.unity.com/packages/3d/environments/low-poly-environment-starter-pack-228606) | 스토어에 무료 및 Unity 2020.3 기준 URP 호환으로 표시. 검색되는 상세 설명과 개별 구성품 목록은 확인되지 않았다. | 단일 환경 팩 대안으로 비교할 가치가 있으나 바닥·벽·충돌체가 실제로 포함되는지와 구성량을 확인하기 전 단독 사용을 추천 확정할 수 없다. |
| [Low Poly Wind](https://assetstore.unity.com/packages/vfx/shaders/low-poly-wind-182586) | 무료 셰이더 에셋. 스토어에 Unity 2022.3 기준 URP 호환으로 표시. | 식생 움직임을 위한 후순위 시각 효과 후보. 지형과 장식 모델을 만드는 도구는 아니며, 다른 팩의 모델과 실제 호환성은 미검증이다. |

프로젝트는 Unity 6000.6.0f1 및 URP 17.6.0이다. 스토어의 과거 Unity 버전 호환 표시는 이 프로젝트에서의 동작 보증이 아니다. 새 세 에셋은 스토어 정보만 확인했고 프로젝트에 가져오거나 렌더링·충돌·성능·KayKit과의 축척을 검증하지 않았다. 각 팩의 조합은 캐릭터와 바닥 공격 예고가 잘 보이는지 실제 게임 카메라로 비교해야 한다.

## 5. 이펙트

| 팩 | 확인된 파일/종류 | 추천 용도 및 제약 |
| --- | --- | --- |
| Hovl Studio Sword | `Sword slash VFX/Prefabs`에 `Sword Slash 1`~`17`, `Sword Slash Combo 1`~`9`, `Prick 1`~`5`, `Slash wave` 등. Shader Graph와 셰이더가 있다. | 검 궤적과 찌르기 우선 비교. Combo 프리팹은 시각 자산이며 게임의 3타 로직이 아니다. 길이·방향·스케일, 무기 및 피해 시점과 동기화 필요. |
| Hovl Studio Magic | `Prefabs`에 AoE effects, Hits and explosions, Sparks, Magic circles, Character auras, Slash effects 등. 예: `Electro hit`, `Sparks white`, `Charge slash blue`. 데모 README가 Bloom을 권장한다. | 피격 스파크·원거리 공격·강화 연출 후보. 화면을 과하게 가리지 않는 크기와 밝기, URP 재질을 확인한다. |
| PixPlays | `ElementalProjectiles`에 Fireball, Stonebullet 등의 Cast/Projectile/Hit, AOE·Auras·Beams·Blast·Shields 프리팹. 각 계열에 별도 URP unitypackage가 있다. README는 발사 VFX 이동을 제어하는 `ProjectileVfx`를 설명한다. | 원거리 공격과 명중의 대안. Built-in과 URP 폴더·패키지의 실제 재질 상태, 이펙트 이동과 게임 투사체 판정의 동기화를 확인한다. README의 URP 패키지 이름 안내가 Projectiles 항목에서 `ElementalAoe_URP`라고 표기되어 실제 파일명과 달라, 대상 패키지를 직접 확인해야 한다. |
| UNI VFX Missiles & Explosions | `.vfx` 10개, Orb·Razor Missile·Lightning Spear 등 프리팹 및 URP 데모가 있다. | 원거리 연출 대안. manifest/lock에서 Visual Effect Graph 패키지를 찾지 못했으므로 의존성·셰이더·실행 호환을 확인한다. |

이펙트는 판정 자체가 아니다. 특히 바닥 예고는 실제 타격 범위·방향·시간과 일치해야 한다. VFX 후보로부터 공격 패턴이나 속성·방어막 시스템을 추가하지 않는다.

## 6. 결정 전 테스트와 아직 남은 결정

1. **캐릭터·애니메이션:** KayKit Medium 캐릭터 1개에 `Idle_A`, `Running_A`, 한손검 Chop/Diagonal/Horizontal, Stab, 네 방향 Dodge, Hit, Death를 재생해 탑다운 시점의 동작·발·무기 위치를 확인한다. 공격의 반복·연결과 회피 이동 및 대시 거리를 함께 본다. Large 보스의 동작도 별도로 검증한다.
2. **전용 클립 확인:** Medium 플레이어와 Large 보스의 동봉 클립을 각 프리팹에서 끝까지 재생해 손 위치·발 접지·동작 가독성을 확인한다. 다른 팩과의 조합은 현재 보류한다.
3. **맵:** KayKit 목재 바닥·장벽과 POLY STYLE Ground/장식을 같은 카메라·조명에서 비교한다. 플레이어/예고 대비, 걸림과 가림, 축척, 재질을 관찰한다.
4. **효과:** Hovl Slash 1개와 Prick 1개, 피격 Sparks 1개를 해당 애니메이션과 타격 순간에 맞춰 비교한다. 원거리 후보도 비교하고 게임 판정과 시각 효과의 차이를 확인한다.
5. 테스트 후 개발자가 플레이어 모델, 실제 3타·찌르기·회피 클립, 맵의 기본 팩, 효과 후보를 선택한다. UI·오디오 및 세부 전투 타이밍은 이번 선정 범위 밖이다.

TopDown Engine 위치 정정: `Assets/TopDownEngine/Common/Scripts/Characters` 아래 `CharacterMovement.cs`, `CharacterDash3D.cs`, `Weapons/MeleeWeapon.cs`가 존재한다. 이번 작업은 에셋 선정 분석이므로 엔진 기능 구현 및 재사용 범위는 평가하지 않았다.
