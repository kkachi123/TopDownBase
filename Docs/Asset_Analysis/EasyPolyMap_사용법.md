# EasyPolyMap 간단 사용법

현재 프로젝트에 설정된 EasyPolyMap으로 지형을 만들고 프리팹으로 저장하는 순서다.

1. Unity에서 사용할 씬을 열고 `Assets/Game/Environment/EasyPolyMap/TerrainCreator.prefab`을 Hierarchy에 드래그한다.
2. 배치한 오브젝트를 선택하고 Inspector에서 지형 이름과 크기를 설정한다.
3. **Create Data**를 눌러 편집 데이터를 만든다.
4. **OBJECTS SECTION > Visual Editor**를 열고 지형 종류를 선택한 뒤 **New**를 누른다. 화면을 클릭해 점을 찍어 지형을 그린다.
   - `Island`, `SandGround`, `SoilGround`는 **CLOSE REGION**으로 영역을 닫는다.
5. 창을 닫고 **CreateTerrain**을 눌러 지형을 생성한다. 필요하면 설정을 바꾸고 다시 생성해 형태를 확인한다.
6. **Save Data**로 편집 데이터를 저장한다. 기존 예제 데이터를 사용할 때는 복사본을 사용한다.
7. 완성되면 **ExportTerrain**을 누른다. `Assets/EPMTerrainOutput/<지형 이름>/`에 생성된 프리팹을 원하는 씬에 드래그해 사용한다.

- 육지에는 MeshCollider가 자동으로 생성되며 바다에는 생성되지 않는다.
- 같은 이름의 출력 폴더가 있으면 덮어쓰기 확인창이 나타난다. 기존 결과를 보존하려면 다른 이름을 사용한다.
- 지형 생성과 저장된 프리팹의 참조는 Unity 에디터에서 확인했다. 실제 캐릭터 이동과 빌드는 아직 검증하지 않았다.
